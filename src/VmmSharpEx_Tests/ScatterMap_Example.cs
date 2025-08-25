using System.Text;
using VmmSharpEx;
using VmmSharpEx.Scatter;

namespace VmmSharpEx_Tests
{
    internal static class ScatterMap_Example
    {
        public static void Run(Vmm vmm)
        {
            if (!vmm.PidGetFromName("explorer.exe", out uint pid))
                throw new InvalidOperationException("Failed to get PID");
            ulong baseAddress = vmm.ProcessGetModuleBase(pid, "explorer.exe");
            // Create a ScatterReadMap to read multiple addresses in one go
            using var map = new ScatterReadMap(vmm, pid); // MUST DISPOSE
            // Add two rounds of reads, the second round will be executed after the first completes
            var rd1 = map.AddRound();
            var rd2 = map.AddRound(useCache: false);
            for (int ix = 0; ix < 3; ix++) // You can use this API to iterate over collections!
            {
                int i = ix; // Capture loop variable (IMPORTANT!)
                // Add entries to the first round
                rd1[i].AddValueEntry<uint>(0, baseAddress + (uint)i * 4);
                rd1[i].AddStringEntry(1, baseAddress, 16, Encoding.ASCII);
                // Add a completion callback for the first round
                rd1[i].Completed += (sender, cb1) =>
                {
                    // Check if the read was successful and get the value
                    if (cb1.TryGetValue<uint>(0, out var value))
                    {
                        // Successful do something with the value
                        Console.WriteLine($"DWORD: {value}");
                        // Now chain a second round after the first completes
                        // You can do this to read successive pointers, etc.
                        rd2[i].AddArrayEntry<byte>(0, baseAddress + (uint)i * 12, 12);
                        // Add a completion callback for the second round
                        rd2[i].Completed += (sender, cb2) =>
                        {
                            // Check if the read was successful and get the byte array
                            if (cb2.TryGetArray<byte>(0, out var bytes))
                            {
                                // Do something with the byte array
                                Console.WriteLine($"Bytes: {BitConverter.ToString(bytes.ToArray())}");
                            }
                        };
                    }
                    // Check our string read
                    if (cb1.TryGetString(1, out var str))
                    {
                        Console.WriteLine($"String: {str}"); // Start of the Windows PE should be "MZ"
                    }
                };
            }
            // Execute the scatter read map as defined above
            map.Execute();
        }
    }
}
