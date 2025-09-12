using System.Text;
using VmmSharpEx;

namespace VmmSharpEx_Tests.Fixtures
{
    public class VmmFixture : IDisposable
    {
        /// <summary>
        /// Remote target process name
        /// </summary>
        public const string TargetProcess = "VmmTestRemote.exe";
        /// <summary>
        /// Vmm Instance connected to remote target
        /// </summary>
        public Vmm Vmm { get; }
        /// <summary>
        /// PID for remote target process
        /// </summary>
        public uint PID { get; }
        /// <summary>
        /// Module base address for remote target process
        /// </summary>
        public ulong ModuleBase { get; }
        /// <summary>
        /// Memory address of the ~1GB memory buffer in target process
        /// </summary>
        public ulong CodeCave { get; }

        public VmmFixture()
        {
            try
            {
                // Initialize VMM
                string[] args = new[]
                {
                    "-device",
                    "fpga",
                    "-waitinitialize"
                };
                Vmm = new Vmm(args);
                if (!Vmm.PidGetFromName(TargetProcess, out uint pid))
                    throw new InvalidOperationException($"Unable to find target process '{TargetProcess}'");
                PID = pid;
                ModuleBase = Vmm.ProcessGetModuleBase(PID, TargetProcess);
                if (ModuleBase == 0)
                    throw new InvalidOperationException($"Unable to find target process module base '{TargetProcess}'");
                if (!Vmm.MemReadValue<VmmPointer>(PID, ModuleBase + 0x40A0, out var codeCave) || !codeCave.IsValid)
                    throw new InvalidOperationException("Unable to read target process code cave address");
                string result = Vmm.MemReadString(PID, codeCave, 24, Encoding.Unicode);
                if (!result?.StartsWith("hello :)", StringComparison.OrdinalIgnoreCase) ?? false)
                    throw new InvalidOperationException("Target process code cave memory is not initialized correctly!");
                CodeCave = codeCave;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize remote vmm test environment!", ex);
            }
        }

        public void Dispose()
        {
            // Tell target process to exit via shellcode injection
            var shellcode = new byte[] { 0x31, 0xC0, 0x48, 0x83, 0xC4, 0x20, 0x5B, 0xC3 }; // xor eax,eax; add rsp,20; pop rbx; ret
            Vmm.MemWriteSpan(PID, ModuleBase + 0x1423, shellcode.AsSpan());
            // Cleanup Vmm native handle
            Vmm.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
