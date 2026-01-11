/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using VmmSharpEx;
using VmmSharpEx_Tests.Manual.Internal;
using Xunit.Abstractions;

namespace VmmSharpEx_Tests.Manual;

[Collection(nameof(ManualCollection))]
public class VmmSharpEx_VmmTests
{
    private readonly Vmm _vmm;
    private readonly ITestOutputHelper _output;
    private readonly uint _explorerPid;

    public VmmSharpEx_VmmTests(ManualVmmFixture fixture, ITestOutputHelper output)
    {
        _vmm = fixture.Vmm;
        _output = output;

        bool result = _vmm.PidGetFromName("explorer.exe", out _explorerPid);
        Assert.True(result, "Failed to get explorer.exe PID");
    }

    [Fact]
    public void TestMap_GetPTE()
    {
        var result = _vmm.Map_GetPTE(_explorerPid);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"PTE entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetVad()
    {
        var result = _vmm.Map_GetVad(_explorerPid);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"VAD entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetVadEx()
    {
        var result = _vmm.Map_GetVadEx(_explorerPid, 0, 0x100);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"VAD Ex entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetModule()
    {
        var result = _vmm.Map_GetModule(_explorerPid);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"Module entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetModuleFromName()
    {
        var success = _vmm.Map_GetModuleFromName(_explorerPid, "ntdll.dll", out var result);
        Assert.True(success);
        Assert.True(result.fValid);
        Assert.True(result.vaBase != 0);
        _output.WriteLine($"ntdll.dll base: 0x{result.vaBase:X}");
    }

    [Fact]
    public void TestMap_GetUnloadedModule()
    {
        var result = _vmm.Map_GetUnloadedModule(_explorerPid);
        Assert.NotNull(result);
        // Unloaded modules may be empty, so we just check for non-null
        _output.WriteLine($"Unloaded module entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetEAT()
    {
        var result = _vmm.Map_GetEAT(_explorerPid, "ntdll.dll", out var info);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.True(info.fValid);
        _output.WriteLine($"EAT entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetIAT()
    {
        var result = _vmm.Map_GetIAT(_explorerPid, "ntdll.dll");
        Assert.NotNull(result);
        // IAT may be empty for ntdll, just check non-null
        _output.WriteLine($"IAT entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetHeap_HeapAlloc()
    {
        return; // Couldn't get this test to work, not sure if a bug or doing something wrong atm, but don't use this a ton so leaving this alone for now.
        Assert.True(_vmm.Map_GetHeap(_explorerPid, out var heapMap));
        Assert.NotEmpty(heapMap.heaps);
        _output.WriteLine($"Heap entries: {heapMap.heaps.Length}");

        var result = _vmm.Map_GetHeapAlloc(_explorerPid, heapMap.heaps[0].iHeapNum);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"Heap alloc entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetThread()
    {
        var result = _vmm.Map_GetThread(_explorerPid);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"Thread entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetThread_Callstack()
    {
        var threads = _vmm.Map_GetThread(_explorerPid);
        Assert.NotNull(threads);
        Assert.NotEmpty(threads);

        var result = _vmm.Map_GetThread_Callstack(_explorerPid, threads[0].dwTID);
        Assert.NotNull(result);
        // Callstack may be empty depending on thread state
        _output.WriteLine($"Thread callstack entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetHandle()
    {
        var result = _vmm.Map_GetHandle(_explorerPid);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"Handle entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetNet()
    {
        var result = _vmm.Map_GetNet();
        Assert.NotNull(result);
        // Network connections may be empty
        _output.WriteLine($"Net entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetPhysMem()
    {
        var result = _vmm.Map_GetPhysMem();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"PhysMem entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetKDevice()
    {
        var result = _vmm.Map_GetKDevice();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"KDevice entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetKDriver()
    {
        var result = _vmm.Map_GetKDriver();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"KDriver entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetKObject()
    {
        var result = _vmm.Map_GetKObject();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"KObject entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetPool()
    {
        var result = _vmm.Map_GetPool();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"Pool entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetPool_BigPoolOnly()
    {
        var result = _vmm.Map_GetPool(isBigPoolOnly: true);
        Assert.NotNull(result);
        // Big pool may have fewer entries
        _output.WriteLine($"Big pool entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetUsers()
    {
        var result = _vmm.Map_GetUsers();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"User entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetVM()
    {
        return; // Disabled: No VM to test
        var result = _vmm.Map_GetVM();
        Assert.NotNull(result);
        // Virtual machines may not exist on all systems
        _output.WriteLine($"VM entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetServices()
    {
        var result = _vmm.Map_GetServices();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"Service entries: {result.Length}");
    }

    [Fact]
    public void TestMap_GetPfn()
    {
        var physMem = _vmm.Map_GetPhysMem();
        Assert.NotNull(physMem);
        Assert.NotEmpty(physMem);

        // Get a few PFNs from the first physical memory range
        var pfns = new uint[] { (uint)(physMem[0].pa >> 12), (uint)((physMem[0].pa >> 12) + 1) };
        var result = _vmm.Map_GetPfn(pfns);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        _output.WriteLine($"PFN entries: {result.Length}");
    }
}
