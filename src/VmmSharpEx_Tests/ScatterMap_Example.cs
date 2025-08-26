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
                int i = ix; // Capture loop variable (IMPORTANT! ix goes out of scope in subsequent callbacks)
                // Add entries to the first round
                var pointer = rd1[i].AddValueEntry<VmmPointer>(0, baseAddress + 312); // VmmPointer is automatically validated, and can be cast to ulong
                rd1[i].Completed += (sender, cb1) => // Executed after round 1 completes
                {
                    if (cb1.TryGetValue<VmmPointer>(0, out var pointer)) // Check if read succeeded
                    {
                        // Pointer was verified to be valid, do something with it,etc....
                        Console.WriteLine($"Validated Pointer: {(ulong)pointer:X}");
                        rd2[i].AddArrayEntry<byte>(0, pointer, 16); // Add a byte array read in round 2 using the pointer location
                    }
                    // Add a string entry
                    rd2[i].AddStringEntry(1, baseAddress, 16, Encoding.ASCII);
                    rd2[i].Completed += (sender, cb2) => // Executed after round 2 completes
                    {
                        if (cb2.TryGetArray<byte>(0, out var bytes)) // Check if read succeeded
                        {
                            // Do something with the byte array
                            Console.WriteLine($"Bytes at pointer location: {BitConverter.ToString(bytes.ToArray())}");
                        }
                        if (cb2.TryGetString(1, out var str)) // Check if read succeeded
                        {
                            Console.WriteLine($"PE Header String: {str}"); // Start of the Windows PE should be "MZ"
                        }
                    };
                };
            }
            // Execute the scatter read map as defined above
            map.Execute();
        }
    }
}
