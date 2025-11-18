/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

/*
MIT License

Copyright (c) 2023 Metick

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

// Thanks to Metick for the original implementation! https://github.com/Metick/DMALibrary/blob/Master/DMALibrary/Memory/InputManager.cpp

using System.Runtime.CompilerServices;
using VmmSharpEx.Internal;

namespace VmmSharpEx.Extensions.Input
{
    /// <summary>
    /// Extension class that queries user input state via Win32 Kernel Interop (Read-Only).
    /// </summary>
    /// <remarks>
    /// NOTE: Only works on Windows Version 22000 and newer (Windows 11).
    /// </remarks>
    public sealed class VmmInputManager
    {
        private readonly Vmm _vmm;

        private readonly byte[] _stateBitmap = new byte[64];
        private readonly byte[] _previousStateBitmap = new byte[256 / 8];
        private readonly ulong _gafAsyncKeyStateExport;
        private readonly uint _winLogonPid;

        private VmmInputManager() { }

        /// <summary>
        /// Extension class that queries user input state via Win32 Kernel Interop (Read-Only).
        /// </summary>
        /// <remarks>
        /// NOTE: Only works on Windows Version 22000 and newer (Windows 11).
        /// </remarks>
        /// <param name="vmm">Parent VMM Instance (must be already initialized).</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="AggregateException"></exception>
        public VmmInputManager(Vmm vmm)
        {
            _vmm = vmm;
            if (!vmm.PidGetFromName("winlogon.exe", out _winLogonPid))
                throw new InvalidOperationException("Failed to get winlogon.exe PID");
            var pids = _vmm.PidGetAllFromName("csrss.exe");
            ulong gafAsyncKeyStateExport = 0;

            var exceptions = new List<Exception>();
            foreach (var pid in pids)
            {
                try
                {
                    if (!_vmm.Map_GetModuleFromName(pid, "win32ksgd.sys", out var win32kModuleInfo))
                    {
                        if (!_vmm.Map_GetModuleFromName(pid, "win32k.sys", out win32kModuleInfo))
                            throw new InvalidOperationException("Failed to get win32kModule");
                    }
                    ulong win32kBase = win32kModuleInfo.vaBase;
                    ulong win32kSize = win32kModuleInfo.cbImageSize;

                    ulong gSessionPtr = _vmm.FindSignature(pid, "48 8B 05 ?? ?? ?? ?? 48 8B 04 C8", win32kBase, win32kBase + win32kSize);
                    if (gSessionPtr == 0)
                    {
                        gSessionPtr = _vmm.FindSignature(pid, "48 8B 05 ?? ?? ?? ?? FF C9", win32kBase, win32kBase + win32kSize);
                        if (!Utilities.IsValidVirtualAddress(gSessionPtr))
                            throw new ArgumentOutOfRangeException(nameof(gSessionPtr), "Failed to find gSessionPtr signature");
                    }
                    int relative = Read<int>(pid, gSessionPtr + 3);
                    ulong gSessionGlobalSlots = gSessionPtr + 7 + (ulong)relative;
                    ulong userSessionState = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        userSessionState = Read<ulong>(pid, Read<ulong>(pid, Read<ulong>(pid, gSessionGlobalSlots) + (ulong)(8 * i)));
                        if (userSessionState > 0x7FFFFFFFFFFF)
                            break;
                    }

                    if (!_vmm.Map_GetModuleFromName(pid, "win32kbase.sys", out var win32kbaseModule))
                        throw new InvalidOperationException("failed to get module win32kbase info");
                    ulong win32kbaseBase = win32kbaseModule.vaBase;
                    ulong win32kbaseSize = win32kbaseModule.cbImageSize;

                    ulong ptr = _vmm.FindSignature(pid, "48 8D 90 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F 57 C0", win32kbaseBase, win32kbaseBase + win32kbaseSize);
                    uint sessionOffset = 0;
                    if (ptr != 0)
                    {
                        sessionOffset = Read<uint>(pid, ptr + 3);
                        gafAsyncKeyStateExport = userSessionState + sessionOffset;
                    }
                    else
                    {
                        throw new InvalidOperationException("failed to find offset for gafAyncKeyStateExport");
                    }

                    if (gafAsyncKeyStateExport > 0x7FFFFFFFFFFF)
                        break;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
            if (gafAsyncKeyStateExport <= 0x7FFFFFFFFFFF)
                throw new AggregateException("Invalid gafAsyncKeyStateExport", exceptions);

            _gafAsyncKeyStateExport = gafAsyncKeyStateExport;
        }

        private T Read<T>(uint pid, ulong address)
            where T : unmanaged
        {
            if (!_vmm.MemReadValue<T>(pid, address, out var result))
                throw new VmmException("Memory Read Failed!");
            return result;
        }

        /// <summary>
        /// Updates the internal key state bitmap. Call this method periodically to refresh key states.
        /// </summary>
        /// <remarks>
        /// Typically you would call this before you make <see cref="IsKeyDown(uint)"/> calls.
        /// </remarks>
        /// <exception cref="VmmException"></exception>
        public void UpdateKeys()
        {
            Span<byte> previousKeyStateBitmap = stackalloc byte[64];
            _stateBitmap.CopyTo(previousKeyStateBitmap);

            // Read 64 bytes from gafAsyncKeyStateExport
            if (!_vmm.MemReadSpan(_winLogonPid | Vmm.PID_PROCESS_WITH_KERNELMEMORY, _gafAsyncKeyStateExport, _stateBitmap, VmmSharpEx.Options.VmmFlags.NOCACHE))
                throw new VmmException("Failed to read key state bitmap.");

            for (int vk = 0; vk < 256; ++vk)
                if ((_stateBitmap[(vk * 2 / 8)] & (1 << ((vk % 4) * 2))) != 0 && (previousKeyStateBitmap[(vk * 2 / 8)] & (1 << ((vk % 4) * 2))) == 0)
                    _previousStateBitmap[vk / 8] |= (byte)(1 << (vk % 8));
        }

        /// <summary>
        /// Checks if a given Virtual Key is currently down.
        /// See: <see href="https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes"/>
        /// </summary>
        /// <remarks>
        /// Recommend calling <see cref="UpdateKeys"/> before calling this method to ensure the key states are up-to-date.
        /// </remarks>
        /// <param name="vkey">Windows virtual key.</param>
        /// <returns><see langword="true"/> if key is down, otherwise <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsKeyDown(Win32VirtualKey vkey) => IsKeyDown((uint)vkey);

        /// <summary>
        /// Checks if a given Virtual Key is currently down.
        /// See: <see href="https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes"/>
        /// </summary>
        /// <remarks>
        /// Recommend calling <see cref="UpdateKeys"/> before calling this method to ensure the key states are up-to-date.
        /// </remarks>
        /// <param name="vkeyCode">Windows virtual key code.</param>
        /// <returns><see langword="true"/> if key is down, otherwise <see langword="false"/>.</returns>
        public bool IsKeyDown(uint vkeyCode)
        {
            if (_gafAsyncKeyStateExport < 0x7FFFFFFFFFFF)
                return false;
            int idx = (int)(vkeyCode * 2 / 8);
            int bit = 1 << ((int)vkeyCode % 4 * 2);
            return (_stateBitmap[idx] & bit) != 0;
        }
    }
}
