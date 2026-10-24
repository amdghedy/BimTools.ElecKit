# ElecKit

**An electrical productivity add-in for Autodesk Revit 2020.**

ElecKit brings frequently used electrical drafting, wiring, model-selection, and schedule export/import tasks into a single ribbon. It is designed to reduce repetitive work while keeping tools close to the Revit workflow.

[![Build](https://github.com/amdghedy/BimTools.ElecKit/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/amdghedy/BimTools.ElecKit/actions/workflows/build.yml) [![Revit](https://img.shields.io/badge/Revit-2020-2D6DB5)](https://www.autodesk.com/products/revit/overview) [![Platform](https://img.shields.io/badge/platform-Windows-0078D4)](https://www.microsoft.com/windows)

> **Project status:** Active development. CI checks compilation; individual commands still require functional verification inside Revit. See [ROADMAP.md](ROADMAP.md) for implemented and planned tools.

## Contents
- [Features](#features)
- [Compatibility](#compatibility)
- [Install and run](#install-and-run)
- [Build from source](#build-from-source)
- [Ribbon tools](#ribbon-tools)
- [Configuration and logs](#configuration-and-logs)
- [Add a command](#add-a-command)
- [Known limitations](#known-limitations)
- [Contributing and support](#contributing-and-support)

## Features
- **Drafting conversion:** convert supported CAD geometry and detail lines to Revit detail lines or electrical wires.
- **Line cleanup and editing:** connect runs, split lines, fillet corners or picked lines, and clean duplicate detail-line geometry.
- **Wiring workflows:** create home-run lines, create conduits from boundary lines, place circuit-number text, and copy wire information into conduit comments.
- **Model navigation and coordination:** select host elements, select same-type instances, use Smart Filter to select same-type, same-category, or same-parameter-value elements in the active view, assign selected elements to a user workset in a workshared model, create a 3D view around a selection, and export MEP clash candidates with solid-intersection checks where geometry is available.
- **Schedule and circuit data:** export active or multiple schedules to CSV, export selected element parameters to a keyed CSV (ElementId and Mark), import parameter values from that CSV, create circuit reports, and select elements by circuit.\n- **Electrical quantity takeoff:** export grouped counts and MEP curve lengths from the active model view to CSV. The grouping/aggregation logic has a Revit-independent regression suite; category-by-category behavior still requires a Revit 2020 test model.
- **Central command registry:** ribbon tools are registered centrally rather than wired individually.

Feature names describe intended workflows; availability and behavior can vary by selection, view, model content, and Revit API constraints.

## Compatibility
| Requirement | Target |
|---|---|
| Host application | Autodesk Revit 2020 |
| Runtime target | .NET Framework 4.7 |
| Architecture | Windows / x64 |
| Recommended IDE | Visual Studio 2022 |
| CI | GitHub Actions |

Revit must be installed to run the add-in. CI uses Revit API NuGet packages to compile without requiring a full Revit installation on the runner.

## Install and run
1. Download or clone this repository.
2. Build the project using the instructions below.
3. Confirm the add-in assembly and `.addin` manifest are copied to `%APPDATA%\Autodesk\Revit\Addins\2020`.
4. Close and restart Revit 2020.
5. Find the **ElecKit** ribbon tab and launch a tool.

A Debug build is configured to copy the add-in to the per-user Revit 2020 add-ins directory. Close Revit before rebuilding or replacing loaded files. If the copy step reports an error, verify the destination path and permissions rather than assuming deployment succeeded.

## Build from source
### Visual Studio
1. Open the repository/project in Visual Studio 2022.
2. Restore NuGet packages.
3. Select **Debug** or **Release** and build the project.

### .NET CLI
From the repository root, run:
```powershell
dotnet build -c Debug
```

If the command cannot locate a project, open the project file under `src/` and run the command from its directory, or pass the project path explicitly.

### Continuous integration
The workflow is defined in [`.github/workflows/build.yml`](.github/workflows/build.yml). It compiles the project using Revit API packages and helps catch compile-time regressions. A successful CI build is **not** proof that every command behaves correctly in a live Revit model.

[View workflow runs](https://github.com/amdghedy/BimTools.ElecKit/actions).

## Ribbon tools
| Group | Examples |
|---|---|
| Drafting | CAD to Detail Lines, Wires to Detail Lines, Detail Lines to Wires, Connect Runs, Lines to Conduit, Fillet tools, Split Lines, Clean Lines |
| Wiring | Home Run Lines, Boundary to Conduits, Circuit Number Text, Wire Info to Comments, Cable Catalog |
| Model | Select Hosts, Select Same Type, Smart Filter, Assign Workset, 3D from Selection, Clash Review |
| Schedules | Export All Schedules, Export Active Schedule, Export Element Parameters, Import CSV Values, Quantity Takeoff |
| Circuits | Circuit Report, Select by Circuit |

The [roadmap](ROADMAP.md) records additional requested functions and their status. Planned entries are not currently available just because they appear in that document.

## Configuration and logs
- **Log:** `%LOCALAPPDATA%\ElecKit\log.txt`
- **Settings:** `%APPDATA%\ElecKit\settings.json`

If a command fails, note the tool name, Revit version, active view, selected element types, and relevant log entries. Remove sensitive project information before sharing logs in an issue.

## Add a command
1. Add a command class under `src/Commands/`, deriving from `ElecCommand`.
2. Apply the appropriate Revit transaction attribute and implement the execution logic.
3. Register the command in `src/Core/ToolRegistry.cs`.
4. Build the project and verify that the command appears on the ribbon.
5. Test success, cancellation, invalid selection, and failure/rollback behavior in Revit 2020.

Follow neighboring commands and shared helpers. Keep geometry operations transaction-safe, validate selections before modifying the model, and provide clear messages when an operation cannot be completed.

## Known limitations
- The project targets **Revit 2020**; compatibility with other Revit versions is not guaranteed.
- CI currently validates compilation, not the full runtime behavior of every command.
- There is no automated live-Revit integration test suite at present.
- Test geometry-changing tools on a copy of a project before production use, especially tools that clean, split, convert, or delete elements.
- Server-based update and licensing features are not included.

## Contributing and support
Submit bug reports and feature requests through [GitHub Issues](https://github.com/amdghedy/BimTools.ElecKit/issues). Please include:
- Revit version and a short description of the workflow.
- Minimal steps to reproduce the issue.
- Expected result versus actual result.
- Relevant error text or sanitized log excerpts.

For feature proposals, describe the repetitive task being improved and the expected result in Revit. Do not upload confidential models or sensitive project data.

---

Maintained by [amdghedy](https://github.com/amdghedy).

## Automated validation

GitHub Actions builds the Revit 2020 / .NET Framework 4.7 add-in and runs the Revit-independent CSV parser regression tests in `tests/ElecKit.Tests`. These tests cover quoted delimiters, escaped quotes, empty cells, malformed quoting, length conversions, bounding-box clearance and grouped quantity aggregation. Category-by-category takeoff verification and all live-Revit command checks remain manual release gates. A successful CI run does **not** establish that commands have been exercised inside Revit; the live Revit 2020 smoke test remains a release gate. See [the feature gap and delivery roadmap](ROADMAP.md).


## Clash review scope

Clash Review screens host-model elements against other host elements and loaded Revit links for supported MEP, structural, wall and floor categories. Linked bounding boxes and solids are transformed into host coordinates. Reports distinguish confirmed positive-volume solid intersections from unavailable/incomplete geometry checks. The command can optionally screen near-misses using a user-entered millimetre threshold; this is an axis-aligned bounding-box distance estimate, not exact solid-to-solid clearance. Exact clearance and live Revit verification remain open. CI build and Revit-independent tests passed for commit [`7a1278d`](https://github.com/amdghedy/BimTools.ElecKit/commit/7a1278db4cdd4062b9f37cf12d60a7d5bac4bb4f); this does not replace testing in a disposable Revit 2020 model. See [ROADMAP.md](ROADMAP.md).


### Additional native gap-closure tools

- **Circuit & Panel Audit** — exports conservative voltage, pole-count and explicitly declared VA-capacity checks; missing parameters are flagged for review.
- **Route Cable Tray** — creates direct/L-shaped and multi-segment routes, plus an optional orthogonal obstacle-aware route using active-view bounding boxes and a user clearance threshold. Failed route pairs roll back.
- **Lines to Cable Tray** — converts straight detail/model lines with per-segment rollback and failure reporting.
- **Boundary to Cable Trays** — creates connected tray runs, including closed boundaries, and rolls back the entire run if a required elbow fails.
- **Deployment Audit** — checks the Revit 2020 manifest, deployed DLL and loaded assembly hashes.
- Transaction and command outcomes are logged locally. Live Revit 2020 validation remains a release gate.


## Revit 2020 release validation

Before release, run the [Revit 2020 integration validation checklist](docs/REVIT2020_VALIDATION_CHECKLIST.md) in a disposable model and attach the reports/screenshots to the pull request. CI alone does not verify Revit runtime behavior.
