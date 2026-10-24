# Revit 2020 Integration Validation Checklist

**Purpose:** verify actual Revit API behavior before this branch is treated as release-ready. GitHub CI compiles against the Revit 2020 API package and runs pure .NET tests; it does not launch Revit or prove model/runtime behavior.

## Test setup

- [ ] Use a **disposable copy** of a test project. Do not run forced-failure/rollback tests on a production model.
- [ ] Record Revit full build number, Windows user, repository commit, add-in DLL path and test-model filename.
- [ ] Build Debug with Revit 2020 installed; confirm deployment copies ElecKit.dll and ElecKit.addin to %AppData%\Autodesk\Revit\Addins\2020.
- [ ] Restart Revit after deployment. Run **Deployment Audit** and save the report/log.
- [ ] Confirm the manifest's resolved Assembly path equals the expected deployed DLL path and hashes match when running the deployed copy.

## Circuits and panels

- [ ] Assign a circuit with matching known voltage/poles to a compatible test panel; confirm Revit accepts the assignment.
- [ ] Attempt known voltage mismatch; verify assignment is blocked before the transaction.
- [ ] Attempt known pole-count mismatch; verify assignment is blocked before the transaction.
- [ ] Configure a test panel with explicit capacity 10,000 VA and an existing circuit load of 8,000 VA; attempt adding a 2,500 VA circuit; verify projected 10,500 VA is blocked.
- [ ] Test exactly-at-capacity load; verify it is not blocked by the numeric capacity rule.
- [ ] Remove/rename one of the explicit rating parameters; verify the command reports review warnings and does not assume missing existing load is zero.
- [ ] Run **Circuit & Panel Audit**; compare panel totals with the actual panel schedule. Confirm all custom parameter units and the apparent-load parameter units in the actual project.
- [ ] Generate the panel single-line diagram and compare each listed circuit/panel against the native Revit panel schedule. Treat the view as schematic until this comparison passes.

## Cable tray routing and fittings

- [ ] Route a direct segment and an L-shaped route. Confirm expected level, offset, tray type, connectors and elbow.
- [ ] Route a multi-segment path with at least two corners. Confirm elbows appear at turns and the full path rolls back if one required fitting is rejected.
- [ ] Use **obstacle-aware routing** in a small, known test area with a wall/equipment obstacle. Test 0 mm and nonzero clearances; confirm the route detours and inspect actual geometry. Bounding-box avoidance is not exact-solid clearance.
- [ ] Create a **Cable Tray Branch Layout** from a horizontal main, vertical main, and a branch anchor projected inside the main. Confirm the tee joins all three segments.
- [ ] Create a **Cable Tray Riser** in a 3D view with vertically aligned endpoints. Confirm Revit creates a truly vertical segment and verify endpoint elevations.
- [ ] Connect two tray ends with an elbow. Test transition/reducer using different width/height types; confirm same-size transition is rejected and unknown dimensions request confirmation.
- [ ] Connect three tray ends with a tee and four with a cross. Confirm connector ordering, type routing preferences and physical continuity.
- [ ] Select separated or incompatible connectors; confirm Revit rejection leaves no partial fitting or unintended geometry.
- [ ] Convert straight detail/model lines to cable tray; include an arc and a deliberately invalid/short segment. Confirm unsupported lines are reported and source lines are deleted only after successful conversion when deletion was requested.
- [ ] Convert an open boundary and a closed boundary to cable tray. Confirm every required elbow exists; force a fitting failure in the disposable model and confirm the affected run is rolled back.
- [ ] Repeat a required-elbow failure for **Boundary to Conduits** and **Connect Runs**; confirm failed geometry is removed and the report/log counts match.

## Schedule exchange, quantity takeoff and filtering

- [ ] Export/import an itemized single-category schedule containing writable instance parameters and type parameters. Compare values before/after.
- [ ] Include ElementId-valued and numeric parameters; confirm round-trip values are interpreted correctly in the actual model.
- [ ] Create conflicting edits to a type parameter shared by two instances; confirm import rejects the conflict before writing.
- [ ] Confirm grouped, multi-category, calculated and combined schedules are rejected with a clear message rather than silently exported as editable rows.
- [ ] Run Quantity Takeoff for electrical-only and expanded MEP scope. Verify category audit counts, quantities, levels and millimetre lengths against known elements, including empty categories and a category scan failure.
- [ ] Test Smart Filter with multiple AND/OR rules, both matching and nonmatching elements, selection-only mode and temporary isolation reset.
- [ ] Test host-host, host-linked and linked-linked clash checks with known intersections. Verify confirmed solid intersections versus unavailable/incomplete geometry checks; do not interpret bounding-box distance as exact solid clearance.

## Transaction and reliability evidence

- [ ] Trigger a controlled command failure. Confirm command failure, transaction rollback, elapsed duration and exception details are written to %LocalAppData%\ElecKit\log.txt.
- [ ] Confirm no failed route/fitting leaves partial model elements after rollback.
- [ ] Run Deployment Audit from the deployed DLL and from a development output folder; verify the expected pass/mismatch behavior.
- [ ] Preserve the generated CSV/text reports and relevant screenshots in the release evidence folder.

## Sign-off record

| Field | Value |
|---|---|
| Revit 2020 full build | |
| Tested commit SHA | |
| Test model | |
| Tester/date | |
| Passed checks / total | |
| Failed checks and issue links | |
| Evidence folder | |

**Release rule:** do not mark the PR ready or merge it until all applicable checks above pass in Revit 2020, failures are fixed or explicitly scoped out, and the evidence is attached to the PR.