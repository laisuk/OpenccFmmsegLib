# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/).

---

## [1.5.0] - Unreleased

### Added

- Added Small Seal Script configurations `S2SEAL` (21), `T2SEAL` (22), `SEAL2S` (23), and `SEAL2T` (24),
  with case-insensitive parsing, canonical names, and native mapping/conversion regression coverage.
- Added instance methods `NormalizeCompat`, `NormalizeCompatExtended`, and `DeTofu`, backed by the native
  instance-based C API (v0.12.0 or later). Existing conversion behavior remains unchanged.
- Added `DeTofuLevel` with explicit native `uint32_t` values 0–7 for inclusive ExtB–ExtI thresholds.
- Added `shared pooled UTF-8 marshalling`, immediate thread-local error checks (including allocated native error strings),
  and guaranteed native-output/input-buffer cleanup for the new text operations.
- Added normalization golden cases, all DeTofu thresholds, Unicode preservation, disposal, repeated-call,
  custom dictionary compatibility, and native error ownership regression coverage.
- Added immutable custom dictionaries through `CustomDictSpec`, `DictSlot`, `CustomDictMode`, and a new
  `OpenccFmmseg(IEnumerable<CustomDictSpec>)` constructor backed by native `opencc_new_custom`.
- Exposed all 25 native slot IDs, including Seal slots requiring native v0.13.0 or later, supported by all bundled
  runtimes.
- Added explicit UTF-8 marshaling with pinned, count-based native arrays and cleanup on initialization failure.
- Added validation, initialization, Unicode, ABI layout, isolation, disposal, and conversion compatibility regression
  tests.

### Changed

- Expanded the encoded-config cache to cover all 24 managed/native configuration IDs.
- Updated all bundled native `opencc-fmmseg-capi` runtimes to v0.13.1: Windows x64, Linux x64/ARM64, and macOS
  x64/ARM64.
- Refactored the managed caller-buffer conversion path to use native `opencc_convert_cfg_mem_len` as the single
  explicit-length implementation, avoiding unnecessary NUL termination and native input-length scanning.
- Deprecated the legacy `TryConvertCfgToUtf8Into`, `ConvertCfgMemLenToUtf8Z`, and `ConvertCfgMemLen` managed APIs.
  Existing callers remain source-compatible through forwarding implementations while the simpler
  `TryConvertCfgToUtf8`, `ConvertCfgToUtf8Z`, and `ConvertCfg` APIs are preferred.
- Deprecated the `opencc_convert_cfg_mem` P/Invoke binding in favor of `opencc_convert_cfg_mem_len` for
  caller-provided buffer conversion.
- Simplified conversion tests to exercise the canonical managed APIs instead of native implementation details, while
  retaining dedicated compatibility coverage for deprecated forwarding APIs.
- Removed tests that depended on the former `ConvertCfgMemLen` stack-buffer and pooled-buffer implementation details.

---

## [1.4.0] - 2026-08-01

### Changed

- Updated native `opencc-fmmseg-capi` to v0.11.5.
- Expanded the encoded-config cache to cover all 20 native configuration IDs.
- Changed native last-error storage to per-calling-thread state, preventing concurrent calls from overwriting one
  another’s diagnostics and making parallel error-handling tests deterministic.
- Made all conversion overloads consistently throw `ObjectDisposedException` after disposal, including for null or empty
  input.
- Clarified caller-buffer API guidance: a too-small destination (including an empty size-query buffer) is an expected
  resize-and-retry result, while native failures should be followed immediately by `LastError()` on the same thread.

### Added

