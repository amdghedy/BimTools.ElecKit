# ElecKit for Revit 2021

Expected package contents:

- `ElecKit.dll` — compiled against the Revit 2021 API and the runtime supported by Revit 2021.
- `ElecKit.addin` — the manifest in this folder.

**Package status:** manifest scaffold only; the matching DLL must be produced by a version-specific build. Do not substitute a DLL compiled for another Revit release.

Install by copying the completed package into `%APPDATA%\\Autodesk\\Revit\\Addins\\2021\\` and confirming the manifest's `Assembly` path resolves to the DLL.
