# ElecKit Roadmap

This roadmap reflects the commands currently registered in `src/Core/ToolRegistry.cs` on `main`. A feature is marked **Implemented in ribbon** only when it is registered as a Revit command. This documents source-code registration; it is not a claim that every command has been runtime-tested in every model.

## Current baseline

- **22 registered commands** across Drafting, Wiring, Model, Schedules, and Circuits.
- **Target:** Autodesk Revit 2020, .NET Framework 4.7, x64 (see `ElecKit.csproj`).
- **Not included:** server-based Update and Contact Us functions. The add-in currently has no update server or licensing service.
- A feature listed as planned is not currently exposed as a command in the ribbon registry. Do not interpret a roadmap entry as an existing feature.

## Implemented in the ribbon

### Drafting

| Command | Current registered behaviour |
|---|---|
| CAD to Detail Lines | Redraws lines and arcs from an imported or linked CAD file as detail lines; supports an optional layer filter. |
| Wires to Detail Lines | Redraws selected electrical wires, or all wires in the active view, as detail lines. |
| Detail Lines to Wires | Converts connected straight detail lines into chamfered electrical wires, one wire per line chain. |
| Connect Runs | Connects picked elements in order using conduit runs, directly or with an L-shaped route and elbow, at a specified height. |
| Lines to Conduit | Converts selected straight detail/model lines into conduits after choosing a conduit type and height. |
| Fillet Radius | Sets the default fillet radius used by the fillet tools. |
| Fillet Two Lines | Fillets two picked detail lines; the user chooses which portions to keep. |
| Fillet Corners | Rounds corners where straight detail lines meet, using the selection or the whole view. |
| Split Lines | Splits straight detail lines where another line crosses or touches them, using the selection or the whole view. |
| Clean Lines | Deletes duplicate or fully overlapping detail lines with the same line style, using the selection or the whole view. |

### Wiring

| Command | Current registered behaviour |
|---|---|
| Home Run Lines | Draws home-run detail lines from picked devices to a panel, as individual lines or a chain. |
| Boundary to Conduits | Converts a path/boundary of detail lines into a continuous conduit run with elbows at corners. |
| Circuit Number Text | Writes circuit-number text to a parameter on selected fixtures/equipment, using a selected text mode. |
| Wire Info to Comments | Writes nearby device/circuit and wire-type information into conduit Comments. |
| Cable Catalog Editor | Opens the cable catalog for review/editing; the catalog is saved in the user's AppData folder. |

### Model

| Command | Current registered behaviour |
|---|---|
| Select Hosts | Selects host elements of selected family instances and zooms to them. |
| Select Same Type | Picks an element and selects instances of the same type in the active view. |
| 3D from Selection | Creates a 3D view with a section box fitted around selected elements. |

### Schedules

| Command | Current registered behaviour |
|---|---|
| Export All Schedules | Exports each schedule in the model to its own CSV file in a new folder. |
| Export Active Schedule | Exports the currently open schedule view to one CSV file. |

### Circuits

| Command | Current registered behaviour |
|---|---|
| Circuit Report | Exports circuit information (panel, circuit number, load name, and element count) to CSV. |
| Select by Circuit | Picks an element and selects elements belonging to the same circuit(s). |

## Planned / not currently registered

These items are not registered in `src/Core/ToolRegistry.cs` on `main`; they should be treated as planned work, not delivered functionality.

### Annotation and drawing utilities

- [ ] Switch Label — label text on lighting devices.
- [ ] Tags with Leaders — place independent tags with leaders.
- [ ] Aligned Dimension — create an aligned dimension between two elements.
- [ ] Distribute Along Path — distribute elements at even spacing along a path.
- [ ] CAD/line cleanup enhancements beyond the current duplicate/overlap detail-line cleanup.

### Model and view utilities

- [ ] Worksets by Type — assign worksets based on element type.
- [ ] Place Junction Box — place a junction box at a picked point.
- [ ] Align to Reference — align an element's location and rotation to a reference.
- [ ] Section Along Element — create a section aligned to a linear element.
- [ ] Clash Check — check for clashes between selected categories/elements.
- [ ] Quantity Takeoff — report quantities for selected elements or categories.
- [ ] Smart Filter — filter/isolate by category, type, or parameter.

### Schedule and data exchange

- [ ] Export Schedules to Excel — Excel output in addition to the existing CSV exporters.
- [ ] Import Values by Mark — import CSV values onto elements matched by Mark.
- [ ] CSV to Text Note — import CSV data as a formatted text note.
- [ ] Shared Parameters — create and bind electrical parameters.
- [ ] Family Type Manager — list family types and change the type of an instance.

### Panels, cable/tray, and view management

- [ ] View Manager — list printable views and activate a selected view.
- [ ] Sheet Creator — create a sheet with the active view.
- [ ] Panel Single-Line Report — produce a single-line report for a panel.
- [ ] Conduit Run Report — report conduit sizes, lengths, and comments.
- [ ] Tray Summary View — create a cable-tray summary in a drafting view.
- [ ] Draw Cable Tray — create a cable tray between two picked points.

## Suggested priorities

1. **Model coordination and annotation:** Align to Reference, Place Junction Box, Tags with Leaders, Aligned Dimension, and Distribute Along Path.
2. **Data and schedules:** Import Values by Mark, CSV to Text Note, Excel export, and Shared Parameters.
3. **Electrical reporting:** Quantity Takeoff, Conduit Run Report, Panel Single-Line Report, and cable-tray summary.
4. **Quality and productivity:** Clash Check and Smart Filter.
5. **View/documentation workflow:** View Manager and Sheet Creator.

## Status and verification policy

- **Implemented in ribbon** means a command is present in the source registry. It does not by itself prove build success, correct behaviour in every Revit model, or release readiness.
- **Planned / not currently registered** means no command for the feature is registered in the current ribbon registry. Before implementation is marked complete, add the command, register it, build against the supported Revit API, and test it in Revit.
- Keep this roadmap synchronized with `src/Core/ToolRegistry.cs` and the actual command implementations whenever the ribbon changes.
