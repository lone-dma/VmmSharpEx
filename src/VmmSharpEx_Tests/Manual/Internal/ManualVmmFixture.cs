/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using VmmSharpEx;

namespace VmmSharpEx_Tests.Manual.Internal;

public sealed class ManualVmmFixture : IDisposable
{
    public Vmm Vmm { get; }

    public ManualVmmFixture()
    {
        var args = new[]
        {
            "-device",
            "fpga",
            "-waitinitialize"
        };

        Vmm = new Vmm(args);
        Vmm.InitializePlugins();
    }

    public void Dispose()
    {
        Vmm.Dispose();
        GC.SuppressFinalize(this);
    }
}
