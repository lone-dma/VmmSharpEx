using VmmSharpEx;
using VmmSharpEx.Scatter;

namespace VmmSharpEx_Tests
{
    internal static class ScatterMap_Example
    {
        public static void Example()
        {
            /// Boilerplate
            Vmm vmm = default; // Assume this is initialized properly
            if (!vmm!.PidGetFromName("TargetProcess.exe", out uint pid))
                throw new InvalidOperationException("Failed to get PID!");
            var list = new List<ulong>(); // Assume this is populated with addresses to read

            /// Example begin
            var map = new ScatterReadMap(vmm, pid);
            var r1 = map.AddRound(useCache: true);
            var r2 = map.AddRound(useCache: false); // Example of multiple rounds if needed, use realtime reads on this one
            for (int ix = 0; ix < list.Count; ix++)
            {
                int i = ix; // Capture
                r1[i].AddValueEntry<ulong>(0, list[i] + 0x10); // Read a pointer type
                r1[i].AddArrayEntry<byte>(1, list[i] + 0x100, 64); // Read a byte array of 64 bytes
                r1[i].Completed += (sender, cb1) =>
                {
                    if (cb1.TryGetValue<ulong>(0, out var ptr))
                    {
                        // Do stuff with this pointer if read is successful!
                        Console.WriteLine($"Pointer: 0x{ptr:X}");
                        r2[i].AddValueEntry<uint>(0, ptr + 0x20); // Use this pointer in the next round to read a uint
                        r2[i].Completed += (sender, cb2) =>
                        {
                            if (cb2.TryGetValue<uint>(0, out var val))
                            {
                                // Do stuff with this uint if read is successful!
                                Console.WriteLine($"Value: {val}");
                            }
                        };
                    }
                    if (cb1.TryGetArray<byte>(1, out var bytes))
                    {
                        // Do stuff with the byte array if read is successful!
                        Console.WriteLine($"Bytes: {BitConverter.ToString(bytes.ToArray())}");
                    }
                };
            }
            map.Execute(); // Begin the scatter read(s) -> The callbacks will be executed as reads complete
        }
    }
}
