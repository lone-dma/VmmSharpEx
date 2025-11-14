/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx;

namespace VmmSharpEx_Tests.Fixtures
{
    public unsafe class VmmFixture : IDisposable
    {
        private const string DUMP_FILE = "dump.raw";
        private const ulong PTR_STR_UNICODE = 0x0;
        private const ulong PTR_HEAP = 0x8;
        private const ulong ADDR_HEAPLEN = 0x10;
        private const string EXPECTED_STR = "Hello, World!";

        /// <summary>
        /// Vmm Instance connected to target
        /// </summary>
        public Vmm Vmm { get; }
        /// <summary>
        /// Address of the heap in the target.
        /// </summary>
        public ulong Heap { get; }
        /// <summary>
        /// Length of the heap in the target.
        /// </summary>
        public int HeapLen { get; }

        public VmmFixture()
        {
            if (!File.Exists(DUMP_FILE))
                throw new FileNotFoundException("The specified memory dump file was not found!", DUMP_FILE);
            // Initialize VMM
            string[] args = new[]
            {
                "-_internal_physical_memory_only",
                "-f",
                $"file://file={DUMP_FILE},write=1",
                "-waitinitialize",
                "-norefresh",
                "-loglevel",
                "3"
            };
            Vmm = new Vmm(args);
            // Validate everything
            var unicodeStr = Vmm.MemReadString(Vmm.PID_PHYSICALMEMORY, ReadPtr(PTR_STR_UNICODE), 64, Encoding.Unicode);
            Assert.Equal(EXPECTED_STR, unicodeStr);
            Assert.True(Vmm.MemReadValue<int>(Vmm.PID_PHYSICALMEMORY, ADDR_HEAPLEN, out var heapLen));
            Heap = ReadPtr(PTR_HEAP);
            HeapLen = heapLen;
            var pMem = NativeMemory.Alloc((nuint)HeapLen);
            try
            {
                Assert.True(Vmm.MemRead(Vmm.PID_PHYSICALMEMORY, Heap, pMem, (uint)HeapLen, out uint cbRead));
                Assert.Equal((uint)HeapLen, cbRead);
            }
            finally
            {
                NativeMemory.Free(pMem);
            }
        }

        private ulong ReadPtr(ulong pa)
        {
            Assert.True(Vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, pa, out var p), $"Failed reading pointer at 0x{pa:X}");
            Assert.NotEqual(0ul, p);
            return p;
        }

        public void Dispose()
        {
            Vmm.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
