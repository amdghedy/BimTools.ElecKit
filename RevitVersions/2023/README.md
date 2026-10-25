# ElecKit for Revit 2023

Expected package contents:

- `ElecKit.dll` — compiled against the Revit 2023 API and the runtime supported by Revit 2023.
- `ElecKit.addin` — the manifest in this folder.

**Package status:** manifest scaffold only; the matching DLL must be produced by a version-specific build. Do not substitute a DLL compiled for another Revit release.

Install by copying the completed package into `%APPDATA%\\Autodesk\\Revit\\Addins\\2023\\` and confirming the manifest's `Assembly` path resolves to the DLL.
