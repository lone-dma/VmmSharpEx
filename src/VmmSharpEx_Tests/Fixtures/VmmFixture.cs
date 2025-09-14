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
        /// Memory address of the R/W Memory Region in Target Process (16 Pages, Aligned)
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
                _ = Vmm.GetMemoryMap(applyMap: true);
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
                CodeCave = (codeCave + 0x1000) & ~0xffful; // Buffer has 17 pages, align working area to the next page boundary (total 16 pages)
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize remote vmm test environment!", ex);
            }
        }

        public void Dispose()
        {
            Vmm.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
