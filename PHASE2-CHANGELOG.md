# Phase 2 changelog - Functional improvements

Phase 2 is implemented for review. Phases 3 and 4 have NOT started. All Revit API behaviors need manual verification.

| Change | Why / behavioral effect |
| --- | --- |
| End / Start / Balanced remainder placement in Scan, measured FinishedTargets and cached takeout seeding. | Placement is applied consistently before and after actual union takeout. Balanced equalizes ends while retaining full middle pieces when feasible. Start/End follow the stored curve direction, not system flow. End remains the default. |
| Validated JSON rules per pipe type, system type and nominal size in APPDATA/PipeSplitter/rules.json. | Allows different stock/finished limits per class of pipe. First matching enabled rule wins; global max/min remain fallbacks. Effective limits and rule are visible in the grid. Invalid files block a new scan. |
| Open rules JSON and Reload rules controls; documented format and disabled example file. | Makes configuration accessible without adding a full editor or external packages. Rule values are snapshotted per scanned row so Preview/Apply cannot silently use different file contents. |
| APPDATA settings.json with validated controls and normal/maximized window size. | Restores last-used settings. Invalid settings show a warning and use defaults; selections and grid filters are not persisted across documents. |
| Pick pipes in Revit. | Hides the dialog for PickObjects with a Pipe-only ISelectionFilter, restores it afterwards, and retains prior selection on Esc. No model transaction is opened for picking. |
| Select rows in Revit and Select all Failed/Skipped (including Blocked). | Closes the form and sets selection after ShowDialog returns. Applied rows select their resulting pipes; other rows select the original. Missing IDs are ignored. |
| Apply to: Visible rows / All eligible, default Visible rows for fresh preferences. | Both Preview and Apply snapshot the chosen eligible scope. Apply requires all candidates to have passed Preview and confirms the exact count. Expanding scope cannot silently apply unpreviewed rows. |
| Per-row preview validity and subset Apply. | Hidden eligible rows stay untouched. Successful rows become ineligible for repeat Apply; remaining rows require fresh Preview after a committed batch because shared connections may change. |
| Audit CSV with original-to-result/new-pipe/union IDs, effective rules, before/after lengths, warnings and outcome. | IDs become authoritative only after group commit. Preview and rolled-back IDs are deliberately omitted. Automatic audit.csv survives closing the form to select elements; explicit export retains a copy. |
| Post-commit display exceptions are distinguished from transaction rollback. | Avoids falsely claiming that already committed model work was rolled back if UI/report refresh fails. |
| Summary includes previously applied rows in the current scan. | Prevents subset Apply from reducing displayed resulting-piece totals to original counts. |
| Planner harness extended with 30,000 placement/compensation cases and rules/settings serialization checks. | Covers new pure calculation and persistence logic without loading Revit or touching production APPDATA. |

The Phase 1 core is retained: per-pipe atomic transactions; measured union takeout; compensation and guarded cache; metadata, connections and insulation validation; strict warning policy; group-rollback Preview; single-undo Apply; fixed filename PipeSplitter2024.dll; C# 5 build.

## Open risks

- DLL was not run in Revit. Modal hide/pick/re-show behavior and selection-after-close require manual verification.
- Balanced/Start targets and asymmetric union compensation need real family-dependent verification.
- System matching is by system TYPE name. Verify names/diameters from the grid before enabling rules.
- Unknown JSON fields are ignored by the serializer; use exact documented field names. Rule snapshot changes require a new Scan.
- A Status filter may hide successful rows after Preview; the execution scope follows the then-visible table. Adjust filters or choose All eligible explicitly.
- Saved All eligible overrides the fresh-install Visible rows default, so always check the displayed scope.
- Automatic audit.csv retains the current dialog's batches, not permanent multi-session history; export it before another session overwrites it.
- Preview remains synchronous. Cooperative Cancel/progress and DPI-safe layout are Phase 3; the current extra controls still need visual verification on the user's display.
- This phase does not add a ribbon or copied deployment. Those remain Phase 4.
