# OpenccFmmsegLib

[![NuGet](https://img.shields.io/nuget/v/OpenccFmmsegLib.svg)](https://www.nuget.org/packages/OpenccFmmsegLib/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/OpenccFmmsegLib.svg?label=downloads&color=blue)](https://www.nuget.org/packages/OpenccFmmsegLib/)
[![License](https://img.shields.io/github/license/laisuk/OpenccFmmsegLib.svg)](https://github.com/laisuk/OpenccFmmsegLib/blob/master/LICENSE)

A .NET Standard 2.0 library providing a managed C# wrapper for the Rust-based
[opencc-fmmseg](https://github.com/laisuk/opencc-fmmseg) C API (OpenCC-compatible), enabling high-performance Chinese
text conversion (Simplified / Traditional)
in .NET applications.

This library focuses **only on OpenCC-style conversion**. For Jieba segmentation and keyword extraction, use
**OpenccJiebaLib** instead.

---

## Features

* OpenCC-compatible Chinese text conversion (Simplified / Traditional variants)
* Optional punctuation conversion
* Fast FMM-based segmentation engine under the hood (native Rust)
* Lightweight API with explicit native error-reporting semantics
* Safe native resource management via `IDisposable`
* Immutable custom dictionaries with typed slots and append/override modes
* Optional native compatibility normalization and DeTofu display fallback

---

## Supported Conversion Configurations

`s2t`, `t2s`, `s2tw`, `tw2s`, `s2twp`, `tw2sp`, `s2hk`, `s2hkp`,
`hk2s`, `hk2sp`, `t2tw`, `t2twp`, `t2hk`, `t2hkp`, `tw2t`, `tw2tp`,
`hk2t`, `hk2tp`, `t2jp`, `jp2t`, `s2seal`, `t2seal`, `seal2s`, `seal2t`

The phrase-aware Hong Kong configurations `s2hkp`, `hk2sp`, `t2hkp`, and
`hk2tp` require native `opencc-fmmseg-capi` v0.11.5 or later.
All bundled native runtimes use `opencc-fmmseg-capi` v0.13.1.

The Small Seal Script configurations require native v0.13.0 or later and support both typed and
case-insensitive string APIs: `OpenccConfig.S2SEAL` (21), `T2SEAL` (22), `SEAL2S` (23), and `SEAL2T` (24).
For example:

```csharp
using var converter = new OpenccFmmseg();
string seal = converter.Convert("小篆", OpenccConfig.T2SEAL);
string traditional = converter.Convert(seal, "seal2t");
```

Displaying the resulting Small Seal Script characters requires a font that supports them.

---

## Getting Started

### Prerequisites

* .NET Standard 2.0 or higher (.NET Framework, .NET Core / 5+ / 6+, Mono, Xamarin, etc.)
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

If you use a project reference or a custom native build, place the native library using the same layout as NuGet:

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

## Custom Dictionaries

Create a converter with one or more in-memory dictionaries. All existing conversion methods
use the custom mappings; configuration remains selected per conversion call.

```csharp
using System.Collections.Generic;
using OpenccFmmsegLib;

var customDictionarySpecifications = new[]
{
    new CustomDictSpec(
        DictSlot.STPhrases,
        CustomDictMode.Append,
        new Dictionary<string, string>
        {
            ["汉字转换"] = "自訂轉換",
            ["自定义词语"] = "自訂詞語"
        }),
    new CustomDictSpec(
        DictSlot.TSPhrases,
        CustomDictMode.Append,
        new Dictionary<string, string> { ["自訂詞語"] = "自定义词语" })
};

using var converter = new OpenccFmmseg(customDictionarySpecifications);
string result = converter.Convert("汉字转换", "s2t"); // 自訂轉換
string reverse = converter.Convert("自訂詞語", OpenccConfig.T2S); // 自定义词语
```

`Append` merges entries into the selected built-in slot, replacing mappings for matching source keys.
`Override` clears that entire slot before adding the supplied entries; other slots retain their mappings.
For example, overriding `STCharacters` with only `汉 → 訂` makes `汉龙` convert to `訂龙` under `s2t`.
Use append when you want to preserve the remaining built-in mappings.

Select a slot used by your conversion pipeline: `STCharacters`/`STPhrases` for `s2t`,
`TSCharacters`/`TSPhrases` for `t2s`, or the appropriate Taiwan, Hong Kong, Japanese, or punctuation slot
for regional conversions. Punctuation mappings require `punctuation: true`.
Slot IDs are explicit native identifiers, separate from `OpenccConfig` IDs.

Specifications and their entries are applied in input order. Later duplicate keys win; a later override
clears mappings added earlier to that slot. An ordered `IEnumerable<KeyValuePair<string, string>>`
can express duplicate keys. `CustomDictSpec` copies entries into a read-only snapshot. The converter
copies the dictionary data into native ownership during construction, so caller collection changes
and later garbage collection cannot affect it. Dictionaries cannot be mutated after initialization,
and separate converter instances have independent custom mappings.

An empty specification collection is equivalent to the default constructor. An empty entry collection
leaves an appended slot unchanged or clears an overridden slot. Null collections, null specifications,
null source/target strings, embedded NUL characters, invalid UTF-16, and undefined slot/mode values
are rejected. Entries are literal mappings, without file-format parsing or target-token splitting.
Empty source/target strings are forwarded according to native dictionary semantics.

Custom construction requires native `opencc_new_custom` (v0.11.5 or later).
All 25 slots in the checked-in header are exposed and supported by every bundled v0.13.1 runtime:
Windows x64, Linux x64/ARM64, and macOS x64/ARM64. The four `Seal*` slots require native v0.13.0 or
later. If you supply an older custom native library, requesting an unsupported slot throws
`InvalidOperationException` with the native error. Both typed and string conversion APIs support all 24
configurations, including Seal configuration IDs 21–24.

Temporary UTF-8 buffers are pinned only during initialization and released on success or failure.
Custom construction adds no work to conversion hot paths and introduces no new dependencies.

---

## Compatibility Normalization and DeTofu

These optional operations require native v0.12.0 or later, supported by all bundled v0.13.1 runtimes.
They are **instance methods** because each native C function requires a live OpenCC instance.
The recommended processing order is normalization, Chinese conversion, then display fallback:

```csharp
using OpenccFmmsegLib;

string input = "金庸小說";
using var converter = new OpenccFmmseg();

string normalized = converter.NormalizeCompatExtended(input);
string converted = converter.Convert(normalized, "s2t");
string result = converter.DeTofu(converted, DeTofuLevel.ExtB);
```

`NormalizeCompat(text)` normalizes mapped CJK Compatibility Ideographs, including supplementary-plane
characters: `金庸` becomes `金庸`, and `天龍八部書` becomes `天龍八部書`.
`NormalizeCompatExtended(text)` additionally applies the native curated Unicode compatibility mappings;
for example, `聼竒羙䂖甁噐` becomes `聽奇美石瓶器`, while the basic method leaves that text unchanged.
These methods use the native tables and are not general-purpose NFC, NFD, NFKC, or NFKD normalization.
Characters without applicable mappings remain unchanged.

`DeTofu(text, level)` uses the native built-in fallback table to improve display compatibility when a font
or reader cannot render rare CJK characters. For example, `骖𬴂` becomes `骖騑` at `DeTofuLevel.ExtB`.
It is a display fallback, not a linguistic conversion, and leaves characters without applicable mappings
unchanged, including unmapped supplementary characters. It does not insert question marks or replacement glyphs.

| Level  | Native value | Eligible mappings (inclusive) |
|--------|--------------|-------------------------------|
| `ExtB` | 0            | Extension B through I         |
| `ExtC` | 1            | Extension C through I         |
| `ExtD` | 2            | Extension D through I         |
| `ExtE` | 3            | Extension E through I         |
| `ExtF` | 4            | Extension F through I         |
| `ExtG` | 5            | Extension G through I         |
| `ExtH` | 6            | Extension H through I         |
| `ExtI` | 7            | Extension I only              |

The selected threshold determines eligibility; replacement still requires an entry in the built-in table.
`ExtB` is the broadest threshold. There is no disabled level; omit the DeTofu call to disable fallback.

All three methods return an empty string for null or empty input on a live converter. Undefined DeTofu
levels throw `ArgumentOutOfRangeException`, including for empty input. Embedded NUL characters are rejected
with `ArgumentException` to prevent native string truncation. Calls after disposal throw `ObjectDisposedException`,
including for null or empty input. Native failures throw `InvalidOperationException` with the native diagnostic;
allocated native output and rented input buffers are released on both success and failure.

Normalization and DeTofu are explicitly opt-in. Normal `Convert()` behavior and custom dictionary configuration
remain unchanged; none of these operations runs automatically during conversion.

---

## Error Handling

* `InvalidOperationException` is thrown if initialization fails or a native error occurs.
* `ObjectDisposedException` is thrown by instance APIs after disposal, including for null or empty input.
* `OpenccFmmseg.LastError()` returns the last native error for the calling thread. Read it immediately after the failing
  native call, on the same thread, before an `await`
  or any other native OpenCC call on that thread.

---

## Public API Overview

### `OpenccFmmseg`

* `OpenccFmmseg()`
  Creates a converter using the built-in dictionaries.

* `OpenccFmmseg(IEnumerable<CustomDictSpec> customDictionarySpecifications)`
  Creates an immutable converter with ordered custom dictionary specifications.
  See [Custom Dictionaries](#custom-dictionaries) for merge behavior and native requirements.

* `string NormalizeCompat(string text)`
  Normalizes mapped CJK Compatibility Ideographs using the native table.

* `string NormalizeCompatExtended(string text)`
  Adds the native curated Unicode compatibility mappings to basic normalization.

* `string DeTofu(string text, DeTofuLevel level)`
  Applies optional native display fallback at an inclusive CJK extension threshold.

* `string Convert(string input, string config, bool punctuation = false)`
  Converts Chinese text using the specified canonical OpenCC configuration name and throws for invalid names.

* `string Convert(string input, OpenccConfig configId, bool punctuation = false)`
  Converts Chinese text using the OpenCC configuration Enum.

* `string ConvertCfg(string input, OpenccConfig configId, bool punctuation = false)`
  Converts Chinese text using the numeric-config native path with a typed config enum.

* `string ConvertCfg(string input, int configNum, bool punctuation = false)`
  Advanced interop overload using a numeric configuration ID. Validation is delegated to the native library;
  invalid IDs return native error text and set `LastError()`. Hidden from IntelliSense.

* `string ConvertCfgMemLen(string input, int configId, bool punctuation = false)`
  Uses the specialized explicit-length native buffer pipeline. Prefer `Convert()` or `ConvertCfg()`
  for ordinary conversion; this API's size-query and output passes add conversion overhead.

* `byte[] ConvertCfgToUtf8Z(string input, int configId, bool punctuation = false)`
  Returns a newly allocated UTF-8 byte array including a trailing NUL terminator for interop scenarios.
  Uses the explicit-length native size-query and output passes; null or empty input returns a single NUL byte.

*
`bool TryConvertCfgToUtf8(string input, int configId, bool punctuation, Span<byte> destination, out int requiredBytes)`
Writes NUL-terminated UTF-8 output into a caller-provided buffer using the explicit-length native API.
`requiredBytes` includes the trailing NUL byte, including for null or empty input (one byte).

* `byte[] ConvertCfgMemLenToUtf8Z(string input, int configId, bool punctuation = false)`
  Obsolete alias for `ConvertCfgToUtf8Z()`; hidden from IntelliSense.

*
`bool TryConvertCfgToUtf8Into(string input, int configId, bool punctuation, Span<byte> destination, out int requiredBytes)`
Obsolete alias for `TryConvertCfgToUtf8()`; hidden from IntelliSense.

For `TryConvertCfgToUtf8()` and its legacy alias, `false` with `requiredBytes > destination.Length`
means the buffer is too small; resize and retry. This is the expected result for an empty-buffer size query and is not a
native error. For other `false` results, retrieve
`LastError()` immediately on the same thread.

* `int ZhoCheck(string input)`
  Detects non-Chinese (0), Traditional Chinese (1), or Simplified Chinese (2) text.

* `static int GetNativeAbiNumber()`
  Returns the native binary interface version for compatibility diagnostics.

* `static string GetNativeVersionString()`
  Returns the native library's semantic version string for logging and diagnostics.

* `static bool ConfigNameToIdNative(string name, out OpenccConfig configId)`
  Attempts a case-insensitive configuration-name lookup using the native library.

* `static bool ConfigIdToNameNative(OpenccConfig configId, out string name)`
  Attempts to retrieve a canonical lowercase configuration name using the native library.

The native configuration helpers are intended for interop/debugging and do not set the native last-error state.
Prefer the managed `OpenccConfigExtensions` helpers for ordinary configuration parsing and formatting.

* `static string LastError()`
  Returns the last error message reported by the native library for the calling thread.

* `void Dispose()`
  Implements `IDisposable` for deterministic native resource cleanup; repeated calls are safe.

### `OpenccConfigExtensions`

These helpers are pure managed code and do not require loading the native library.
Configuration names are case-insensitive and otherwise must match a supported canonical name.

* `static OpenccConfig DefaultConfig()`
  Returns the managed default, `OpenccConfig.S2T`.

* `static bool TryParseConfig(string name, out OpenccConfig configId)`
  Returns whether a name is supported; on failure, sets `configId` to `default`.

* `static OpenccConfig Parse(string name)`
  Parses a supported name; throws `ArgumentNullException` for null or `ArgumentException` for an unsupported name.

* `static bool IsValidConfig(string name)`
  Tests whether a name is supported without throwing.

* `static bool TryGetConfigName(OpenccConfig configId, out string name)`
  Returns a canonical lowercase name, or `false` with an empty string for an undefined ID.

* `static string ToCanonicalName(this OpenccConfig config)`
  Extension method returning a canonical lowercase name; throws `ArgumentOutOfRangeException` for an undefined ID.

### Custom dictionary specifications

* `CustomDictSpec(DictSlot slot, CustomDictMode mode, IEnumerable<KeyValuePair<string, string>> entries)`
  Copies ordered mappings into an immutable specification, validating the slot, mode, and entries.

* `DictSlot Slot { get; }`, `CustomDictMode Mode { get; }`
  Expose the selected dictionary slot and merge mode.

* `IReadOnlyList<KeyValuePair<string, string>> Entries { get; }`
  Exposes the read-only snapshot of mappings in input order.

### Public enums

* `OpenccConfig`
  Defines all 24 numeric conversion IDs, including `S2SEAL`, `T2SEAL`, `SEAL2S`, and `SEAL2T`.
  See [Supported Conversion Configurations](#supported-conversion-configurations).

* `DictSlot : uint`
  Defines all 25 native dictionary slots, including `SealCharacters`, `SealCharactersRev`,
  `SealVariants`, and `SealVariantsRev`. See [Custom Dictionaries](#custom-dictionaries).

* `CustomDictMode : uint`
  Defines `Append` (1) to merge entries and `Override` (2) to clear a slot before applying entries.

* `DeTofuLevel : uint`
  Defines inclusive display-fallback thresholds from `ExtB` (0) through `ExtI` (7).
  See [Compatibility Normalization and DeTofu](#compatibility-normalization-and-detofu).

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

MIT License. See [LICENSE](https://github.com/laisuk/OpenccFmmsegLib/blob/master/LICENSE).

---

## Acknowledgements

* [OpenCC](https://github.com/BYVoid/OpenCC)
* [opencc-fmmseg](https://github.com/laisuk/opencc-fmmseg)