- Added `OpenccConfig.S2HKP` (`s2hkp`, ID 17) for phrase-aware Simplified-to-Hong Kong Traditional conversion.
- Added `OpenccConfig.HK2SP` (`hk2sp`, ID 18) for phrase-aware Hong Kong Traditional-to-Simplified conversion.
- Added `OpenccConfig.T2HKP` (`t2hkp`, ID 19) for phrase-aware Traditional-to-Hong Kong Traditional conversion.
- Added `OpenccConfig.HK2TP` (`hk2tp`, ID 20) for phrase-aware Hong Kong Traditional-to-Traditional conversion.
- Added case-insensitive parsing, canonical-name mappings, XML documentation, and round-trip tests for the new
  configurations.
- Added data-driven disposal regression coverage for all four `Convert` and `ConvertCfg` overloads.
- Re-enabled parallel execution for last-error tests under the native v0.11.5 per-calling-thread contract.

---

## [1.3.1] — 2026-04-22

### ✨ Added

- Added conversion APIs:
    - `ConvertCfgmemLen(...)` for optimized string conversion using the new buffer-based pipeline.
    - `ConvertCfgMemLenToUtf8Z(...)` for managed UTF-8 output (NUL-terminated).
- Added `TryConvertCfgToUtf8Into(...)` for caller-provided buffer scenarios (Span-based, allocation-free).
- Added fast UTF-8 decoding helpers in `Utf8Z`:
    - Pointer + length decoding (no NUL scan)
    - Span-based decoding helper (netstandard2.0-compatible)

### 🚀 Performance

- Introduced explicit-length conversion pipeline via native `opencc_convert_cfg_mem_len`:
    - Eliminates input/output NUL scanning
    - Reduces allocations and improves throughput
- Optimized end-to-end conversion path:
    - Avoids native string allocation
    - Avoids redundant buffer copies
    - Uses stackalloc fast path with ArrayPool fallback

### 🔄 Changed

- Refined API layering:
    - Clear separation between classic (`ConvertCfg`) and optimized (`MemLenXXX`) conversion paths
- Improved internal UTF-8 handling to reduce overhead in high-throughput scenarios
- Updated native `opencc-fmmseg-capi` to v0.9.2
- Tightened the public string-config conversion API: `Convert(string, string, ...)` now validates config names strictly
  and throws for invalid values instead of silently falling back to the default config
- Refined public XML documentation for release: clarified exception behavior, documented shared native `LastError()`
  semantics, and cleaned up IntelliSense output for advanced conversion APIs
- Refreshed README release-facing docs to match the actual public API and threading/error-reporting behavior

### 🧩 Compatibility

- Fully backward compatible with existing APIs
- Legacy `_mem`-based APIs retained for compatibility

---

## [1.3.0] — 2026-03-24

### ✨ Added

- Introduced the `OpenccConfig` enum as a strongly-typed conversion configuration.
- Added a new numeric-ID–based conversion API: `ConvertCfg(...)`, intended for advanced and interop scenarios.

### 🔄 Changed

- Refactored native interop code into a dedicated native wrapper class for improved separation of concerns.
- Updated the bundled **opencc-fmmseg C API** to **v0.9.1**.

### 🐞 Fixed

- Fixed a C# P/Invoke ABI mismatch by explicitly marshaling Rust `bool` parameters as `UnmanagedType.I1`
  (affecting both **OpenccFmmsegLib** and **OpenccJiebaLib**).

---

## [1.2.1] - 2025-10-28

### Added

- Added XML documentation for IDE intelliSense

---

## [1.2.0] - 2025-10-22

### Changed

- Update `opencc-fmmseg` C API to v0.8.3

## [1.1.0] - 2025-10-07

### Changed

- Update opencc-fmmseg natives to v0.8.2

---

## [1.0.0] – 2025-08-27

### Added

- First official Nuget release of `OpenccFmmsegLib`.
- High-performance OpenCC-style Chinese conversion using FMM segmentation.
- Support for:
    - Simplified ↔ Traditional (ST, TS)
    - Taiwan, Hong Kong, and Japanese variants
    - Phrase and character dictionaries
    - Punctuation conversion
- Utility for UTF-8 script detection (`zho_check`).
