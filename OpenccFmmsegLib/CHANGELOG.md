# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/).

---

## [1.2.2] — 2026-01-10

### ✨ Added
- Introduced the `OpenccConfig` enum as a strongly-typed conversion configuration.
- Added a new numeric-ID–based conversion API: `ConvertCfg(...)`, intended for advanced and interop scenarios.

### 🔄 Changed
- Refactored native interop code into a dedicated native wrapper class for improved separation of concerns.
- Updated the bundled **opencc-fmmseg C API** to **v0.8.4**.

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
