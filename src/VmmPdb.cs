using System.Text;
using VmmSharpEx.Internal;

namespace VmmSharpEx;

/// <summary>
///     The PDB sub-system requires that MemProcFS supporting DLLs/.SO’s for debugging and symbol server are put alongside
///     vmm.dll.
///     Also it’s recommended that the file info.db is put alongside vmm.dll.
/// </summary>
public sealed class VmmPdb
{
    #region Base Functionality

    private readonly Vmm _hVmm;

    private VmmPdb()
    {
        ;
    }

    internal VmmPdb(Vmm hVmm, string module)
    {
        _hVmm = hVmm;
        Module = module;
    }

    internal VmmPdb(Vmm hVmm, uint pid, ulong vaModuleBase)
    {
        var szModuleName = "";
        var data = new byte[260];
        unsafe
        {
            fixed (byte* pb = data)
            {
                var result = Vmmi.VMMDLL_PdbLoad(hVmm, pid, vaModuleBase, pb);
                if (result)
                {
                    szModuleName = Encoding.UTF8.GetString(data);
                    szModuleName = szModuleName.Substring(0, szModuleName.IndexOf((char)0));
                    _hVmm = hVmm;
                    Module = szModuleName;
                    return;
                }
            }
        }

        throw new VmmException("Failed to load PDB for module");
    }

    /// <summary>
    ///     ToString override.
    /// </summary>
    public override string ToString()
    {
        return $"VmmPdb:{Module}";
    }

    #endregion

    #region Specific Functionality

    /// <summary>
    ///     The module name of the PDB.
    /// </summary>
    public string Module { get; }

    /// <summary>
    ///     Get the symbol name given an address or offset.
    /// </summary>
    /// <param name="cbSymbolAddressOrOffset"></param>
    /// <param name="szSymbolName"></param>
    /// <returns></returns>
    public bool SymbolName(ulong cbSymbolAddressOrOffset, out string szSymbolName)
    {
        return SymbolName(cbSymbolAddressOrOffset, out szSymbolName, out _);
    }

    /// <summary>
    ///     Get the symbol name given an address or offset.
    /// </summary>
    /// <param name="cbSymbolAddressOrOffset"></param>
    /// <param name="szSymbolName"></param>
    /// <param name="pdwSymbolDisplacement"></param>
    /// <returns></returns>
    public unsafe bool SymbolName(ulong cbSymbolAddressOrOffset, out string szSymbolName, out uint pdwSymbolDisplacement)
    {
        szSymbolName = "";
        pdwSymbolDisplacement = 0;
        var data = new byte[260];
        fixed (byte* pb = data)
        {
            var result = Vmmi.VMMDLL_PdbSymbolName(_hVmm, Module, cbSymbolAddressOrOffset, pb, out pdwSymbolDisplacement);
            if (!result) return false;
            szSymbolName = Encoding.UTF8.GetString(data);
            szSymbolName = szSymbolName.Substring(0, szSymbolName.IndexOf((char)0));
        }

        return true;
    }

    /// <summary>
    ///     Get the symbol address given a symbol name.
    /// </summary>
    /// <param name="szSymbolName"></param>
    /// <param name="pvaSymbolAddress"></param>
    /// <returns></returns>
    public bool SymbolAddress(string szSymbolName, out ulong pvaSymbolAddress)
    {
        return Vmmi.VMMDLL_PdbSymbolAddress(_hVmm, Module, szSymbolName, out pvaSymbolAddress);
    }

    /// <summary>
    ///     Get the size of a type.
    /// </summary>
    /// <param name="szTypeName"></param>
    /// <param name="pcbTypeSize"></param>
    /// <returns></returns>
    public bool TypeSize(string szTypeName, out uint pcbTypeSize)
    {
        return Vmmi.VMMDLL_PdbTypeSize(_hVmm, Module, szTypeName, out pcbTypeSize);
    }

    /// <summary>
    ///     Get the child offset of a type.
    /// </summary>
    /// <param name="szTypeName"></param>
    /// <param name="wszTypeChildName"></param>
    /// <param name="pcbTypeChildOffset"></param>
    /// <returns></returns>
    public bool TypeChildOffset(string szTypeName, string wszTypeChildName, out uint pcbTypeChildOffset)
    {
        return Vmmi.VMMDLL_PdbTypeChildOffset(_hVmm, Module, szTypeName, wszTypeChildName, out pcbTypeChildOffset);
    }

    #endregion
}