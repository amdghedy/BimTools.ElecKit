# ElecKit Feature Gap and Delivery Roadmap

## Purpose

ElecKit is the focused, public electrical productivity add-in targeting Revit 2020 / .NET Framework 4.7. ElecTool is the broader private reference suite targeting .NET Framework 4.8 with a Revit 2020 API package baseline. The projects are related, but they are **not feature-equivalent**. This file tracks the practical gaps without claiming that a command is production-ready merely because a class or ribbon button exists.

## Current ElecKit baseline

The main branch contains 24 command classes:

| Area | Implemented commands |
|---|---|
| Drafting | CAD to Detail Lines; Wires to Detail Lines; Detail Lines to Wires; Connect Runs; Lines to Conduit; Fillet Radius; Fillet Two Lines; Fillet Corners; Split Lines; Clean Lines |
| Wiring | Home Run Lines; Boundary to Conduits; Circuit Number Text; Wire Info to Comments; Cable Catalog |
| Model and selection | Select Hosts; Select Same Type; 3D from Selection |
| Schedules | Export All Schedules; Export Active Schedule |
| Circuits | Circuit Report; Select by Circuit |

**Status definition:** “Implemented” means a command is present in the current source tree. It does not, by itself, prove a clean build, safe behavior in every model, or successful testing inside Revit. Revit integration testing remains a release gate.

## Gap matrix against ElecTool

The following capabilities exist as named commands or command groups in ElecTool but are not currently represented by a dedicated equivalent in ElecKit. Before porting, inspect each source workflow, identify its actual behavior and dependencies, then implement and test it against ElecKit's Revit 2020 / net47 constraints.

| Priority | Gap | ElecTool reference | Acceptance criteria for ElecKit |
|---|---|---|---|
| P0 | Clean Lines safety | `OverkillCommand.cs` | Remove only exact duplicates or shorter collinear segments fully contained by a retained segment. Preserve partial overlaps and disjoint collinear segments. |
| P1 | Clash detection | `ClashDetectionCommand.cs`, `NewToolsCoordination.cs` | User-selectable categories, explicit tolerance, deterministic pair results, element IDs and a review/export report. Avoid presenting bounding-box intersections as confirmed geometric clashes. |
| P1 | Quantity takeoff | `QuantityTakeoffCommand.cs`, `NewToolsQA.cs` | Category/filter selection, correct internal-unit conversion, grouped totals, and CSV output with clear units. |
| P1 | Schedule round-trip | `ScheduleDynamicExportCommand.cs`, `ScheduleDynamicImportCommand.cs`, `ImportExcelAsScheduleCommand.cs` | **Partially implemented.** `ImportScheduleValuesCommand.cs` imports CSV by ElementId or unique Mark, previews before changes, skips blank cells, uses unit-aware parsing for doubles, and reports failures. A true keyed export/import round-trip still needs a matching export format that supplies stable ElementIds. |
| P1 | Circuit and panel tools | `NewToolsCircuits.cs`, `AssignCircuitNumberCommand.cs`, `PanelSLDCommand.cs` | Validate circuit/panel relationships; report skipped items; never silently change circuit assignments. |
| P1 | Conduit and cable-tray workflows | `NewToolsConduit.cs`, `NewToolsTray.cs`, `CableConduitCommand.cs`, `CableTrayCommand.cs`, `DrawCableTrayCommand.cs` | Validate routing inputs, sizes and fittings; report failed segments; preserve user control of type and level. |
| P2 | Workset assignment | `AssignToTypeWorksetCommand.cs` | Preview affected elements, require a valid workshared document and target workset, and report per-element failures. |
| P2 | Smart filtering | `SmartFilterCommand.cs` | Filter by category, type and parameter; show the match count; offer select/isolate without modifying model data. |
| P2 | Parameter management | `SharedParamsCommand.cs` | Validate shared-parameter definitions and bindings; preserve existing bindings; explain conflicts before applying changes. |
| P2 | Family type manager | `FamilyTypeManagerCommand.cs` | Show available types, validate type compatibility, and report elements that could not be changed. |
| P2 | Junction-box placement and alignment | `AddJunctionBoxCommand.cs`, `AlignToFamilyCommand.cs`, `ConnectToElementCommand.cs` | Validate host/connector compatibility, use explicit pick prompts, and roll back cleanly on failure. |
| P2 | Annotation tools | `SwitchLabelCommand.cs`, `LeaderToolsPaletteCommand.cs`, `DimProCommand.cs`, `DistributeElementsCommand.cs` | Respect view plane and element geometry; provide clear selection prompts and preserve existing annotations. |
| P2 | View and sheet productivity | `ViewManagerCommand.cs`, `AlignedSectionCommand.cs`, `ElecToolSheetGenCommand.cs`, `View3DBoxCommand.cs` | Create valid views/sheets with unique names, predictable crop/section extents, and no duplicate or orphaned views. |
| P3 | Cable-tray and conduit reporting | `ElecToolExporterCommand.cs`, `CableConduitCommand.cs` | Produce auditable totals, explicit units, stable columns and useful empty-result messages. |
| P3 | Family/type selection and view utilities | `FamilyTypeManagerCommand.cs`, `MatchTypeSelectCommand.cs`, `ViewManagerCommand.cs` | Make selection scope and active-view behavior explicit; do not change model data unexpectedly. |
| P3 | Settings and support commands | `SettingsCommand.cs`, `CheckForUpdateCommand.cs`, `ContactUsCommand.cs` | Keep settings local and validated. Do not add a network updater or external endpoint until hosting, security and support ownership are defined. |

