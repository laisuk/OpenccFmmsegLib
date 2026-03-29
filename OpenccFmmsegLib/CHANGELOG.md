# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/).

---

## [1.3.1] — 2026-03-29

### ✨ Added

- Added high-performance conversion APIs:
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
