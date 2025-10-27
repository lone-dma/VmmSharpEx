# VmmSharpEx

Custom Vmmsharp fork targeting bleeding edge .NET Core for Windows x64. Also includes the Native Libraries in the build process so you don't need to hunt them down (all files digitally signed).

```
VmmSharpEx_Tests
  Tests in group: 30
   Total Duration: 140 ms

Outcomes
   30 Passed
```

## Getting Started
[Get it on NuGet!](https://www.nuget.org/packages/VmmSharpEx)
```csharp
Install-Package VmmSharpEx
```
This library is **Windows Only**, and only bundles/targets the Windows x64 native libraries.

## Changelog
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

## License
```
/*  
 *  C# API wrapper 'vmmsharp' for MemProcFS 'vmm.dll' and LeechCore 'leechcore.dll' APIs.
 *  
 *  Please see the example project in vmmsharp_example for additional information.
 *  
 *  Please consult the C/C++ header files vmmdll.h and leechcore.h for information about parameters and API usage.
 *  
 *  (c) Ulf Frisk, 2020-2025
 *  Author: Ulf Frisk, pcileech@frizk.net
 *  
 */

/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/
```
