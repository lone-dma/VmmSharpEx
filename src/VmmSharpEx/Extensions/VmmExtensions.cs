using System.Diagnostics;
using System.Text;
using VmmSharpEx.Options;

namespace VmmSharpEx.Extensions
{
    /// <summary>
    /// Contains various Core VMM extension methods to implement additional functionality.
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
            // If already mapped successfully, skip
            if (vmm.Map_GetModuleFromName(pid, processName, out var mod) && mod.fValid)
            {
                return true;
            }
            // Ensure plugins are ready
            if (!vmm.InitializePlugins())
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
                vmm.ConfigSet((VmmOption)((ulong)VmmOption.PROCESS_DTB | pid), dtb);
                if (vmm.Map_GetModuleFromName(pid, processName, out mod) && mod.fValid)
                {
                    Debug.WriteLine($"[+] Patched DTB: 0x{dtb:X}");
                    return true;
                }
            }

            Debug.WriteLine("[-] Failed to patch DTB");
            return false;
        }

        /// <summary>
        /// Find a signature within a process' memory.
        /// </summary>
        /// <param name="vmm">Vmm instance.</param>
        /// <param name="pid">Process to search within.</param>
        /// <param name="signature">Signature to search for. Hex Characters (separated by space) with optional ?? wildcard mask. Ex: 0F 1F ?? ?? 90 AA</param>
        /// <param name="vaMin">(Optional) Minimum Virtual Address to begin scanning at. By default will scan whole process.</param>
        /// <param name="vaMax">(Optional) Maximum Virtual Address to end scanning at. By default will scan whole process.</param>
        /// <returns>Address of first occurrence of signature, otherwise 0 if failed.</returns>
        public static ulong FindSignature(this Vmm vmm, uint pid, string signature, ulong vaMin = 0, ulong vaMax = ulong.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(vmm, nameof(vmm));
            ArgumentException.ThrowIfNullOrEmpty(signature, nameof(signature));
            string[] sigSplit = signature.Split(' ');
            ArgumentOutOfRangeException.ThrowIfGreaterThan(sigSplit.Length, 32, nameof(signature));
            byte[] searchBytes = new byte[sigSplit.Length];
            byte[] skipBytes = new byte[sigSplit.Length];
            for (int i = 0; i< sigSplit.Length; i++)
            {
                string byteStr = sigSplit[i];
                if (byteStr.StartsWith('?'))
                {
                    searchBytes[i] = 0;
                    skipBytes[i] = 0xff;
                }
                else
                {
                    searchBytes[i] = byte.Parse(byteStr, System.Globalization.NumberStyles.HexNumber);
                    skipBytes[i] = 0;
                }
            }
            using var vmmSearch = vmm.CreateSearch(
                pid: pid,
                addr_min: vaMin,
                addr_max: vaMax,
                cMaxResult: 1);
            vmmSearch.AddEntry(
                search: searchBytes,
                skipmask: skipBytes);
            var result = vmmSearch.Result;
            if (result.Results.IsEmpty)
            {
                Debug.WriteLine("[FindSignature] No results found");
                return 0;
            }
            return result.Results.First().Address;
        }
    }
}
