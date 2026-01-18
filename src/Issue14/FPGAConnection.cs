using VmmSharpEx;
using VmmSharpEx.Refresh;

namespace Issue14
{
    public sealed class FPGAConnection : IDisposable
    {
        public Vmm Vmm { get; }
        public IReadOnlyList<PMemPageEntry> PhysicalMemoryPages { get; }

        public FPGAConnection()
        {
            string[] args = [
                "-device",
                "fpga",
                "-norefresh",
                "-waitinitialize",
                "-printf",
                "-v"
            ];
            Vmm = new Vmm(args)
            {
                EnableMemoryWriting = false
            };
            _ = Vmm.GetMemoryMap(
                applyMap: true);
            Vmm.RegisterAutoRefresh(RefreshOption.MemoryPartial, TimeSpan.FromMilliseconds(300));
            Vmm.RegisterAutoRefresh(RefreshOption.TlbPartial, TimeSpan.FromSeconds(2));
            var physMemMap = Vmm.Map_GetPhysMem() ??
                throw new InvalidOperationException("Failed to get physical memory map.");
            var paList = new List<PMemPageEntry>();
            foreach (var pMapEntry in physMemMap)
            {
                for (ulong p = pMapEntry.pa, cbToEnd = pMapEntry.cb;
                    cbToEnd > 0x1000;
                    p += 0x1000, cbToEnd -= 0x1000)
                {
                    paList.Add(new()
                    {
                        PageBase = p,
                        RemainingBytesInSection = cbToEnd
                    });
                }
            }
            var pages = paList.ToArray();
            Random.Shared.Shuffle(pages);
            PhysicalMemoryPages = pages;
        }

        public PMemPageEntry GetRandomPage()
        {
            return PhysicalMemoryPages[
                Random.Shared.Next(PhysicalMemoryPages.Count)];
        }

        public IEnumerable<PMemPageEntry> GetRandomPages(int count)
        {
            var pages = new List<PMemPageEntry>();
            for (int i = 0; i < count; i++)
            {
                pages.Add(GetRandomPage());
            }
            return pages;
        }

        public void Dispose()
        {
            Vmm.Dispose();
        }
    }
}
