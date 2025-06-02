# OpenccFmmsegLib

A .NET class library providing a managed wrapper for the [OpenCC](https://github.com/BYVoid/OpenCC) + [opencc-fmmseg](https://github.com/laisuk/opencc-fmmseg) C API, enabling Chinese text conversion (Simplified/Traditional) in C# applications.

## Features

- Convert Chinese text between Simplified, Traditional, and other variants using OpenCC.
- Optional punctuation conversion.
- Efficient buffer management for high performance.
- Check if a string is Chinese (zh-Hans / zh-Hant) using OpenCC's language check.
- Safe resource management and error reporting.

## Supported Conversion Configurations

- `s2t`, `t2s`, `s2tw`, `tw2s`, `s2twp`, `tw2sp`, `s2hk`, `hk2s`, `t2tw`, `t2twp`, `t2hk`, `tw2t`, `tw2tp`, `hk2t`, `t2jp`, `jp2t`

## Getting Started

### Prerequisites

- .NET 6.0 or later
- .NET Standard 2.0 or higher (.NET Core, .NET Framework, Mono, Xamarin, etc.)
- Native `opencc_fmmseg_capi` library (must be available in your system path or application directory)

### Installation

Add a reference to the compiled DLL (OpenccFmmsegLib) or include the project in your solution.

### Usage

```csharp
using OpenccFmmsegLib;

using var opencc = new OpenccFmmseg();

string input = "汉字转换测试";
string result = opencc.Convert(input, "s2t"); // Simplified to Traditional

Console.WriteLine(result); // output: 漢字轉換測試

int isChinese = opencc.ZhoCheck(input); // Language check
Console.WriteLine(isChinese); // output: 2
```

### Error Handling

If initialization fails or a native error occurs, an `InvalidOperationException` is thrown. Use `OpenccFmmseg.LastError()` to retrieve the last error message from the native library.

## API Reference

### `OpenccFmmseg` Class

- `string Convert(string input, string config, bool punctuation = false)`
  - Converts Chinese text using the specified configuration.
- `int ZhoCheck(string input)`
  - Checks if the input is Chinese text.
- `static string LastError()`
  - Gets the last error message from the native library.
- Implements `IDisposable` for resource management.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE.txt) for details.

## Acknowledgements

- [OpenCC](https://github.com/BYVoid/OpenCC)
- [opencc-fmmseg](https://github.com/laisuk/opencc-fmmseg)
