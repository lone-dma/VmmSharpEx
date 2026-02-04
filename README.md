# VmmSharpEx

Custom Vmmsharp fork based on [Ulf Frisk's MemProcFS](https://github.com/ufrisk/MemProcFS) targeting bleeding edge .NET Core for Windows x64. Also includes the Native Libraries in the build process so you don't need to hunt them down (all files digitally signed).

## Getting Started
[Get it on NuGet!](https://www.nuget.org/packages/VmmSharpEx)
```pwsh
Install-Package VmmSharpEx
```
This library is **Windows Only**, and only bundles/targets the Windows x64 native libraries.

## Changelog
- Version 3.200
  - Bump MemProcFS to 5.16.13 (security update)
  - Minor changes to various Scatter APIs.
  - General optimizations and improvements.
  - Fixed some major AOT issues with unmanaged callbacks/delegates (VmmSearch,etc.)
- Version 3.160
  - Bump MemProcFS to 5.16.12 (supports Chinese paths)
  - Optimizations and stability improvements to Scatter API. Some minor breaking changes.
- Version 3.150
  - Added new APIs: VMMDLL_MemReadPage, VMMDLL_WinGetThunkInfoIATW
  - Refactor VmmSearch to a static/stateless API. Provides better resource cleanup guarantees than the former object-based model. **This is a breaking change.**
- Version 3.140
  - Bump MemProcFS to 5.16.11 (fixes rare buffer overflow/access violation in Scatter API)
  - Improve finalizer safety
  - #nullable support
- Version 3.130
  - Optimized lots of methods and implementations.
  - Some breaking changes with a few renames and changed Map_Get return values to return NULL on failure instead of an empty array.
  - Changed many methods to accept Span<byte>.
- Version 3.120
  - Bump MemProcFS to 5.16.9 (fixes rare TLP bug on PCIe x1)
  - Extra AOT Support
- Version 3.110
  - Reworked ReadArray APIs to have a clear distinction between Array (non-pooled) and Pooled (using a backing IMemoryOwner).
  - Added new extension methods and cleaned up API.
- Version 3.100
  - Added new VmmInputManager extension class that checks for User Input on the Target System (Win11 Only).
- Version 3.92
  - Optimized Vmm MemCallback functionality.
  - Code/API Cleanup.
  - Scatter V2 API has been removed.
- Version 3.91
  - Updated MemProcFS to 5.16.7 for additional logging interop.
- Version 3.90
  - Updated versioning to utilize dotnet/Nerdbank.GitVersioning
- Version 3.80
  - Updated MemProcFS to 5.16.3
  - New Vmm Refresh Options
- Version 3.70
  - Expanded VmmScatter functionality and introduced VmmScatterMap.
  - Refactored Scatter API namespaces slightly for better organization.
  - V2 API (ScatterReadMap) will be deprecated in future releases. See [this discussion](https://github.com/lone-dma/VmmSharpEx/discussions/4).
- Version 3.60
  - Updated MemProcFS to 5.16.0 (Support for Windows 11 25H2)
- Version 3.50
  - Updated MemProcFS to 5.15.8
- Version 3.0
  - Added .NET 10 Support
- Initial Release
  - .NET 9
  - MemProcFS 5.15.3