/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using VmmSharpEx;
using VmmSharpEx_Tests.Fixtures;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public class VmmSharpEx_LeechCoreTests
{
    private readonly Vmm _vmm;
    private readonly LeechCore _lc;

    public VmmSharpEx_LeechCoreTests(VmmFixture fixture)
    {
        _vmm = fixture.Vmm;
        _lc = _vmm.LeechCore;
        Assert.NotNull(_lc);
    }
}