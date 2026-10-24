# ElecKit

**Electrical productivity tools for Autodesk Revit — one toolkit, version-specific builds.**

ElecKit brings common electrical drafting, wiring, model-selection, schedule-data, quantity-takeoff, and coordination workflows into a dedicated Revit ribbon. The goal is to reduce repetitive clicks, make routine tasks more consistent, and provide clearer feedback when model operations cannot be completed.

<p align="center">
  <img src="https://img.shields.io/badge/Revit-2020--2027-2D6DB5?style=for-the-badge" alt="Revit 2020–2027 target versions">
  <img src="https://img.shields.io/badge/Platform-Windows-0078D4?style=for-the-badge" alt="Windows">
  <img src="https://img.shields.io/badge/Language-C%23-68217A?style=for-the-badge" alt="C sharp">
</p>

<p align="center">
  <a href="https://github.com/amdghedy/BimTools.ElecKit/actions/workflows/build-all-versions.yml"><img src="https://github.com/amdghedy/BimTools.ElecKit/actions/workflows/build-all-versions.yml/badge.svg?branch=feature/native-gap-closure-2026" alt="Build all Revit versions workflow"></a>
  <a href="https://github.com/amdghedy/BimTools.ElecKit/issues"><img src="https://img.shields.io/github/issues/amdghedy/BimTools.ElecKit" alt="GitHub issues"></a>
  <a href="https://github.com/amdghedy/BimTools.ElecKit/blob/feature/native-gap-closure-2026/LICENSE"><img src="https://img.shields.io/badge/License-see%20repository-lightgrey" alt="License"></a>
</p>

> **Development status:** Active development. GitHub Actions builds version-specific packages and runs Revit-independent tests. A successful compile is not a substitute for testing the commands in each supported Revit installation. Treat the current multi-version build and installer as release candidates until all CI jobs pass and the live-model checklist is completed.

## Quick links

