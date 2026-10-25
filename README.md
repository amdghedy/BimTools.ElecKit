# ElecKit

**A native Revit electrical productivity toolkit** for drafting, wiring, model coordination, schedules, circuit checks, cable tray/conduit workflows, and data exchange.

ElecKit adds a dedicated **ElecKit** ribbon tab with searchable tools, task-based ribbon panels, configurable reporting, and local diagnostics. It is designed to work with multiple Revit API generations through a version-specific build.

> **Verification note:** A successful compile or unit-test run does not prove that every command behaves correctly in a live Revit model. Test commands on a disposable copy of your project before production use. See [the Revit validation checklist](docs/REVIT2020_VALIDATION_CHECKLIST.md).

## Contents

- [Feature overview](#feature-overview)
- [Complete tool and function catalogue](#complete-tool-and-function-catalogue)
- [Electrical circuit and panel workflows](#electrical-circuit-and-panel-workflows)
- [Schedules, quantities, and data exchange](#schedules-quantities-and-data-exchange)
- [Reliability and diagnostics](#reliability-and-diagnostics)
- [Requirements and compatibility](#requirements-and-compatibility)
- [Installation](#installation)
- [Build from source](#build-from-source)
- [CI, installer, and release workflow](#ci-installer-and-release-workflow)
- [Configuration and local files](#configuration-and-local-files)
- [Add a new tool](#add-a-new-tool)
- [Known limitations and safe-use notes](#known-limitations-and-safe-use-notes)
- [Repository map](#repository-map)

## Feature overview

| Area | What you can do |
|---|---|
| Drafting | Convert CAD/wires/lines, clean and split lines, fillet corners, create conduit from lines, connect runs |
| Wiring | Create home-run lines, build conduit runs from boundaries, write circuit/wire information, manage cable catalog data |
| Annotation | Place switch labels and tags, dimension elements, distribute family instances along a path |
| Model utilities | Align families, select hosts or matching types, assign worksets, place junction boxes, create sections and 3D views |
| Coordination | Run category-based clash screening and export model warnings |
| Electrical circuits | Preflight panel assignments, audit cumulative panel load, export circuit lists, find unassigned devices, select by circuit |
| Conduit and cable tray | Draw/route tray, build branches and risers, connect fittings, convert lines, report runs |
| Schedules and data | Export schedules to CSV/Excel, import values, round-trip supported schedule rows, manage shared parameters |
| Productivity | Search tools, manage views/sheets, inspect family types, configure options, inspect logs |
| Deployment | Build version-specific assemblies and run CI checks; package a selectable-version setup executable when all required jobs pass |

## Complete tool and function catalogue

The following catalogue describes the tools registered in the add-in and the additional workflows implemented in this repository. Availability of individual functions can depend on the active view, selected element types, family content, Revit version, and model parameters.

### 1. Tool discovery and settings

- **Tool Finder** — Search tool names/descriptions and launch a command from a single searchable list; recently used tools are prioritized.
- **Options** — Configure the report output folder, automatic report opening, and default fillet radius.
- **Open Log** — Open the local ElecKit log to investigate command failures and transaction diagnostics.
- **Cable Catalog Editor** — Review and edit the locally stored cable catalog.

### 2. Drafting and line conversion

- **CAD to Detail Lines** — Recreate lines and arcs from imported/linked CAD in the active view, with an optional layer filter.
- **Wires to Detail Lines** — Convert selected electrical wires, or wires in the view, into detail lines.
- **Detail Lines to Wires** — Convert connected straight detail-line chains into electrical wires.
- **Lines to Conduit** — Convert selected straight detail/model lines to conduit, with conduit type and height choices.
- **Connect Runs** — Pick elements in sequence and connect pairs with direct or L-shaped conduit runs and elbows.
- **Boundary to Conduits** — Build conduit runs from a detail-line path/boundary, adding elbows at corners and rolling back affected routes if required fittings fail.
- **Clean Lines** — Remove duplicate or fully overlapping detail lines of the same line style.
- **Split Lines** — Split straight detail lines where other lines cross or touch them.
- **Fillet Corners** — Round corners between straight detail lines in the selection or active view.
- **Fillet Two Lines** — Pick two detail lines and round the corner; repeat until cancelled.
- **Fillet Radius** — Set the default fillet radius in millimetres.
- **Lines to Cable Tray** — Convert supported straight detail/model line segments to cable tray, with per-segment failure reporting.

### 3. Wiring and electrical annotations

- **Home Run Lines** — Draw home-run detail lines from picked devices to a panel, using individual lines or a chain.
- **Circuit Number Text** — Write a fixed circuit number, each element's own circuit number, or a numbered sequence to a selected parameter.
- **Wire Info to Comments** — Write nearby device/wire circuit and wire-type information into conduit Comments.
- **Switch Labels** — Label lighting devices in pick order using alphabetic, numeric, or fixed-text schemes.
- **Tags with Leaders** — Place tags with leaders on selected elements using category-appropriate default tags.
- **Aligned Dimension** — Create an aligned dimension between supported picked elements such as family instances, grids, or reference planes.
- **Distribute Along Path** — Evenly distribute picked family instances along a supported line, arc, conduit, or tray path.

### 4. Model and view tools

- **Worksets by Type** — Move selected elements into worksets named by category or by family/type; create missing worksets when possible.
- **Select Hosts** — Select host elements for currently selected family instances and zoom to them.
- **Place Junction Box** — Place level-based electrical fixture/junction-box family instances at picked points and a specified height.
- **Align to Reference** — Align family instances to a reference instance by rotation, location, or a selected axis.
- **Section Along Element** — Create a section aligned along a supported straight conduit, cable tray, or line.
- **Select Same Type** — Select instances of the same type as a picked element in the active view.
- **3D from Selection** — Create a 3D view with a section box fitted around selected elements.
- **View Manager** — Search printable views and sheets, then activate the chosen view.
- **Sheet Creator** — Create a sheet and place the active view or schedule on it.
- **Family Type Manager** — Inspect available types and change the type of selected matching elements.

### 5. Coordination and model QA

- **Clash Check** — Screen selected elements or a chosen category against another category, select the involved elements, and export a CSV report.
- **Model Warnings** — Export model warnings to CSV with element IDs and summarize frequent warnings.
- **Deployment Audit** — Compare the add-in manifest's assembly path, expected deployed DLL, loaded assembly location, and file hashes.
- **Transaction diagnostics** — Log command cancellation/failure and transaction start, commit, rollback, end, elapsed time, and rollback failures.

### 6. Quantity takeoff and reporting

- **Quantity Takeoff** — Count selected elements or view contents by category, family, and type; include lengths for supported linear elements and export CSV.
- **Conduit Run Report** — Report conduit type, size, length, level, and comments, including totals grouped by size.
- **Cable Tray Summary View** — Create a drafting view summarizing cable trays by type and size.
- **Conduit Run Report / Circuit Report** — Export conduit-run information or circuit/panel/circuit-number/load-name/element-count data to CSV.
- **Circuit Report** — Export a CSV inventory of circuits and their panel, circuit number, load name, and element count.
- **Unassigned Devices** — Find electrical devices not assigned to a circuit, select them, and save a report.

### 7. Cable tray routing and fittings

- **Draw Cable Tray** — Draw cable tray between picked points at a selected height and size, with elbows at corners.
- **Route Cable Tray** — Create direct, L-shaped, ordered multi-segment, and optional obstacle-aware orthogonal routes.
- **Boundary to Cable Trays** — Create connected or closed cable-tray runs from line boundaries; roll back a run when a required elbow cannot be created.
- **Cable Tray Riser** — Create a vertical tray segment between vertically aligned points picked in a 3D view.
- **Cable Tray Branch Layout** — Create a guided perpendicular T-branch with a tee fitting.
- **Connect Cable Tray Fitting** — Connect supported tray runs using elbows, transitions/reducers, tees, and crosses, with connector proximity checks and rollback on Revit rejection.
- **Lines to Cable Tray** — Convert supported straight detail/model line segments to tray with per-segment rollback/reporting.
- **Tray Summary View** — Create a drafting-view summary grouped by tray type and size.

### 8. Electrical circuits, panels, and single-line reports

- **Assign Circuit to Panel** — Preflight known voltage, pole-count, and declared-capacity conflicts before assigning a circuit to a destination panel.
- **Circuit & Panel Audit** — Review cumulative panel loading; flag incomplete/unknown load data rather than silently assuming unknown loads are zero.
- **Panel Single-Line Report** — Generate a text-based single-line report for a selected panel, including available circuit, load, pole, and voltage information.
- **Circuit Report** — Export circuit details to CSV.
- **Select by Circuit** — Pick an element and select elements associated with the same circuit(s).
- **Unassigned Devices** — Identify electrical devices that are not on a circuit.
- **Panel SLD / Single-Line Diagram** — The current output is schematic/report-oriented, not a complete engineered electrical single-line diagram.

### 9. Schedule export, import, and round-tripping

- **Export All Schedules** — Export each model schedule to a separate CSV file.
- **Export Active Schedule** — Export the open schedule view to one CSV file.
- **Schedules to Excel** — Export the active schedule or all schedules to an Excel workbook, with one worksheet per schedule for bulk export.
- **Import Values by Mark** — Read a CSV and write values to elements matched by Mark, with a preview of proposed changes.
- **CSV to Text Note** — Place CSV table content in a view as a formatted text note.
- **Schedule Round-Trip Export/Import** — Export itemized rows from supported single-category schedules and import supported edited values back to elements.
- **Parameter data types** — The round-trip workflow supports typed CSV values and parameter-ID-qualified instance/type values, including supported ElementId parameters.
- **Shared-type conflict checks** — Detect conflicting edits that target the same shared type instead of silently applying incompatible values.

### 10. Shared parameters and family data

- **Shared Parameters** — Create common electrical shared parameters and bind them to supported electrical categories.
- **Family Type Manager** — List available types and change selected instances to the chosen type.
- **Import Values by Mark** — Apply CSV values to elements identified by Mark, with a change preview.
- **Schedule round-trip** — Exchange supported schedule parameter values while checking for shared-type edit conflicts.

### 11. Smart filtering and selection

- **Smart Filter** — Find elements in the active view using category, type name, and parameter values; then select or isolate matches.
- **Compound filter rules** — Combine up to 12 supported rules using AND/OR logic.
- **Select Hosts** — Find hosts for selected family instances.
- **Select Same Type** — Select matching element types in the active view.
- **Select by Circuit** — Select elements associated with the selected element's circuit(s).

## Electrical circuit and panel workflows

Circuit assignment validation is a conservative preflight check, not a substitute for engineering calculations. Where the model exposes the necessary data, checks include:

- Voltage compatibility.
- Pole-count compatibility.
- Declared panel capacity.
- Existing destination-panel apparent load plus the proposed circuit load.
- Unknown or incomplete existing load values flagged for review.
- Regression cases for projected overload, exact-capacity pass, and unknown existing load.

The single-line output is a schematic/reporting aid. It does **not** replace code-compliant design, protective-device coordination, phase balancing, short-circuit calculations, or review by a qualified electrical engineer.

## Schedules, quantities, and data exchange

- Quantity takeoff grouping and category-by-category audit output.
- CSV exports for schedules, circuits, model warnings, quantities, conduit runs, and supported reports.
- Excel workbook export for the active schedule or multiple schedules.
- CSV import by Mark with a preview before changes are applied.
- Itemized single-category schedule row exchange with typed values and supported instance/type parameters.
- CSV parsing supports quoted fields and multiline values in the round-trip workflow.
- Smart Filter supports compound AND/OR criteria for targeted selection/isolation.

Import and round-trip features should be tested against copies of schedules and models first. Revit schedules may contain calculated, read-only, combined, or otherwise unsupported fields; not every column is writable.

## Reliability and diagnostics

- Commands that modify the model use Revit transactions so successful operations can generally be undone with **Ctrl+Z**.
- Routing and fitting operations use localized rollback/reporting where implemented, so a failed required fitting should not leave a partial route silently behind.
- Transaction and command events are logged locally, including elapsed time and rollback failures.
- Deployment Audit helps identify manifest/DLL path mismatches and deployment hash differences.
- Debug deployment is conditional on a detected local Revit installation and verifies expected output files.
- Unit tests cover supported logic such as circuit validation, CSV parsing, orthogonal route planning, and quantity grouping.

## Requirements and compatibility

- Windows and a supported desktop installation of Autodesk Revit.
- The add-in must be built for the same Revit major version that will load it.
- Revit API references come from the local Revit installation when available; otherwise CI uses the matching Nice3point Revit API NuGet packages.
- The repository contains version-specific target settings:
  - Revit 2020: .NET Framework 4.7.
  - Revit 2021–2024: .NET Framework 4.8.
  - Revit 2025–2026: .NET 8 for Windows.
  - Revit 2027: .NET 10 for Windows.
- Some commands require suitable family content, electrical connectors, editable parameters, worksharing permissions, or an appropriate active view.

Compatibility targets describe the intended build configuration. Confirm the latest multi-version workflow is green before treating a release package as verified.

## Installation

### Option A — Setup executable

1. Download the setup executable from a successful GitHub Actions run's **ElecKit-Setup-EXE** artifact or from a published GitHub release.
2. Close all running Revit sessions.
3. Run the setup program and select the Revit versions you want to install.
4. Start the matching Revit version and look for the **ElecKit** ribbon tab.
5. If Revit displays an add-in trust prompt, review it and choose the appropriate option for your environment.

The installer is intended to install into the current Windows user's Revit Addins folders and does not require administrator rights. The executable is not code-signed, so Windows SmartScreen may show a warning.

### Silent installation

Run from a command prompt with the executable path adjusted to your download location:

```text
ElecKit-Setup.exe /install /quiet
ElecKit-Setup.exe /uninstall /quiet
```

The install command targets detected Revit versions. Validate the installer in a test environment before enterprise deployment.

### Option B — Manual per-user deployment

For Revit version `<version>`, the usual per-user add-in directory is:

```text
%APPDATA%\Autodesk\Revit\Addins\<version>\
```

Copy the version-matched `ElecKit.dll` and `ElecKit.addin` manifest to the correct locations. Ensure the manifest's assembly path resolves to the deployed DLL. Never deploy one version's assembly to a different Revit major version.

## Build from source

Install the .NET SDKs required by the target framework and use a Windows development environment. Revit API DLLs can be taken from a local installation; CI restores the version-matched API NuGet packages.

Example — build for Revit 2024:

```powershell
dotnet restore ElecKit.csproj -p:RevitVersion=2024
dotnet build ElecKit.csproj -c Release -p:RevitVersion=2024 --no-restore
```

Other supported version values are `2020`, `2021`, `2022`, `2023`, `2025`, `2026`, and `2027`.

Build output:

```text
bin\Release\R<version>\ElecKit.dll
```

A Debug build can copy build outputs to the matching per-user Revit Addins folder when that Revit installation is detected. Close Revit before rebuilding/deploying to avoid file-lock or load-state issues.

Version-specific API differences are isolated where possible using compile-time constants such as `REVIT2022_OR_GREATER` and `REVIT2024_OR_GREATER`, plus compatibility helpers in `src/Core/Compat.cs`.

## CI, installer, and release workflow

- `.github/workflows/build.yml` runs unit tests and builds supported Revit versions, then prepares the setup executable artifact when its required jobs succeed.
- `.github/workflows/build-all-versions.yml` provides a version matrix and installer packaging workflow.
- The setup source is in `EXE/ElecKit.iss`; installer project/source files are in `installer/`.
- Generated binaries are CI artifacts and are not intended to be committed into the repository.
- A version tag such as `v1.0.0` can trigger the release workflow configured in the repository.

Useful links:

- [Latest workflow runs](https://github.com/amdghedy/BimTools.ElecKit/actions)
- [Pull requests](https://github.com/amdghedy/BimTools.ElecKit/pulls)
- [Release downloads](https://github.com/amdghedy/BimTools.ElecKit/releases)
- [Revit 2020 integration validation checklist](docs/REVIT2020_VALIDATION_CHECKLIST.md)

**Release gate:** publish or distribute a setup executable only after the relevant workflow has completed successfully and the installer artifact has been verified. A compile-only result does not certify in-app behavior.

## Configuration and local files

| Purpose | Default location |
|---|---|
| Logs | `%LOCALAPPDATA%\ElecKit\log.txt` |
| Settings | `%APPDATA%\ElecKit\settings.json` |
| Per-user Revit add-ins | `%APPDATA%\Autodesk\Revit\Addins\<version>\` |
| Release installer source | `EXE/ElecKit.iss` |
| Installer project | `installer/ElecKit.Installer.csproj` |

Use **Options** to set the report folder and report auto-open preference. Use **Open Log** to inspect errors. The cable catalog is stored in the user's application data area.

## Add a new tool

1. Create a command class under `src/Commands/` deriving from `ElecCommand`.
2. Add the appropriate Revit `[Transaction(...)]` attribute.
3. Register the tool in `src/Core/ToolRegistry.cs`, including a stable ID, title, ribbon panel, tooltip, and command type.
4. Add or update unit tests for any logic that can be tested independently of Revit.
5. Build against the intended Revit version and test in a disposable model.
6. Update this README and the validation documentation when the user-facing behavior or limitations change.

The ribbon button, generated icon, and Tool Finder entry are created from the registry.

## Known limitations and safe-use notes

- **Live integration:** automated build/tests cannot replace opening the add-in in each supported Revit version and testing it against representative models.
- **Clash screening:** bounding-box screening is broad-phase detection. Positive-volume solid intersections are checked where possible, but this is not a guarantee of exact clearance for every geometry or linked-model scenario.
- **Obstacle-aware routing:** uses active-view bounding boxes, explicit clearance, a bounded obstacle count, and per-pair rollback. It is not exact-solid clearance or globally optimal automatic routing.
- **Cable-tray transitions:** preflight checks use readable width/height dimensions where available; Revit remains the final authority on connector compatibility.
- **Panel reports:** single-line output is schematic. It is not an engineered or code-compliance calculation.
- **Schedule import:** only supported writable parameters can be safely updated; review the preview and test on a copy.
- **Families and connectors:** placement/routing tools depend on suitable loaded families, connectors, levels, views, and model conditions.
- **Worksharing and permissions:** workset operations require a workshared model and appropriate permissions.
- **Unimplemented services:** no server-based **Update** or **Contact Us** command, licensing server, or remote update service is included.

## Repository map

| Path | Purpose |
|---|---|
| `src/Commands/` | Revit command implementations |
| `src/Core/` | Shared utilities, routing, validation, logging, registry, and compatibility code |
| `tests/ElecKit.Tests/` | Unit tests for Revit-independent logic |
| `docs/REVIT2020_VALIDATION_CHECKLIST.md` | Manual in-Revit validation cases and sign-off |
| `RevitVersions/` | Version-specific build output area / documentation |
| `OtherFiles/` | Supporting CI artifacts and documentation |
| `EXE/ElecKit.iss` | Selectable-version installer source |
| `installer/` | Installer application source |
| `.github/workflows/` | CI build, test, and packaging workflows |
| `ROADMAP.md` | Mapping of requested functions to tools |

## License and contributions

Review the repository's license and contribution guidance before redistributing or contributing. Report reproducible defects with the Revit version, command name, expected result, actual result, and relevant log excerpt (remove project-sensitive data first).
