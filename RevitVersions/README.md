# Revit versioned deployment packages

This directory is the version-specific deployment layout for ElecKit.

## Versions

- [Revit 2020](./2020/)
- [Revit 2021](./2021/)
- [Revit 2022](./2022/)
- [Revit 2023](./2023/)
- [Revit 2024](./2024/)
- [Revit 2025](./2025/)
- [Revit 2026](./2026/)
- [Revit 2027](./2027/)

Each version folder contains an `ElecKit.addin` manifest configured to load `ElecKit.dll` from the same deployment folder. Copy the complete version folder's contents into the matching per-user Revit Addins folder, preserving the relative paths.

## Important build requirement

A manifest is not a compiled add-in. The repository's current project is configured for Revit 2020 / .NET Framework 4.7 only. Do **not** copy the Revit 2020 DLL into the 2021–2027 folders and treat it as compatible. Revit 2021–2024 generally use .NET Framework 4.8, Revit 2025–2026 use .NET 8 (with newer 2026 updates moving to .NET 10), and Revit 2027 uses .NET 10. Each release must be compiled against the matching Revit API and runtime, then tested in that Revit version.

The matching `ElecKit.dll` is intentionally not fabricated or committed as an empty/invalid binary. Build output must be generated from the source for each supported target before that version folder is a complete installable package.
