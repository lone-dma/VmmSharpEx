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

        private VmmInputManager() { throw new NotImplementedException(); }

        /// <summary>
        /// Extension class that queries user input state via Win32 Kernel Interop (Read-Only).
        /// </summary>
        /// <remarks>
        /// NOTE: Only works on Windows Version 22000 and newer (Windows 11).
        /// </remarks>
        /// <param name="vmm">Parent VMM Instance (must be already initialized).</param>
        /// <exception cref="VmmException"></exception>
        public VmmInputManager(Vmm vmm)
        {
            _vmm = vmm;
            if (!_vmm.PidGetFromName("winlogon.exe", out _winLogonPid))
                throw new VmmException("Failed to get winlogon.exe PID");
            var pids = _vmm.PidGetAllFromName("csrss.exe");
            if (pids is null || pids.Length == 0)
                throw new VmmException("Failed to get csrss.exe PIDs");
            ulong gafAsyncKeyStateExport = 0;

            var exceptions = new List<Exception>();
            foreach (var pid in pids)
            {
                try
                {
                    if (!_vmm.Map_GetModuleFromName(pid, "win32ksgd.sys", out var win32kModuleInfo))
                    {
                        if (!_vmm.Map_GetModuleFromName(pid, "win32k.sys", out win32kModuleInfo))
                            throw new VmmException("Failed to get win32kModule");
                    }
                    ulong win32kBase = win32kModuleInfo.vaBase;
                    ulong win32kSize = win32kModuleInfo.cbImageSize;

                    ulong gSessionPtr = _vmm.FindSignature(pid, "48 8B 05 ?? ?? ?? ?? 48 8B 04 C8", win32kBase, win32kBase + win32kSize);
                    if (gSessionPtr == 0)
                    {
                        gSessionPtr = _vmm.FindSignature(pid, "48 8B 05 ?? ?? ?? ?? FF C9", win32kBase, win32kBase + win32kSize);
                        if (gSessionPtr == 0)
                            throw new VmmException("failed to find gSessionPtr signature");
                    }
                    int relative = _vmm.MemReadValue<int>(pid, gSessionPtr + 3);
                    ulong gSessionGlobalSlots = gSessionPtr + 7 + (ulong)relative;
                    ulong userSessionState = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        userSessionState = _vmm.MemReadValue<ulong>(pid, _vmm.MemReadValue<ulong>(pid, _vmm.MemReadValue<ulong>(pid, gSessionGlobalSlots) + (ulong)(8 * i)));
                        if (userSessionState.IsValidKernelVA())
                            break;
                    }

                    if (!_vmm.Map_GetModuleFromName(pid, "win32kbase.sys", out var win32kbaseModule))
                        throw new VmmException("failed to get module win32kbase info");
                    ulong win32kbaseBase = win32kbaseModule.vaBase;
                    ulong win32kbaseSize = win32kbaseModule.cbImageSize;

                    ulong ptr = _vmm.FindSignature(pid, "48 8D 90 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F 57 C0", win32kbaseBase, win32kbaseBase + win32kbaseSize);
                    if (ptr == 0)
                        throw new VmmException("failed to find gafAsyncKeyStateExport signature");
                    uint sessionOffset = _vmm.MemReadValue<uint>(pid, ptr + 3);
                    gafAsyncKeyStateExport = userSessionState + sessionOffset;

                    if (gafAsyncKeyStateExport.IsValidKernelVA())
                        break;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
            gafAsyncKeyStateExport.ThrowIfInvalidKernelVA(nameof(gafAsyncKeyStateExport));

            _gafAsyncKeyStateExport = gafAsyncKeyStateExport;
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
            if (!_gafAsyncKeyStateExport.IsValidKernelVA())
                return false;
            int idx = (int)(vkeyCode * 2 / 8);
            int bit = 1 << ((int)vkeyCode % 4 * 2);
            return (_stateBitmap[idx] & bit) != 0;
        }
    }
}
