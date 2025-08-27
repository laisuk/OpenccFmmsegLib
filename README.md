# OpenccFmmsegLib

[![NuGet](https://img.shields.io/nuget/v/OpenccFmmsegLib.svg)](https://www.nuget.org/packages/OpenccFmmsegLib/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/OpenccFmmsegLib.svg?label=downloads&color=blue)](https://www.nuget.org/packages/OpenccFmmsegLib/)
[![License](https://img.shields.io/github/license/laisuk/OpenccFmmsegLib.svg)](https://github.com/laisuk/OpenccFmmsegLib/blob/master/LICENSE)

A .NET class library providing a managed wrapper for the [OpenCC](https://github.com/BYVoid/OpenCC) +
high-performance [opencc-fmmseg](https://github.com/laisuk/opencc-fmmseg) Rust C API, enabling Chinese text conversion (
Simplified/Traditional) in C# applications.

## Features

- Convert Chinese text between Simplified, Traditional, and other variants using OpenCC.
- Optional punctuation conversion.
- Efficient buffer management for high performance.
- Check if a string is Chinese (zh-Hans / zh-Hant) using OpenCC’s language check.
- Safe resource management and error reporting.

## Supported Conversion Configurations

`s2t`, `t2s`, `s2tw`, `tw2s`, `s2twp`, `tw2sp`, `s2hk`, `hk2s`, `t2tw`,  
`t2twp`, `t2hk`, `tw2t`, `tw2tp`, `hk2t`, `t2jp`, `jp2t`

## Getting Started

### Prerequisites

- .NET Standard 2.0 or higher (.NET Framework, .NET Core/5+/6+, Mono, Xamarin).
- .NET 6.0 or later recommended.
- Native **`opencc_fmmseg_capi`** library (must be available to the runtime).

### Installation

#### Option 1 — As Project Reference

- Add a project reference to **OpenccFmmsegLib** in your solution.
- **Manually copy** the native binary to your app’s output directory (`bin/<Config>/<TFM>`):
    - Windows: `opencc_fmmseg_capi.dll`
    - Linux: `libopencc_fmmseg_capi.so`
    - macOS: `libopencc_fmmseg_capi.dylib`
- Alternative: mark the native file **Copy to Output Directory: Copy always/if newer**.

> 🧪 **Unit tests (MSTest/xUnit/nUnit)**  
> Test projects don’t automatically copy natives from referenced projects. Either:
> - Put natives in the test project and set **Copy to Output Directory**, or
> - Add a small `Target` to copy them after build:
    >   ```xml
    > <Project Sdk="Microsoft.NET.Sdk">
    > <PropertyGroup>
    > <TargetFramework>net8.0</TargetFramework>
    > </PropertyGroup>
    > <ItemGroup>
    > <!-- Place natives under ./natives/<RID>/ -->
    > <None Include="natives\**\*.*">
    > <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    > </None>
    > </ItemGroup>
    > <Target Name="CopyNatives" AfterTargets="Build">
    > <ItemGroup>
    > <NativeFiles Include="natives\**\*.dll;natives\**\*.so;natives\**\*.dylib" />
    > </ItemGroup>
    > <Copy SourceFiles="@(NativeFiles)" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />
    > </Target>
    > <ItemGroup>
    > <ProjectReference Include="..\OpenccFmmsegLib\OpenccFmmsegLib.csproj" />
    > </ItemGroup>
    > </Project>
    >   ```

#### Option 2 — From NuGet

- Install via NuGet:
  ```sh
  dotnet add package OpenccFmmsegLib
  ```
- The package contains platform-specific native runtimes and **automatically** deploys them to the output directory. *
  *No manual copy required.**

### Usage

```csharp
using OpenccFmmsegLib;

using var opencc = new OpenccFmmseg();

string input = "汉字转换测试";
string result = opencc.Convert(input, "s2t"); // Simplified → Traditional
Console.WriteLine(result); // 漢字轉換測試

int isChinese = opencc.ZhoCheck(input); // Language check
Console.WriteLine(isChinese); // 2
```

### Error Handling

If initialization fails or a native error occurs, an `InvalidOperationException` is thrown.  
Use `OpenccFmmseg.LastError()` to retrieve the last error message from the native library.

## API Reference

### `OpenccFmmseg` Class

- `string Convert(string input, string config, bool punctuation = false)`  
  Converts Chinese text using the specified configuration.
- `int ZhoCheck(string input)`  
  Checks if the input is Chinese text.
- `static string LastError()`  
  Gets the last error message from the native library.
- Implements `IDisposable` for safe resource cleanup.

## Troubleshooting

### 1) `DllNotFoundException` or `Unable to load shared library 'opencc_fmmseg_capi'`

- **Cause:** Native library not found at runtime.
- **Fix:**
    - If using **Project Reference**: ensure the correct native file is present in your app’s output folder (`bin/...`).
      See *Installation → Option 1*.
    - If using **NuGet**: confirm your project targets a supported RID/TFM; clean + rebuild. The package should copy
      natives automatically.
    - Verify the file name and extension for your OS (see table below).

| OS      | Expected File Name            | Note                                               |
|---------|-------------------------------|----------------------------------------------------|
| Windows | `opencc_fmmseg_capi.dll`      | Must match app bitness (x64 vs x86).               |
| Linux   | `libopencc_fmmseg_capi.so`    | Ensure executable has permission to read the file. |
| macOS   | `libopencc_fmmseg_capi.dylib` | See Gatekeeper/quarantine notes below.             |

### 2) `BadImageFormatException`

- **Cause:** Architecture mismatch (e.g., x64 app loading x86 native).
- **Fix:** Match architectures. Build your app and native for the same target (typically **x64**).

### 3) Works on dev machine, fails on CI/other PC

- **Project Reference:** you likely forgot to ship the native file. Include it next to your `.exe`/`.dll`, or add a *
  *copy step** (see MSTest snippet above).
- **NuGet:** ensure the **Runtime Identifier (RID)** is set when publishing self-contained:
  ```sh
  dotnet publish -c Release -r win-x64 --self-contained false
  ```
  (RID examples: `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`.)

### 4) Linux cannot find the library even though it’s in the folder

- **Cause:** Loader search path.
- **Fix options:**
    - Place the `.so` **next to the app**.
    - Or set `LD_LIBRARY_PATH` to include the directory:
      ```bash
      export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:$(pwd)
      ```
    - Or use an *rpath* when publishing/packaging (advanced).

### 5) macOS “cannot be opened because the developer cannot be verified”

- **Cause:** Gatekeeper quarantine on downloaded binaries.
- **Fix:** Remove quarantine flag or codesign (for distribution):
  ```bash
  xattr -dr com.apple.quarantine libopencc_fmmseg_capi.dylib
  ```
  Ensure the `.dylib` sits **next to** your app if not using NuGet.

### 6) `EntryPointNotFoundException`

- **Cause:** Version mismatch between managed P/Invoke signatures and the native binary.
- **Fix:** Make sure the native DLL and the managed library are from the **same release**.

### 7) Access violations / random crashes under heavy load

- Ensure each `OpenccFmmseg` instance is used from the thread that created it, or create separate instances per thread.
- If using dependency injection, prefer **transient** or **scoped** lifecycle unless you’re certain the native layer is
  fully multi-thread safe.

> ✅ **Tip:** NuGet is the simplest path for correct native deployment. Use **Project Reference + manual copy** only when
> you know you need a custom build or local native debugging.

## License

This project is licensed under the MIT License.  
See [LICENSE](https://github.com/laisuk/OpenccFmmsegLib/blob/master/LICENSE) for details.

## Acknowledgements

- [OpenCC](https://github.com/BYVoid/OpenCC)
- [opencc-fmmseg](https://github.com/laisuk/opencc-fmmseg)