- **[Latest GitHub Actions runs](https://github.com/amdghedy/BimTools.ElecKit/actions/workflows/build-all-versions.yml)**
- **[Pull request / current development](https://github.com/amdghedy/BimTools.ElecKit/pull/2)**
- **[Feature roadmap](ROADMAP.md)**
- **[Revit 2020 validation checklist](docs/REVIT2020_VALIDATION_CHECKLIST.md)**
- **[Report a bug or request a feature](https://github.com/amdghedy/BimTools.ElecKit/issues)**

## What ElecKit helps with

### Drafting and line conversion
- Convert supported CAD/detail geometry to Revit detail lines or electrical wires.
- Connect runs, split lines, fillet supported corners, and clean duplicate detail-line geometry.
- Convert supported lines into conduit or cable-tray routes.

### Wiring and electrical workflows
- Create home-run lines and circuit-number text.
- Create conduit or cable-tray runs from supported boundaries.
- Copy wire information into conduit comments and access cable-catalog workflows.
- Audit available circuit voltage, pole-count, and explicitly declared panel-capacity information; uncertain or missing data should be reviewed rather than treated as a pass.

### Model selection and coordination
- Select host elements, same-type instances, and elements matching compound Smart Filter rules.
- Assign selected elements to a workset in a workshared model.
- Create a 3D view around a selection.
- Review potential MEP/architectural/structural clashes with solid-intersection checks where geometry is available.
- Screen possible near-misses using bounding-box estimates; this is not exact solid-to-solid clearance analysis.

### Schedules and quantities
- Export active or multiple schedules to CSV.
- Export selected element parameters to keyed CSV and import supported values back into a model.
- Generate circuit reports and select elements by circuit.
- Export grouped quantity counts and supported MEP curve lengths.
- Preserve typed schedule values where supported, including ElementId-related data; validate imported changes on a copy of the model.

Tool availability and results depend on the active view, selection, model content, category, parameters, and Revit API limitations. See [ROADMAP.md](ROADMAP.md) for current implementation notes and remaining gaps.

## Supported Revit versions

| Revit version | Target framework | Package |
|---|---|---|
| 2020 | .NET Framework 4.7 | Dedicated DLL and `.addin` |
| 2021–2024 | .NET Framework 4.8 | Dedicated DLL and `.addin` |
| 2025–2026 | .NET 8 for Windows | Dedicated DLL and `.addin` |
| 2027 | .NET 10 for Windows | Dedicated DLL and `.addin` |

Each Revit version must use its own build against the matching Revit API. **Do not copy one version's DLL into another version's Addins folder.** Revit must be installed locally to run and functionally test the add-in.

## Download and install

### Option A — setup EXE

1. Open the [multi-version GitHub Actions workflow](https://github.com/amdghedy/BimTools.ElecKit/actions/workflows/build-all-versions.yml).
2. Open the latest run for the development branch.
3. Confirm that every Revit build job, the test job, and the installer job completed successfully.
4. In **Artifacts**, download `ElecKit-Setup-EXE` if it is present.
5. Run the setup and select only the Revit versions installed on your computer.
6. Restart Revit and look for the **ElecKit** ribbon tab.

The installer is a CI artifact, not a guaranteed permanent download. If `ElecKit-Setup-EXE` is absent, the installer job did not publish it successfully; use the version-specific artifacts below instead.

### Option B — version-specific package

1. Open a successful workflow run.
2. Download the artifact matching your installed version, for example `ElecKit-Revit-2020` or `ElecKit-Revit-2025`.
3. Extract the package and follow its included manifest/package layout.
4. Ensure the `.addin` manifest points to the correct DLL location for that version.
5. Restart Revit.

The per-user Revit Addins location is typically:

```text
%APPDATA%\Autodesk\Revit\Addins\<RevitVersion>
```

Only install packages from a workflow run whose corresponding build job succeeded. Close Revit before replacing loaded DLLs. If the add-in does not appear, check the manifest path, Windows file permissions, and Revit's add-in load messages.

## Repository layout

```text
BimTools.ElecKit/
├── .github/workflows/       # Multi-version CI build, tests and packaging
├── src/                     # Commands, shared core and services
├── tests/                   # Revit-independent regression tests
├── docs/                    # Validation and technical documentation
├── RevitVersions/            # Version-specific package/output location
├── OtherFiles/               # Supporting CI files and intermediate artifacts
├── EXE/                      # Setup installer source and output location
├── ElecKit.csproj
├── ROADMAP.md
└── README.md
```

Build outputs and the setup EXE are expected to be generated by CI; their presence in the repository is not guaranteed. Download published artifacts from the relevant workflow run.

## Build from source

### Requirements
- Windows
- Visual Studio 2022 or a compatible .NET SDK installation
- Access to restore the required NuGet packages
- Revit installed for live functional testing

### Build one version

From the repository root:

```powershell
dotnet restore ElecKit.csproj -p:RevitVersion=2020
dotnet build ElecKit.csproj -c Release --no-restore -p:RevitVersion=2020
```

Replace `2020` with the Revit version you intend to target. The framework and Revit API references are selected using the `RevitVersion` build property. If you build locally, confirm the project uses the correct SDK and version-matched API packages for your target.

### Build and test in GitHub Actions

The workflow is defined in [`.github/workflows/build-all-versions.yml`](.github/workflows/build-all-versions.yml). It creates a matrix for Revit 2020–2027, runs the Revit-independent tests, stages version-specific packages, and attempts to build the selectable-version setup EXE.

[**Open workflow runs →**](https://github.com/amdghedy/BimTools.ElecKit/actions/workflows/build-all-versions.yml)

CI verifies compilation and automated tests only. It does not launch Revit or prove that every command behaves correctly in a real project.

## Ribbon tools at a glance

| Area | Representative tools |
|---|---|
| Drafting | CAD to Detail Lines, Wires to Detail Lines, Detail Lines to Wires, Connect Runs, Fillet, Split Lines, Clean Lines |
| Wiring & routing | Home Run Lines, Boundary to Conduits, Route Cable Tray, Lines to Cable Tray, Circuit Number Text, Wire Info to Comments |
| Selection & model | Select Hosts, Select Same Type, Smart Filter, Assign Workset, 3D from Selection |
| Coordination | Clash Review, Deployment Audit |
| Schedules & quantities | Export Schedules, Export Element Parameters, Import CSV Values, Quantity Takeoff |
| Circuits | Circuit Report, Select by Circuit, Circuit & Panel Audit |

Names and grouping may evolve as the ribbon and command registry are refined. The roadmap distinguishes implemented work from planned work.

## Logs and troubleshooting

- **Log file:** `%LOCALAPPDATA%\ElecKit\log.txt`
- **Settings file:** `%APPDATA%\ElecKit\settings.json`

When reporting a problem, include:
- Revit version and ElecKit package version/commit.
- Tool name and a short sequence of reproduction steps.
- Active view and relevant selected element categories.
- Expected result and actual result.
- Relevant sanitized log/error text.

Do not attach confidential project models or unredacted project data to public issues.

## Known limitations and release checks

- Some route-obstacle checks use active-view axis-aligned bounding boxes; they are approximate and are not exact solid-clearance analysis.
- Circuit/panel checks depend on the parameters available in the model and should not be interpreted as a complete electrical engineering design validation.
- Schedule import/export supports defined parameter types and workflows; test round-tripping on a copy before applying changes to a production model.
- A Revit-independent test suite cannot validate all model-specific API behavior, geometry creation, transaction handling, or ribbon loading.
- No automated live-Revit integration test currently replaces manual verification in Revit.
- Before release, run the [Revit 2020 integration checklist](docs/REVIT2020_VALIDATION_CHECKLIST.md) and validate other target versions in their respective installed Revit environments.

## Contributing

Bug reports and feature requests are welcome via [GitHub Issues](https://github.com/amdghedy/BimTools.ElecKit/issues). For a proposed tool, describe the repetitive workflow, required inputs, expected model result, and how failures or cancellations should be handled.

For code changes:
1. Follow the existing command patterns under `src/Commands/`.
2. Register commands through `src/Core/ToolRegistry.cs` where appropriate.
3. Keep model modifications transaction-safe and report failures clearly.
4. Add or update Revit-independent tests for logic that can be isolated from Revit.
5. Verify behavior in a disposable model before using geometry-changing commands in production.

---

Maintained by [amdghedy](https://github.com/amdghedy).
