# Revit Pipe Splitter

Revit 2024 add-in for splitting straight pipes into finished lengths, adding unions from Routing Preferences, and preserving pipe insulation type and thickness.

Current version: **1.3.0**. Command: `PipeSplitter2024.Command`.

## Features

- Configurable finished target length (default 6 m) and minimum piece length (default 0.5 m).
- Compensation for actual union takeout so full pieces reach the target after fittings are placed.
- Redistribution of short remainders while respecting the minimum.
- Model-wide or preselected-pipe scanning, estimates, and exact rollback preview.
- Pipe insulation type and thickness preservation on resulting pipe segments.
- Per-column numeric/text filters, ascending/descending sorting, and visible-row CSV export.
- Per-pipe rollback on failed validation, with one Undo entry for the completed batch.

## Build

Requirements: Windows, Revit 2024 installed at `C:\Program Files\Autodesk\Revit 2024`, and the .NET Framework C# compiler used by the build script.

```powershell
.\Build.ps1
```

Output: `PipeSplitter2024_v1_3.dll` beside the source files. Autodesk assemblies are referenced from the local installation and are not distributed.

## Run

1. Open an editable Revit project (not the Family Editor).
2. In Add-In Manager (Manual), load `PipeSplitter2024_v1_3.dll`.
3. Run `PipeSplitter2024.Command`. Confirm the window title shows v1.3.
4. Set finished target and minimum lengths, then select the scan scope.
5. Run Scan, Preview, and Apply in order.

For optional persistent installation, keep this repository folder in a stable location and run `Install.ps1`. Restart Revit and choose Add-Ins > External Tools > Pipe Splitter. The script registers this DLL path in the current user's Revit 2024 add-ins directory.

## Lengths and preview

Lengths are actual Revit pipe curve lengths in metres. Preview creates pipe segments and unions in temporary transactions, measures the resulting geometry, compensates cut positions, verifies constraints, and rolls back the trial. It can attempt compensation up to eight times per pipe. Apply starts from the successful preview's cut positions and validates again.

Full pieces target the specified finished length. Remainder pieces may be shorter to respect the minimum. The sum of finished pipe lengths may be less than the original route length because unions occupy part of the route. No separate stock, weld-gap, or insertion-depth allowance is calculated beyond the union family's actual Revit geometry.

Preview can be slow on large models and runs synchronously. Use Selected pipes only for smaller batches.

## Filtering and sorting

Click a column header to toggle sorting; right-click to set its filter. Multiple column filters are combined with AND. Text matching ignores case. Numeric length filtering uses the six decimal places shown in the table. CSV export follows the visible rows and sort order.

**Filters affect the table and CSV only. Preview, Apply, and summary totals use the full scan scope, including hidden rows.**

## Scope and limitations

- Straight rigid Pipe elements in the current model, including sloped pipes.
- Linked models, FlexPipe, and Fabrication Parts are not included.
- Pinned pipes, group members, design-option elements, placeholders, and pipes with branch/tap connectors are skipped.
- Existing pipes shorter than the minimum remain unchanged and are flagged.
- Suitable Union routing rules and loaded families must exist for the pipe size.
- Pipe insulation type and thickness are preserved; custom insulation parameters and tags are not explicitly copied. New unions are not insulated by this tool.
- Failed constraints or Revit transaction warnings roll back the affected pipe.
- Preview rollback does not guarantee relinquishment of worksharing ownership.

## Validation status

Version 1.3 was compiled against the local Revit 2024 API. Standalone arithmetic checks covered compensation, remainder redistribution, and infeasible constraints. The DLL has **not been run or tested inside Revit** by the assistant, per the user's instruction. Actual family-dependent behavior remains for user validation.

## Repository files

- `PipeSplitter.cs`: external command, Revit operations, and English interface.
- `Planner.cs`: length allocation and union compensation arithmetic.
- `Build.ps1`: builds the DLL using local Revit references.
- `Install.ps1`: optional per-user add-in registration.

Generated DLLs and build artifacts are ignored by Git.
