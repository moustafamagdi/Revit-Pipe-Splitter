# Revit Pipe Splitter

Revit 2024 add-in for splitting straight pipes into finished lengths, adding unions from Routing Preferences, and preserving pipe insulation type and thickness.

Version: **2.0.0.0**. Command: `PipeSplitter2024.Command`.

## Features

- Configurable finished target length (default 6 m) and minimum piece length (default 0.5 m).
- Compensation for actual union takeout so full pieces reach the target after fittings are placed.
- End / Start / Balanced remainder placement while respecting the minimum.
- Model-wide, preselected or interactively picked pipe scanning, estimates, and exact rollback preview.
- Pipe insulation type and thickness preservation on resulting pipe segments.
- Per-column filters/sorting, visible-row execution by default, table export, and committed-ID audit CSV.
- JSON limits per type/system/size, remembered controls and window size.
- Per-pipe rollback on failed validation, with one Undo entry for the completed batch.

## Build

Requirements: Windows, Revit 2024 installed at `C:\Program Files\Autodesk\Revit 2024`, and the .NET Framework C# compiler used by the build script.

```powershell
.\Build.ps1
```

Output: `PipeSplitter2024.dll` beside the source files. Autodesk assemblies are referenced from the local installation and are not distributed.

## Run

1. Open an editable Revit project (not the Family Editor).
2. In Add-In Manager (Manual), load `PipeSplitter2024.dll`.
3. Run `PipeSplitter2024.Command`.
4. Set target/minimum, remainder placement, scan scope and Apply to. Use Pick pipes in Revit if needed.
5. Run Scan, inspect effective rule limits, then Preview and Apply. Apply uses exactly the successfully verified Preview set, including rows hidden by later filters. Adding eligible rows or changing settings/rules requires a new Preview (and Scan after settings/rule changes).

For optional persistent installation, keep this repository folder in a stable location and run `Install.ps1`. Restart Revit and choose Add-Ins > External Tools > Pipe Splitter. The script registers this DLL path in the current user's Revit 2024 add-ins directory.

## Lengths and preview

Lengths are actual Revit pipe curve lengths in metres. Preview creates pipe segments and unions in temporary transactions, measures the resulting geometry, compensates cut positions, verifies constraints, and rolls back the trial. It can attempt compensation up to eight times per pipe. Apply first tries each saved final verified Preview cut plan. If it fails, the guarded cache/iterative path is used; errors from failure processing and regeneration failures remain fatal for that pipe. A dialog-local cache of measured takeout per pipe type/size seeds subsequent trials; every pipe is verified again, and a wrong cache estimate falls back to iterative compensation. The cache is cleared on a new Scan. These Revit behaviors need manual verification.

Full pieces target the specified finished length. Remainder pieces may be shorter to respect the minimum. The sum of finished pipe lengths may be less than the original route length because unions occupy part of the route. No separate stock, weld-gap, or insertion-depth allowance is calculated beyond the union family's actual Revit geometry.

Preview can be slow on large models and runs synchronously. Use Selected pipes only for smaller batches.

## Filtering and sorting

Click a column header to toggle sorting; right-click to set its filter. Multiple column filters are combined with AND. Text matching ignores case. Numeric length filtering uses the three decimal places shown in the table. CSV export follows the visible rows and sort order.

**Apply to always starts on Visible rows and is never persisted. Preview processes eligible rows in the chosen scope. Apply uses the fixed successful Preview ID set shown as Verified set: N pipes, even if later filters hide some. Including eligible IDs outside the requested Preview scope invalidates that set. Confirmation states the exact count. Summary totals cover the whole scan and include warnings.**

## Scope and limitations

- Straight rigid Pipe elements in the current model, including sloped pipes.
- Linked models, FlexPipe, and Fabrication Parts are not included.
- Pinned pipes, group members, design-option elements, placeholders, and pipes with branch/tap connectors are skipped.
- Existing pipes shorter than the minimum remain unchanged and are flagged.
- Suitable Union routing rules and loaded families must exist for the pipe size.
- Pipe insulation type and thickness are preserved; custom insulation parameters and tags are not explicitly copied. New unions are not insulated by this tool.
- Errors and failed constraints roll back the affected pipe. Warnings are logged and dismissed by default; Strict mode (default OFF) also rolls back warning-producing pipes. Failure processing needs manual verification.
- Preview rollback does not guarantee relinquishment of worksharing ownership.

## Validation status

