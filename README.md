# ElecKit — Electrical Productivity Tools for Revit

A C# add-in focused on electrical drafting, wiring workflows, model selection, schedules, and circuit reporting in Autodesk Revit. ElecKit uses a central tool registry to create its ribbon buttons and keeps user settings and logs in your Windows profile.

> **Compatibility:** The current project targets **Revit 2020** (`net47`, x64). Do not assume it works with other Revit releases without a compatible build and testing in the target version.

## What it includes

The commands currently registered in `src/Core/ToolRegistry.cs` are grouped into these ribbon panels:

| Panel | Tools | What they help with |
|---|---|---|
| **Drafting** | CAD to Detail Lines, Wires to Detail Lines, Detail Lines to Wires, Connect Runs, Lines to Conduit, Fillet Radius, Fillet Two Lines, Fillet Corners, Split Lines, Clean Lines | Convert and clean drawing geometry, create conduit runs, and speed up repetitive line-editing tasks. |
| **Wiring** | Home Run Lines, Boundary to Conduits, Circuit Number Text, Wire Info to Comments, Cable Catalog | Create home-run graphics and conduit paths, write circuit-related information, and manage the cable catalog. |
| **Model** | Select Hosts, Select Same Type, 3D from Selection | Navigate host relationships, select matching instances, and create a 3D view around selected elements. |
| **Schedules** | Export All Schedules, Export Active Schedule | Export schedules to CSV for review and downstream workflows. |
| **Circuits** | Circuit Report, Select by Circuit | Export circuit summaries and select elements associated with a circuit. |

Tool behaviour can depend on the active view, selection, available Revit categories, and model data. Review the command tooltips and test on a sample model before using a tool in a production project.

## Requirements

- Windows and **Autodesk Revit 2020** for the current local build and runtime.
- Visual Studio 2022 or a compatible .NET SDK/MSBuild environment.
- Access to the Revit 2020 API assemblies for a local build, or network access to restore the Revit API NuGet packages for CI.

## Build locally

1. Close Revit before building if you plan to deploy the add-in to the Revit Addins folder.
2. Clone or download this repository.
3. Open `ElecKit.csproj` in Visual Studio, or run the following from the repository root:

   ```powershell
   dotnet build ElecKit.csproj -c Release
   ```

The project uses the Revit API assemblies from `C:\Program Files\Autodesk\Revit 2020\` when found. If they are not found there, the project falls back to the Revit 2020 API NuGet packages configured in the project file.

A **Debug** build attempts to copy the compiled files and `.addin` manifest to `%APPDATA%\Autodesk\Revit\Addins\2020`. The deployment target uses `ContinueOnError`, so a successful build alone does not guarantee deployment succeeded; check the destination and build output.

## Continuous integration

The GitHub Actions workflow in [`.github/workflows/build.yml`](.github/workflows/build.yml) builds on Windows for pushes and pull requests and uploads a `ElecKit-Release` artifact when the workflow succeeds.

- [View build workflow runs](../../actions/workflows/build.yml)
- [Build workflow definition](.github/workflows/build.yml)

CI compilation is not the same as an in-Revit integration test. Always validate the resulting add-in in the intended Revit installation and with a backed-up test model.

## Install the add-in

1. Build the project using the instructions above.
2. Confirm `ElecKit.dll` and `ElecKit.addin` are present in `%APPDATA%\Autodesk\Revit\Addins\2020` (and the referenced add-in files are in the expected subfolder).
3. Start Revit 2020 and look for the ElecKit ribbon tab.
4. If the tab does not appear, verify the manifest's assembly path, the DLL location, and the Revit journal or add-in loading messages.

The current README does not provide a prebuilt standalone `.exe`: ElecKit is a **Revit add-in assembly**, loaded by Revit, not a standalone Windows application.

## Add a command

1. Add a command class under `src/Commands/` and derive it from `ElecCommand`.
2. Apply the appropriate Revit transaction attribute and implement the command's execution logic.
3. Register the command in `src/Core/ToolRegistry.cs` using the existing `ToolInfo` pattern. The ribbon is generated from this registry.
4. Build and test in Revit 2020, including cancellation, invalid selections, and models with missing or unexpected data.

## Configuration and logs

| Item | Location |
|---|---|
| Log file | `%LOCALAPPDATA%\ElecKit\log.txt` |
| User settings | `%APPDATA%\ElecKit\settings.json` |
| Feature roadmap | [`ROADMAP.md`](ROADMAP.md) |

The roadmap describes the broader requested feature set and its status. It is not a guarantee that every listed feature is already implemented in the current build; the registered commands above are the best starting point for identifying what is currently exposed on the ribbon.

## Safety and project use

- Work on a copy of the model first and review changes before saving.
- Check exported CSV files before using them for quantity, circuit, or commercial reporting.
- Validate conduit geometry, circuit associations, and parameter updates against your project standards.
- Back up project data before running bulk operations.

## Contributing

Issues and pull requests are welcome. Include your Revit version, exact steps to reproduce, expected behaviour, actual behaviour, and any relevant log messages. Please avoid sharing confidential project models or sensitive project information.

## License

Check the repository's license file before redistributing or using this project under specific licensing terms. If no license file is present, do not assume that the code is offered under an open-source license.