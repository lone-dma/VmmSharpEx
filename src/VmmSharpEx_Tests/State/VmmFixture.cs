using VmmSharpEx;

namespace VmmSharpEx_Tests.State
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
                CodeCave = codeCave;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize remote vmm test environment!", ex);
            }
        }

        public void Dispose()
        {
            // cleanup after all tests finish
            Vmm.Dispose();
        }
    }
}
