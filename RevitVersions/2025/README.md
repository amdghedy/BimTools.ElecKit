# ElecKit for Revit 2025

Expected package contents:

- `ElecKit.dll` — compiled against the Revit 2025 API and the runtime supported by Revit 2025.
- `ElecKit.addin` — the manifest in this folder.

**Package status:** manifest scaffold only; the matching DLL must be produced by a version-specific build. Do not substitute a DLL compiled for another Revit release.

Install by copying the completed package into `%APPDATA%\\Autodesk\\Revit\\Addins\\2025\\` and confirming the manifest's `Assembly` path resolves to the DLL.
