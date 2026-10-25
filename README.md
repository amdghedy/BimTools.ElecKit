# ElecKit

A Revit electrical add-in, written from scratch: 49 tools in 12 ribbon panels. See [ROADMAP.md](ROADMAP.md) for the list.
Works with Revit 2020 to 2026 (one build per version).

## Install (users)
1. Download `ElecKit-Setup.exe` from the latest release (or from the **ElecKit-Setup** artifact of a successful Actions run).
2. Close Revit and run it. Tick the Revit versions to install for and click **Install**.
   It installs for your Windows user only and needs no administrator rights.
3. Start Revit. The **ElecKit** tab appears. If Revit asks whether to load the add-in, choose **Always Load**.

For scripted installs: `ElecKit-Setup.exe /install /quiet` (every Revit version found) and `ElecKit-Setup.exe /uninstall /quiet`.
The file is not code-signed, so Windows SmartScreen may ask for confirmation.

## Build (developers)
```
dotnet build ElecKit.csproj -c Release -p:RevitVersion=2024
```
Versions: 2020, 2021, 2022, 2023, 2024, 2025, 2026. The output is `bin\Release\R<version>\ElecKit.dll`.
With that Revit installed, the build references its DLLs; otherwise it uses the Revit API NuGet packages.
Debug builds copy the add-in into `%APPDATA%\Autodesk\Revit\Addins\<version>` (close Revit first).

The code differences between Revit versions are hidden behind compile-time switches (`REVIT2022_OR_GREATER`, `REVIT2024_OR_GREATER`) and the helpers in `src/Core/Compat.cs`.

## Continuous build and releases
`.github/workflows/build.yml` builds every Revit version, packs them into one `ElecKit-Setup.exe`, and uploads it as an artifact.
Push a tag such as `v1.0.0` and the same workflow publishes the setup file as a GitHub release.

## Add a tool
1. Create `src/Commands/MyToolCommand.cs`, derive from `ElecCommand`, add `[Transaction(...)]`.
2. Add one entry to `src/Core/ToolRegistry.cs`. The ribbon button, its icon and the Tool Finder entry appear automatically.

Logs: `%LOCALAPPDATA%\ElecKit\log.txt` - Settings: `%APPDATA%\ElecKit\settings.json`
