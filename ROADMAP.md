# ElecKit Feature Gap and Delivery Roadmap

## Purpose

ElecKit is the focused, public electrical productivity add-in targeting Revit 2020 / .NET Framework 4.7. ElecTool is the broader private reference suite targeting .NET Framework 4.8 with a Revit 2020 API package baseline. The projects are related, but they are **not feature-equivalent**. This file tracks the practical gaps without claiming that a command is production-ready merely because a class or ribbon button exists.

## Current ElecKit baseline

The main branch contains 29 command classes:

| Area | Implemented commands |
|---|---|
| Drafting | CAD to Detail Lines; Wires to Detail Lines; Detail Lines to Wires; Connect Runs; Lines to Conduit; Fillet Radius; Fillet Two Lines; Fillet Corners; Split Lines; Clean Lines |
| Wiring | Home Run Lines; Boundary to Conduits; Circuit Number Text; Wire Info to Comments; Cable Catalog |
| Model and selection | Select Hosts; Select Same Type; Smart Filter; Assign Workset; Clash Review; 3D from Selection |
| Schedules and data exchange | Export All Schedules; Export Active Schedule; Export Element Parameters; Import CSV Values; Quantity Takeoff |
| Circuits | Circuit Report; Select by Circuit; Assign Circuit Panel |

**Status definition:** “Implemented” means a command is present in the current source tree. It does not, by itself, prove a clean build, safe behavior in every model, or successful testing inside Revit. Revit integration testing remains a release gate.

## Gap matrix against ElecTool

The following capabilities exist as named commands or command groups in ElecTool but are not currently represented by a dedicated equivalent in ElecKit. Before porting, inspect each source workflow, identify its actual behavior and dependencies, then implement and test it against ElecKit's Revit 2020 / net47 constraints.

| Priority | Gap | ElecTool reference | Acceptance criteria for ElecKit |
|---|---|---|---|
| P0 | Clean Lines safety | `OverkillCommand.cs` | Remove only exact duplicates or shorter collinear segments fully contained by a retained segment. Preserve partial overlaps and disjoint collinear segments. |
| P1 | Clash detection | `ClashDetectionCommand.cs`, `NewToolsCoordination.cs` | **Partially implemented.** Exports host-host and host-linked bounding-box candidates for supported MEP, structural, wall and floor categories. Loaded linked-model bounding boxes and solids are transformed into host coordinates; positive-volume intersections are confirmed where usable solids exist. CSV rows distinguish confirmed intersections, no-positive-volume-intersection, and unavailable/incomplete checks. Host-host, host-linked and linked-linked comparisons are implemented using host-coordinate bounding boxes and solid intersection checks. Category scope is user-selectable (MEP-only or MEP plus walls/floors/structural framing/columns). Optional threshold screening now reports axis-aligned bounding-box separation in millimetres and labels near-misses explicitly; this is a broad-phase estimate, not exact solid clearance. Exact solid-to-solid clearance, more granular category selection and live-model verification remain open. |
| P1 | Quantity takeoff | `QuantityTakeoffCommand.cs`, `NewToolsQA.cs` | Grouped counts and MEP curve lengths in millimetres are exported with category/family/type and resolved level names where available. Category scope is selectable between electrical categories and electrical plus supported mechanical/duct/pipe categories. The grouping accumulator is now isolated in `src/Core/QuantityTakeoffGrouping.cs` and has regression tests for aggregation, key separation, delimiter-safe keys, invalid lengths and null input. Category-by-category verification and Revit curve extraction fixtures remain open. |
| P1 | Schedule and parameter data exchange | `ScheduleDynamicExportCommand.cs`, `ScheduleDynamicImportCommand.cs`, `ImportExcelAsScheduleCommand.cs` | **Partially implemented.** `ExportElementParametersCommand.cs` exports selected instances with ElementId, Mark and common writable instance parameters; `ImportScheduleValuesCommand.cs` imports the keyed CSV, previews before changes, skips blank cells and reports failures. This is an element-parameter round-trip, not a native schedule-row round-trip; type parameters, ElementId-valued parameters, and grouped schedule rows are not covered. Duplicate headers and duplicate primary-key rows are rejected; when both ElementId and Mark are supplied, import also verifies that both identify the same unique instance and skips mismatched/stale-key rows. |
| P1 | Circuit and panel tools | `NewToolsCircuits.cs`, `AssignCircuitNumberCommand.cs`, `PanelSLDCommand.cs` | **Partially implemented.** Circuit report, select-by-circuit, and a guarded manual circuit-to-panel assignment command are present. The assignment command requires the user to pick the circuit member and destination electrical equipment, and asks Revit to perform the reassignment in a transaction. Electrical compatibility, spare capacity, panel schedules, and live Revit 2020 behavior must be verified in a test model; automatic assignment and single-line-diagram generation remain open. |
| P1 | Conduit and cable-tray workflows | `NewToolsConduit.cs`, `NewToolsTray.cs`, `CableConduitCommand.cs`, `CableTrayCommand.cs`, `DrawCableTrayCommand.cs` | Validate routing inputs, sizes and fittings; report failed segments; preserve user control of type and level. |
| P2 | Workset assignment | `AssignToTypeWorksetCommand.cs` | **Partially implemented.** `AssignWorksetCommand.cs` requires a workshared document, lets the user select instances and a target user workset, and reports changed/unchanged/failed counts. A preview table and live-Revit ownership/permission testing remain open. |
| P2 | Smart filtering | `SmartFilterCommand.cs` | **Partially implemented.** Pick an element and select same-type, same-category, or same-parameter-value instances in the active view. A follow-up action now supports selection-only or temporary isolation; use Revit's Reset Temporary Hide/Isolate control to restore the view. Parameter-name/value matching is exact text (numeric values use Revit’s displayed string); compound multi-rule filtering remains open. |
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
- [x] Build verification: the latest successful CI run is linked from GitHub Actions; require a successful run for every subsequent change.
- [ ] Runtime verification: test commands inside Revit 2020 with a disposable model before calling them production-ready.
- [ ] Transaction consistency: audit conversion and creation commands for clear rollback behavior and partial-success reporting.
- [x] Ribbon startup: suppress only the known “tab already exists” condition; surface unrelated startup failures.
- [x] Deployment: Debug copy failures are no longer suppressed by `ContinueOnError`.
- [x] Automated tests: added a .NET 8 xUnit project with CSV parser regression tests and length-unit conversion regression tests. GitHub Actions runs these tests. Revit-dependent geometry, parameter mapping, grouped quantity totals and command behavior still need separate tests. The pure axis-aligned box-distance helper has unit tests for overlap, face gaps, diagonal gaps, invalid bounds and non-finite coordinates.
- [x] Data import validation: rejects duplicate parameter headers and duplicate row keys before transaction; malformed CSV quoting and file-access errors now produce a user-facing cancellation message.
- [ ] Quantity takeoff: the grouped-total accumulator now has Revit-independent regression tests; test each supported category and confirm the active view is a valid model view in Revit 2020. Revit curve extraction still needs integration fixtures. Shared feet/mm conversion has isolated unit tests. Category scope can be selected at runtime.
- [x] Documentation: maintain a command inventory and clearly distinguish source-present, CI-build-passing and Revit-runtime-tested states.
- [ ] Remove or archive `fix-revit2020-compile.patch` once its changes are confirmed to be incorporated and no longer needed.

