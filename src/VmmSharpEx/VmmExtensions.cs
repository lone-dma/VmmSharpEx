using System.Diagnostics;
using System.Text;
using VmmSharpEx.Internal;

namespace VmmSharpEx
{
    /// <summary>
    /// Contains various VMM extension methods to implement additional functionality.
    /// </summary>
    public static class VmmExtensions
    {

        /// <summary>
        /// This fixes the database shuffling that EAC does.
        /// It fixes it by iterating over all DTB's that exist within your system and looks for specific ones
        /// that nolonger have a PID assigned to them, aka their pid is 0
        /// it then puts it in a vector to later try each possible DTB to find the DTB of the process.
        /// NOTE: Using FixCR3 requires you to have symsrv.dll, dbghelp.dll and info.db
        /// CREDIT: Contributed by Mambo, but based off Metick's DMA Lib https://github.com/Metick/DMALibrary :)
        /// </summary>
        /// <param name="vmm">Vmm instance.</param>
        /// <param name="processName">Process name to fix.</param>
        /// <param name="pid">PID of process to fix.</param>
        /// <returns>TRUE if successful, otherwise FALSE.</returns>
        public static bool FixCr3_EAC(this Vmm vmm, string processName, uint pid)
        {
            const ulong vmmdll_opt_process_dtb = 0x2002000100000000;
            // If already mapped successfully, skip
            var mod = vmm.Map_GetModuleFromName(pid, processName);
            if (mod.fValid)
                return true;

            // Ensure plugins are ready
            if (!Vmmi.VMMDLL_InitializePlugins(vmm))
            {
                Debug.WriteLine("[-] Failed VMMDLL_InitializePlugins");
                return false;
            }

            Thread.Sleep(500); // Let plugin init finish

            // Wait for progress to reach 100%
            while (true)
            {
                var percentBytes = vmm.VfsRead(@"\misc\procinfo\progress_percent.txt", 4);
                if (percentBytes.Length > 0 &&
                    int.TryParse(Encoding.ASCII.GetString(percentBytes).Trim(), out int percent) &&
                    percent == 100)
                    break;

                Thread.Sleep(100);
            }

            // VFS list and read DTBs
            var vfsEntries = vmm.VfsList(@"\misc\procinfo\");
            var dtbRaw = vmm.VfsRead(@"\misc\procinfo\dtb.txt", 0x1000);
            if (dtbRaw.Length == 0)
                return false;

            var possibleDtbs = new List<ulong>();
            var lines = Encoding.ASCII.GetString(dtbRaw)
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 6)
                    continue;

                try
                {
                    uint parsedPid = uint.Parse(tokens[1]);
                    ulong dtb = Convert.ToUInt64(tokens[2], 16);
                    string name = tokens[5];

                    if (parsedPid == 0 || processName.Contains(name))
                        possibleDtbs.Add(dtb);
                }
                catch { }
            }

            foreach (var dtb in possibleDtbs)
            {
                Vmmi.VMMDLL_ConfigSet(vmm, (VmmSharpEx.Options.VmmOption)(vmmdll_opt_process_dtb | pid), dtb);
                mod = vmm.Map_GetModuleFromName(pid, processName);
                if (mod.fValid)
                {
                    Debug.WriteLine($"[+] Patched DTB: 0x{dtb:X}");
                    return true;
                }
            }

            Debug.WriteLine("[-] Failed to patch DTB");
            return false;
        }
    }
}
