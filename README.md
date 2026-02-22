# OpenccFmmsegLib

[![NuGet](https://img.shields.io/nuget/v/OpenccFmmsegLib.svg)](https://www.nuget.org/packages/OpenccFmmsegLib/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/OpenccFmmsegLib.svg?label=downloads\&color=blue)](https://www.nuget.org/packages/OpenccFmmsegLib/)
[![License](https://img.shields.io/github/license/laisuk/OpenccFmmsegLib.svg)](https://github.com/laisuk/OpenccFmmsegLib/blob/master/LICENSE)

A .NET Standard 2.0 library providing a managed C# wrapper for the Rust-based
[opencc-fmmseg](https://github.com/laisuk/opencc-fmmseg) C API (OpenCC-compatible),
enabling high-performance Chinese text conversion (Simplified / Traditional)
in .NET applications.

This library focuses **only on OpenCC-style conversion**.
For Jieba segmentation and keyword extraction, use **OpenccJiebaLib** instead.

---

## Features

* OpenCC-compatible Chinese text conversion (Simplified / Traditional variants)
* Optional punctuation conversion
* Fast FMM-based segmentation engine under the hood (native Rust)
* Lightweight API with no hidden global state
* Safe native resource management via `IDisposable`

---

## Supported Conversion Configurations

`s2t`, `t2s`, `s2tw`, `tw2s`, `s2twp`, `tw2sp`, `s2hk`, `hk2s`,
`t2tw`, `t2twp`, `t2hk`, `tw2t`, `tw2tp`, `hk2t`, `t2jp`, `jp2t`

---

## Getting Started

### Prerequisites

* .NET Standard 2.0 or higher
  (.NET Framework, .NET Core / 5+ / 6+, Mono, Xamarin, etc.)
* .NET 6.0 or later recommended
* Native **`opencc_fmmseg_capi`** library available at runtime

---

## Installation

### Option 1 — From NuGet (recommended)

```sh
dotnet add package OpenccFmmsegLib
```

The NuGet package includes prebuilt native runtimes and deploys them under:

```
runtimes/<RID>/native/
```

**Shipped RIDs:**

* `win-x64`
* `linux-arm64`
* `linux-x64`
* `osx-x64`
* `osx-arm64`

No manual copying is required when using NuGet.

---

### Option 2 — Project Reference / Custom Native Builds

If you use a project reference or a custom native build, place the native
library using the same layout as NuGet:

```
runtimes/<RID>/native/
```

Expected filenames:

* Windows: `opencc_fmmseg_capi.dll`
* Linux: `libopencc_fmmseg_capi.so`
* macOS: `libopencc_fmmseg_capi.dylib`

The built-in native loader will discover the library automatically.

> 🧪 **Unit test projects** must also have access to the native library.
> Use the same layout under the test project output directory.

---

## Usage

```csharp
using OpenccFmmsegLib;

using var opencc = new OpenccFmmseg();

string input = "汉字转换测试";
string result = opencc.Convert(input, "s2t");

Console.WriteLine(result); // 漢字轉換測試

int code = opencc.ZhoCheck(input);
Console.WriteLine(code); // 2 (Simplified Chinese)
```

---

## Error Handling

* `InvalidOperationException` is thrown if initialization fails or a native error occurs
* `OpenccFmmseg.LastError()` returns the last native error message

---

## Public API Overview

### `OpenccFmmseg`

* `string Convert(string input, string config, bool punctuation = false)`
  Converts Chinese text using the specified OpenCC configuration.

* `string Convert(string input, OpenccConfig configId, bool punctuation = false)`
  Converts Chinese text using the OpenCC configuration Enum.

* `int ZhoCheck(string input)`
  Detects whether the input text is Simplified Chinese, Traditional Chinese, or non-Chinese.

* `static string LastError()`
  Returns the last error message reported by the native library.

* Implements `IDisposable` for deterministic native resource cleanup.

---

## Troubleshooting

### `DllNotFoundException` / `Unable to load shared library 'opencc_fmmseg_capi'`

* Ensure the native file exists under:

  ```
  runtimes/<RID>/native/
  ```
* Clean and rebuild the project after installing via NuGet
* Verify the correct file name for your platform

### `BadImageFormatException`

* Architecture mismatch (x64 vs x86)
* Ensure your application and native library target the same architecture

### Linux: library exists but cannot be loaded

* Place the `.so` next to the executable **or** set:

  ```bash
  export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:$(pwd)
  ```

### macOS: Gatekeeper / quarantine issues

```bash
xattr -dr com.apple.quarantine libopencc_fmmseg_capi.dylib
```

---

## Thread Safety

* Do not share a single `OpenccFmmseg` instance across threads
* Create one instance per thread or scope
* Dispose instances promptly (`using` is recommended)

---

## License

MIT License.
See [LICENSE](https://github.com/laisuk/OpenccFmmsegLib/blob/master/LICENSE).

---

## Acknowledgements

* [OpenCC](https://github.com/BYVoid/OpenCC)
* [opencc-fmmseg](https://github.com/laisuk/opencc-fmmseg)
