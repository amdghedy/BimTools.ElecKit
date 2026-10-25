# ElecKit

**Electrical BIM productivity toolkit** — a C# add-in for Revit 2020, organized around drafting, wiring, annotation, model utilities, analysis, schedules, panels, cable tray, and electrical data.

ElecKit exposes its commands through a configurable ribbon registry. The descriptions below document the tools registered in the repository; availability in the ribbon does not, by itself, guarantee every workflow has been verified in a live project. Validate model-changing commands on a backup copy before production use.

## Contents

- [Highlights](#highlights)
- [Complete tool catalogue](#complete-tool-catalogue)
- [Requirements](#requirements)
- [Build](#build)
- [Install](#install)
- [Configuration, logs, and reports](#configuration-logs-and-reports)
- [Development and tests](#development-and-tests)
- [Safety and verification](#safety-and-verification)
- [Roadmap](#roadmap)

## Highlights

- **Drafting and conversion:** turn CAD/detail/model linework into useful electrical drafting geometry.
- **Wiring and routing:** create conduit runs, boundary routes, home-run lines, cable trays, and wire information.
- **Annotation:** generate labels, tags, dimensions, and evenly distributed elements.
- **Model navigation:** select hosts, match types, align families, create focused 3D/section views, and organize worksets.
- **Electrical analysis:** review clashes, summarize quantities, inspect conduit runs, and report circuits/panels.
- **Schedule and data exchange:** export schedules to CSV/Excel, import values by Mark, create text-note tables, and manage shared parameters.
- **Workflow support:** searchable view manager, sheet creation, options, and log access.

## Complete tool catalogue

The catalogue below reflects the commands registered in `src/Core/ToolRegistry.cs`. The ribbon panel names are included so you can find each command in the application.

### Drafting

| Tool | Function |
|---|---|
| **CAD to Detail Lines** | Redraws lines and arcs from an imported or linked CAD file as detail lines in the active view, with an optional layer filter. |
| **Wires to Detail Lines** | Redraws selected electrical wires—or all wires in the view—as detail lines. |
| **Detail Lines to Wires** | Converts connected straight detail-line chains into chamfered electrical wires. |
| **Connect Runs** | Connects picked elements in order using conduit runs, with direct or L-shaped routing and elbows at a chosen height. |
| **Lines to Conduit** | Converts selected straight detail/model lines into conduits using a chosen conduit type and height. |
| **Fillet Radius** | Sets the default fillet radius used by the fillet commands. |
| **Fillet Two Lines** | Rounds the corner between two picked detail lines; repeat picks until Escape. |
| **Fillet Corners** | Rounds corners where straight detail lines meet, using the selection or the active view. |
| **Split Lines** | Splits straight detail lines where other lines cross or touch them. |
| **Clean Lines** | Removes duplicate or fully overlapping detail lines of the same line style. |

### Wiring

| Tool | Function |
|---|---|
| **Home Run Lines** | Draws home-run detail lines from picked devices to a panel, either individually or as a chain. |
| **Boundary to Conduits** | Converts a detail-line path/boundary into a conduit run with elbows at corners. |
| **Circuit Number Text** | Writes a fixed circuit label, each element's own circuit, or a numbered sequence into a chosen parameter. |
| **Wire Info to Comments** | Writes nearby circuit and wire-type information into conduit Comments. |
| **Cable Catalog** | Opens the editable cable catalog; the catalog is saved in the user's application-data folder. |

### Annotation

| Tool | Function |
|---|---|
| **Switch Labels** | Labels lighting devices in pick order using alphabetic, numeric, or fixed-text labels. |
| **Tags with Leaders** | Places category-appropriate tags with leaders on selected elements. |
| **Aligned Dimension** | Creates an aligned dimension between two picked elements, grids, or reference planes. |
| **Distribute Along Path** | Spaces picked family instances evenly along a line, arc, conduit, or tray path. |

### Model

| Tool | Function |
|---|---|
| **Worksets by Type** | Moves selected elements to worksets named by category or family/type, creating missing worksets where possible. |
| **Select Hosts** | Selects the host elements of selected family instances and zooms to them. |
| **Place Junction Box** | Places a level-based electrical junction box at clicked points at a chosen height. |
| **Align to Reference** | Aligns picked family instances to a reference by rotation, location, or one axis. |
| **Section Along Element** | Creates a section view along a straight conduit, tray, or line. |
| **Select Same Type** | Selects all instances of the picked element's type in the active view. |
| **3D from Selection** | Creates a 3D view with a section box around selected elements. |

### Analysis

| Tool | Function |
|---|---|
| **Clash Check** | Checks selected elements or a chosen category against another category, selects involved elements, and saves a CSV report. |
| **Quantity Takeoff** | Counts elements by category/family/type, including lengths for linear elements, and exports a CSV. |
| **Smart Filter** | Finds elements by category, type name, and parameter value; selects or isolates matching elements. |

### Schedules and data exchange

| Tool | Function |
|---|---|
| **Export All Schedules** | Exports each schedule in the model to its own CSV file. |
| **Export Active Schedule** | Exports the open schedule view to a CSV file. |
| **Schedules to Excel** | Exports the active schedule or all schedules to an Excel workbook, with each schedule on its own sheet. |
| **Import by Mark** | Reads CSV values and updates matching elements by Mark; previews changes before applying them. |
| **CSV to Text Note** | Places CSV table content in the active view as a formatted text note. |
| **Shared Parameters** | Creates common electrical shared parameters and binds them to electrical categories. |
| **Family Type Manager** | Lists available types for selected elements and changes matching selected elements to a chosen type. |

### Panels, views, and sheets

| Tool | Function |
|---|---|
| **View Manager** | Searches printable views and sheets, then activates the selected view. |
| **Sheet Creator** | Creates a new sheet and places the active view or schedule on it. |
| **Panel Single-Line** | Generates a text-based panel single-line report containing circuit, load, pole, and voltage information. |

### Cable and tray

| Tool | Function |
|---|---|
| **Conduit Run Report** | Reports conduit type, size, length, level, and comments for selected conduits or the view, with totals by size. |
| **Tray Summary View** | Creates a drafting view summarizing cable trays by type and size. |
| **Draw Cable Tray** | Draws cable tray between picked points at a chosen height and size, adding elbows at corners. |

### Circuits

| Tool | Function |
|---|---|
| **Circuit Report** | Exports circuit information—including panel, circuit number, load name, and element count—to CSV. |
| **Select by Circuit** | Selects all elements belonging to the same circuit or circuits as a picked element. |

### Settings and support

| Tool | Function |
|---|---|
| **Options** | Configures the report folder, whether reports open automatically, and the default fillet radius. |
| **Open Log** | Opens the ElecKit log file to help diagnose errors. |

**Total: 44 registered ribbon tools**, grouped across Drafting, Wiring, Annotation, Model, Analysis, Schedules, Panels & SLD, Cable & Tray, Circuits, Data, and Settings.

## Requirements

- Windows
- Revit 2020
- Visual Studio 2022 or a compatible .NET SDK/build environment
- .NET Framework 4.7 targeting support for local development

The project targets `net47` and `x64`. When a local Revit 2020 installation is found, the project references its API assemblies. On CI or a machine without Revit installed, it uses the Revit 2020 API NuGet packages.

## Build

### Visual Studio

1. Close Revit if it is running.
2. Open `ElecKit.csproj` in Visual Studio 2022.
3. Restore NuGet packages.
4. Build the project in Debug or Release configuration.

### .NET CLI

From the repository root:

```bash
dotnet build ElecKit.csproj -c Debug
dotnet build ElecKit.csproj -c Release
```

A Debug build attempts to copy the add-in assembly and manifest into the current user's Revit 2020 add-ins directory. Deployment copy operations are configured to continue if the destination is unavailable; check the build output and install paths rather than assuming deployment succeeded.

GitHub Actions builds the project using the Revit API NuGet packages. See [the build workflow](.github/workflows/build.yml) for the current CI steps.

## Install

1. Build the project.
2. Close Revit.
3. Confirm `ElecKit.dll` is in the add-in folder:
   `%APPDATA%\Autodesk\Revit\Addins\2020\ElecKit\`
4. Confirm `ElecKit.addin` is in:
   `%APPDATA%\Autodesk\Revit\Addins\2020\`
5. Start Revit 2020 and look for the ElecKit ribbon tab.

If the tab does not appear, check the manifest assembly path, Windows file permissions, the build output, and the log file. The exact output path can vary with the build configuration.

## Configuration, logs, and reports

| Item | Location / behavior |
|---|---|
| Settings | `%APPDATA%\ElecKit\settings.json` |
| Log | `%LOCALAPPDATA%\ElecKit\log.txt` |
| Reports | Configurable through **Options** |
| Cable catalog | Stored under the user's application-data folder |
| Ribbon registration | `src/Core/ToolRegistry.cs` |

Report formats depend on the command and include CSV, Excel workbook, or text output. Review exported files before using them as project records.

## Development and tests

### Add a command

1. Add a command class under `src/Commands/` deriving from `ElecCommand`.
2. Apply the appropriate Revit transaction attribute when required.
3. Register the command in `src/Core/ToolRegistry.cs`; the application builds ribbon buttons from this central list.
4. Add or update automated tests where the behavior can be tested outside a live Revit session.
5. Build and verify the command in Revit 2020 using a disposable project copy.

### Test status and scope

The GitHub Actions workflow provides a build check. A successful compile does **not** prove that every command behaves correctly in every model, family library, view type, worksharing setup, or project configuration. Some commands depend on active-view context, element selection, available family/type definitions, tags, worksets, or compatible electrical system data.

Use the repository's [ROADMAP](ROADMAP.md) to review the current feature mapping and notes. Keep feature claims aligned with evidence from builds and tests.

## Safety and verification

- Test model-changing commands on a **copy** of the project first.
- Review any preview shown by import/update tools before committing changes.
- Confirm conduit, tray, fitting, family, tag, and shared-parameter definitions exist in the target model.
- Inspect CSV/Excel reports for missing values, delimiters, units, and expected row counts.
- Keep private project files, credentials, and sensitive data out of Git.
- Back up project files before bulk operations or data imports.
- Use the log file when a command fails; include a minimal reproduction and relevant sanitized log details when reporting a bug.

## Roadmap

See [ROADMAP.md](ROADMAP.md) for the requested-function mapping, implementation notes, and known exclusions. The current implementation does not include server-based **Update** or **Contact Us** commands, and it does not provide a licensing or update server.