The current add-in compiles with the .NET Framework csc compiler and /langversion:5 against the local Revit 2024 API. Test-Planner.ps1 compiles and runs an isolated console harness for Planner: deterministic edge cases, 50,000 seeded allocations, and 10,000 synthetic takeout cases, plus 30,000 phase-2 placement/compensation cases and rules/settings JSON round-trips. These are standalone checks, not Revit validation. The DLL has **not been run or tested inside Revit** by the assistant, per the user's instruction. Actual family-dependent behavior remains for user validation.

## Repository files

- `PipeSplitter.cs`: external command, Revit operations, and English interface.
- `Planner.cs`: length allocation and union compensation arithmetic.
- `Build.ps1`: builds the DLL using local Revit references.
- `Install.ps1`: optional per-user add-in registration.

Generated DLLs and build artifacts are ignored by Git.

## Development workflow

This repository is the source of truth for all future changes. Build.ps1 overwrites the single output PipeSplitter2024.dll. Do not create version-suffixed DLLs or release folders. If Revit locks the file, close Revit and rebuild with the same filename. The assistant must not run or test the DLL inside Revit unless the user changes that instruction.

## Phase 1 handoff

Phase 1 is retained and Phase 2 is now implemented. Phase 3 and Phase 4 require explicit user confirmation. The Phase 1 documents are historical; their old display-only-filter expectations are superseded by Phase 2.

- See PHASE1-CHANGELOG.md for each behavior change and its reason.
- See MANUAL-TEST-PLAN.md: every Revit API behavior is **needs manual verification**.
- Phase1Support.cs contains focused correctness helpers; the Phase 4 file restructuring is deferred.
- Row logs include callback warnings and exception details. The diagnostic file rotates at approximately 5 MB, retaining one log.txt.bak backup. The current file is `%APPDATA%\PipeSplitter\log.txt`.
- Routing Preferences are prechecked by pipe type/diameter before splitting. A matching rule does not prove that a family can be placed.
- New unions and pieces receive the original workset and creation/demolition phases. Final validation compares pipe type, system type, reference level, diameter, phases, workset and insulation.
- Point coincidence uses Application.VertexTolerance. Scalar length tolerance is Application.ShortCurveTolerance / 100 (converted to metres as needed). This is an explicit acceptance policy needing manual verification, not a minimum creation distance. Planned pieces must exceed ShortCurveTolerance; input minimum remains greater than twice ShortCurveTolerance.
- UI lengths have 3 decimal places; calculations retain doubles, and individual CSV segment lengths use round-trip formatting.

Run the arithmetic harness without loading Revit:

```powershell
.\Test-Planner.ps1
```

A successful build or console harness does not establish Revit correctness. Cache acceleration, warning handling, metadata propagation, placement and transaction/undo behavior all remain unverified in Revit.
## Phase 2 handoff

- PHASE2-CHANGELOG.md explains the functional changes and open risks.
- PHASE2-MANUAL-TEST-PLAN.md adds the manual Revit scenarios; all remain unverified.
- RULES-AND-SETTINGS.md documents rule precedence, settings, placement, scope and audit semantics.
- rules.example.json contains disabled examples. Open rules JSON in the UI and adapt them to actual model names.
- Phase2Settings.cs provides validated .NET JSON persistence; Phase2Support.cs provides picking/audit helpers; Phase2Form.cs extends the form with Phase 2 controls. Phase 4 restructuring is still deferred.
- Test-Planner.ps1 exercises pure Planner/rule/settings code without referencing Revit, and writes only into its test output folder.
- Export audit CSV includes all attempts in this dialog. A unique APPDATA/PipeSplitter/audit_yyyyMMdd_HHmmss.csv is created for each dialog session with operations and appended after each batch (collision suffix if needed); Preview IDs are omitted because they no longer exist after rollback.

DLL name remains PipeSplitter2024.dll. The assistant has not run it in Revit. UI responsiveness/cancellation/DPI work remains Phase 3.
## Phase 2 review fixes (2.0.0.0)

See PHASE2-CHANGELOG.md and PHASE2-MANUAL-TEST-PLAN.md for the review fixes and required checks. No Phase 3 layout redesign was performed.

Unknown rule fields now block loading with the field and rule name; shadowed rules produce warnings. Export scan combinations provides exact selector names and sizes. Insert example rule adds a disabled template only to an empty list.

New insulation receives the source pipe workset/phases; all inherited insulation is verified against them. Any mismatch rejects the pipe. Select in Revit also zooms; the dialog closes and you must Scan again when reopening. These API behaviors need manual verification.

Each log session and every audit row includes tool version, Revit version/build, Windows user and document path. Audit files are never overwritten, including explicit exports (choose a new filename). TakeoutMeasurement.cs, PreviewSnapshot.cs, SessionFiles.cs and SessionInfo.cs are Revit-free helpers covered by the standalone harness.