## Automated regression tests\n\nThe `tests/ElecKit.Tests` project targets .NET 8 so pure logic can be exercised in GitHub Actions without installing Revit. The production `CsvLineParser` is linked directly into that test project and is also used by the Revit CSV importer. This does not replace Revit 2020 integration testing.\n\n## Recommended implementation order

1. Fix and regression-test data-loss risks before adding more commands.
2. Stabilize transaction handling, startup diagnostics and deployment feedback.
3. Expand keyed element-parameter export/import; native schedule-row round-trip remains a separate task.
4. Add quantity takeoff and CSV reporting.
5. Extend smart selection/filtering with compound rules; validate workset assignment in live Revit and add preview/ownership safeguards.
6. Add clash review and coordination reporting.
7. Expand panel/circuit workflows (capacity/compatibility validation, panel schedules and single-line diagrams), then add conduit/tray tools.
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


## Native gap-closure implementation — feature branch 2026-10-24

- [x] Circuit preflight blocks known voltage/pole-count mismatches and known load-over-capacity conflicts where explicitly named comparable VA/kVA data exists. Missing ratings are warnings, not verified-safe results.
- [x] Circuit & Panel Audit exports per-circuit values and PASS/FAIL/REVIEW findings. This is a conservative parameter-based screen, not a substitute for protection, phase-balance, short-circuit or code calculations.
- [x] Route Cable Tray creates direct or L-shaped tray routes between ordered picked anchors; a route pair rolls back if its required elbow cannot be created.
- [x] Transaction and command start/end/failure/rollback/commit outcomes are written to the local ElecKit log.
- [x] Deployment Audit compares the Revit 2020 manifest, deployed DLL, loaded assembly path and SHA-256 hashes. Debug deployment copies the exact assembly and verifies outputs.
- [x] Revit-independent circuit validation regression tests added to CI.
- [ ] Revit 2020 integration checks remain mandatory for API behavior, connector geometry, fitting routing preferences, project-specific panel parameter mapping and deployment state.

- [x] Expanded tray workflows with straight-line conversion and connected/closed boundary routing; required elbow failures roll back each affected tray run.
- [x] Tightened conduit routing consistency: Connect Runs no longer counts rolled-back L-shaped conduits as created, and required elbow failures roll back the pair; Boundary to Conduits rolls back the run on required elbow failure.