Some ElecTool features are grouped inside the `NewTools*.cs` files, so the table is a **feature-level gap list**, not a one-class-to-one-class porting plan. Confirm the actual registered ribbon controls and behavior before marking any row complete.

## Reliability and release gaps

- [x] Clean Lines: restrict deletion to duplicate or fully contained finite segments; partial overlaps and disjoint collinear segments must remain.
- [ ] Build verification: require a successful CI build for every change and retain the build log/artifact.
- [ ] Runtime verification: test commands inside Revit 2020 with a disposable model before calling them production-ready.
- [ ] Transaction consistency: audit conversion and creation commands for clear rollback behavior and partial-success reporting.
- [ ] Ribbon startup: handle only the known “tab already exists” condition; surface unrelated startup failures instead of silently swallowing them.
- [x] Deployment: Debug copy failures are no longer suppressed by `ContinueOnError`; the latest project-file change is awaiting CI verification.
- [ ] Automated tests: add testable pure logic for geometry, filtering, CSV parsing, parameter mapping and unit conversions where practical.\n- [ ] Data import validation: reject duplicate parameter headers and verify the import file contains unique row keys before applying changes.\n- [ ] Quantity takeoff: test each supported category and confirm the active view is a valid model view; add regression fixtures for grouped totals and curve-length conversion.
- [ ] Documentation: maintain a command inventory and clearly distinguish source-present, CI-build-passing and Revit-runtime-tested states.
- [ ] Remove or archive `fix-revit2020-compile.patch` once its changes are confirmed to be incorporated and no longer needed.

## Recommended implementation order

1. Fix and regression-test data-loss risks before adding more commands.
2. Stabilize transaction handling, startup diagnostics and deployment feedback.
3. Add a safe schedule export/import workflow with preview and key validation.
4. Add quantity takeoff and CSV reporting.
5. Add smart selection/filtering and workset assignment.
6. Add clash review and coordination reporting.
7. Add panel/circuit and conduit/tray tools.
8. Add annotation, view/sheet, parameter and family management tools.
9. Update the command inventory only after each feature has passed CI and a documented Revit test.

## Definition of done for every new command

- Compiles against the declared Revit 2020 API / .NET Framework 4.7 target.
- Has explicit selection/input validation and useful cancellation behavior.
- Uses a transaction strategy appropriate to the operation; failures roll back or are reported accurately.
- Does not silently skip invalid elements or overwrite user data without a clear rule.
- Handles internal units and user-facing units correctly.
- Has a short manual test checklist and evidence of the result in Revit.
- Is reflected in the README and this roadmap with an honest status.
