# VmmSharpEx

Custom Vmmsharp fork targeting bleeding edge .NET Core for Windows x64. Also includes the Native Libraries in the build process so you don't need to hunt them down (all files digitally signed).

```
VmmSharpEx_Tests
  Tests in group: 26
   Total Duration: 120 ms

Outcomes
   26 Passed
```

## Getting Started
[Get it on NuGet!](https://www.nuget.org/packages/VmmSharpEx)
```csharp
Install-Package VmmSharpEx
```
This library is **Windows Only** so make sure your solution is targeting a Windows TFM like `net9.0-windows`, etc.

## Changelog
- Version 3.50
  - Updated Windows SDK to 10.0.26100.4948
  - Updated MemProcFS to 5.15.8
- Version 3.0
  - Added .NET 10 Support
- Initial Release
  - .NET 9
  - MemProcFS 5.15.3

## License
```
Changes documented in git.
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
