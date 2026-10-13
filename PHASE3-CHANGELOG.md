# Temporary original-insulation creation workset - 3.0.0.4

- Added ActiveWorksetScope.cs. Capture the user's current active workset; validate the source insulation workset exists and is WorksetKind.UserWorkset; activate it before the first cut. Keep it active through union creation and missing-insulation creation. Do not guess/map invalid IDs or change the source pipe to the insulation's workset.
- Restore before per-pipe commit, in finally for every attempt/retry/rollback, at Process exit, and after the TransactionGroup is disposed. Each restoration reads back and verifies the ID. Persistent failures surface to the batch error handler instead of continuing silently. Non-workshared documents skip this switch.
- Keep final insulation matching/verification, pipe/union metadata, connections, measured takeout, compensation, preview snapshot and one-Undo behavior. Arithmetic unchanged. This is a creation-context change, not a relaxation of placement requirements.
- Log before/target active workset IDs and target name, created/inherited insulation expected/actual placement even when equal, and restoration. Separate missing parameter from read-only errors.
- Original insulation Workset 0 is checked through the same document table and kind rules. If not a valid user workset, its pipe fails before cutting with an explicit reason. No unsupported assignment is forced.
- Full changed/new files: ActiveWorksetScope.cs, InsulationPlacement.cs, PipeSplitter.cs, SplitForm.Execution.cs, Build.ps1, AssemblyInfo.cs, README.md, PHASE3-CHANGELOG.md, PHASE3-MANUAL-TEST-PLAN.md, BUILD-RESULT.txt.
- Build: C# 5, fixed PipeSplitter2024.dll, 0 errors/warnings. Existing Revit-free harness passes 1,106,656 assertions. No DLL/Revit execution. The harness does NOT validate active-workset API behavior or insulation inheritance.
- Open risk: last audit's common resulting Workset 38547 suggested an active-workset creation effect; it did not prove the source. Actual inheritance, phase/workset mutability, restoration across commit/group rollback and cancellation must be verified in Revit. Read-only/ownership restrictions are not bypassed. Old unsupported-workset cases may still fail intentionally.

---
# Insulation repair and right-click menu - 3.0.0.3

Evidence: user-supplied audit_20261013_100137 export reported 139 Preview insulation-placement failures, two worksharing failures and 63 successful committed Apply rows. Screenshot also reported ObjectDisposedException for ContextMenuStrip during header right-click.

- PreserveInsulation now attempts placement matching on INHERITED insulation as well as newly created insulation. The ORIGINAL INSULATION baseline remains authoritative, even when different from the pipe's placement. Existing type/thickness guards, final regeneration and verification remain.
- Matching fields are untouched. Demolition is temporarily cleared only when creation phase must change, avoiding an unnecessary clear/set when only demolition differs. Missing/read-only/setter failures still reject only the current pipe; no delete/recreate or ownership workaround.
- Diagnostics log differing field names and expected/actual values before repair, successful matching, and the field being assigned on failure. Final mismatch reports show actual differences rather than only expected values. RegenerationFailedException is rethrown without further model inspection in the matching helper.
- One persistent, form-owned ContextMenuStrip replaces per-click creation/disposal in Closed. Actions use BeginInvoke after closing so the menu framework can finish before opening a filter dialog or rebinding the grid. Disposal happens on form disposal, including correct reuse through Pick pipes. Queued actions guard running/disposed/hidden/pick-request states and report exceptions locally.
- No changes to Planner, takeout/compensation, Verified set, per-pipe transactions, preview rollback or one-Undo Apply. Ownership failures remain outside this fix.
- C# 5 build: zero errors/warnings. Standalone harness: 1,106,656 assertions passed. No Revit or WinForms execution; fixes are NOT runtime-verified and the 139 failures are not claimed resolved yet.
- Complete changed/new files: InsulationPlacement.cs, PipeSplitter.cs, SplitForm.ColumnMenu.cs, SplitForm.View.cs, Build.ps1, AssemblyInfo.cs, README.md, PHASE3-CHANGELOG.md, PHASE3-MANUAL-TEST-PLAN.md and BUILD-RESULT.txt.

---
# Active-view pipe selection - 3.0.0.2

- Added Scope > Select pipes in active view and Scan scope > Pipes in active view. The button updates Revit selection without closing or zooming the view, then requests Scan.
- Shared active-view collector handles Pipe and visible PipeInsulation hosts, deduplicates IDs, excludes linked pipes/fittings/FlexPipe, and validates that the view supports element iteration. Scan recollects the current view independently of previous manual picks. Empty view returns an empty scan; no all-model fallback.
- Added backward-compatible ActiveViewOnly scan preference; previous settings default false. Preview scope is still not persisted. Existing verified-set invalidation and split algorithm remain unchanged.
- C# 5 build passed with zero errors/warnings; 1,106,656 standalone assertions passed including active-view setting round-trip/default checks. No Revit execution.
- Manual risks: Revit collector visibility may include occluded or outside-crop elements (documented in installed RevitAPI.xml); this is not a screen-pixel/zoom selection. Verify hide/isolate, view filters, crop and section box behavior.
- Complete changed files: AssemblyInfo.cs, SplitForm.Layout.cs, SplitForm.Selection.cs, Phase2Form.cs, Phase2Settings.cs, PipeSplitter.cs, tests/PlannerTests.cs, README.md, RULES-AND-SETTINGS.md, PHASE3-CHANGELOG.md, PHASE3-MANUAL-TEST-PLAN.md, BUILD-RESULT.txt.

---
# Pick pipes fix - 3.0.0.1

- The previous Click handler called Hide then PickObjects while still inside ShowDialog. The new request exits the modal dialog first; the external command performs picking and reopens the same form. No external event/modeless conversion or splitting-algorithm change.
- Accept PipeInsulation clicks and resolve HostElementId to a Pipe; deduplicate pipe/insulation clicks. Keep fittings/linked elements/FlexPipe excluded.
- Finish updates selected-only scope and invalidates Scan. Esc and empty picks preserve prior scan/verification. Reuse the same form/audit session and skip settings-save during the temporary picking handoff.
- New UI code: SplitForm.Selection.cs. Changed: PipeSplitter.cs, Phase2Form.cs, Phase2Support.cs, SplitForm.Layout.cs, Build.ps1, AssemblyInfo.cs and current documentation/build report. Full files in the fixed-name handoff archive.
- Build: C# 5 success, zero compiler errors/warnings; existing standalone harness passes 1,106,654 assertions. Neither the DLL nor picking was executed in Revit. Modal ownership, returning focus, selection and insulation-hit behavior need manual verification.
- Reference: [Microsoft ShowDialog lifecycle](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.showdialog). InsulationLiningBase.HostElementId also checked in installed Revit 2024 RevitAPI.xml. These references do not establish Revit runtime correctness.

---
# Phase 3 changelog - carry-over fixes and UI/UX

Version 3.0.0.0. Scope: requested A1-A4, then B1-B8. No Phase 4 work and no Revit execution. Core splitting arithmetic and verification sequence are preserved; Planner.cs, TakeoutMeasurement.cs and PreviewSnapshot.cs are unchanged.

## A - carry-over correctness fixes

| Item | Change and reason |
| --- | --- |
| A1 | Automatic session audit writes EF BB BF only on exclusive file creation. Appends remain BOM-free. Harness checks raw BOM, exactly one BOM after several appends and the expected Arabic Unicode code points. This supports Excel's encoding detection. |
| A2 | Capture ORIGINAL insulation workset/CreatedPhaseId/DemolishedPhaseId before each trial's cuts. New insulation uses that baseline; inherited insulation is verified against it. Differences from the host are logged and intentionally preserved. Missing/read-only setters produce a contextual insulation error and roll back only that pipe. |
| A3 | AssemblyInfo.cs is the single version declaration. SessionInfo reads its own assembly version; title/log/audit use that value. The fixed filename stays PipeSplitter2024.dll. File-version metadata follows the assembly version. |
| A4 | Rule shadow warnings are non-modal Notes, logged once per rules-content change in a dialog. Repeated Scan/Reload does not duplicate the same notice. Other validation errors still block actions and can report errors normally. |

## B - Phase 3 UI/UX

| Item | Change and reason |
| --- | --- |
| B1 | Replace crowded rows with labeled Scope, Limits, Rules, Run and Results groups. Preserve all existing actions. Rename the execution selector to Preview scope to make clear that Apply uses the saved Verified set. Visible rows remains the startup default and is not saved. |
| B2 | DPI autoscaling, auto-sized rows/groups/filter/cancellation dialogs, wrapping toolbar and status strip, and a scrollable content viewport. No absolute table row heights. Progress/Cancel and KPIs stay outside the scrolling content. Actual 100/125/150% display behavior still needs manual verification. |
| B3 | Friendly headers retain internal property names for filtering/sorting. Add a numeric Warnings column with warning text tooltips. Status colors supplement text: green verified/applied, red failed, amber blocked/warnings, grey non-actionable. |
| B4 | Quick Status choices exactly as requested; Actionable is default. It combines with column filters. Actionable includes retryable failures and unprocessed cancellations; Will split excludes verified/failure rows. Narrowing does not shrink the snapshot; new eligible IDs invalidate it. |
| B5 | Progress and cooperative Cancel checked between pipes; all API calls remain on Revit's main thread. Disable toolbar/filter/grid actions, guard action handlers and Run/Scan, and block form close while running. Preview cancellation rolls back all trials and discards verification. Apply cancellation asks Keep completed pipes (one Undo step) or Roll back everything; Escape/X chooses rollback. Audit every unprocessed target explicitly. |
| B6 | ErrorProvider validates raw Max/Min text and range/order/Revit safety limit; invalid inputs disable Scan, Preview and Apply. Consistent term Max finished length throughout current UI/docs. Compatible JSON key MaximumM is unchanged. |
| B7 | Compact status strip for scanned, to split, verified, applied, failed, row warnings, resulting pieces and unions. Counts cover the full scan, not just filtered rows. Estimates and trial counts are provisional until the group result. |
| B8 | Tooltips on interactive controls/headers/status indicators and a short How it works area covering Scan > Preview > Apply, frozen IDs, rules, curve direction and close/zoom/rescan. |

Cancellation only affects the CURRENT batch. With Keep, committed successes become ineligible; failed/unprocessed pipes remain eligible and require Preview again. With rollback, no current-batch output IDs are reported as committed. Preview audit lengths remain simulated. If no successful change exists, Revit may create no Undo entry. The per-pipe transaction, measured takeout, eight-attempt limit, saved-plan-first/cache/iterative fallback, final connection/metadata/insulation checks, preview TransactionGroup rollback and Apply Assimilate are retained.

New files: AssemblyInfo.cs, InsulationPlacement.cs, Phase3Policy.cs, SplitForm.Layout.cs, SplitForm.View.cs, SplitForm.Execution.cs. UI construction, filtering/validation and batch UI orchestration moved into these new partial-class files rather than expanding the old form file. This is the requested UI separation, not the Phase 4 wholesale restructuring.

Other changed complete files: PipeSplitter.cs, Phase1Support.cs, Phase2Form.cs, SessionFiles.cs, SessionInfo.cs, Build.ps1, Test-Planner.ps1, tests/PlannerTests.cs, README.md, RULES-AND-SETTINGS.md, BUILD-RESULT.txt. This changelog and PHASE3-MANUAL-TEST-PLAN.md are added. Historical Phase 2 documentation gets a supersession note.

## Validation and open risks

- See BUILD-RESULT.txt for final compiler/harness results. The harness covers arithmetic, snapshot/filter interactions, limit policy, strict rules, audit encoding/appends/collisions and log rotation without loading Revit or WinForms.
- No DLL execution in Revit and no visual/DPI validation occurred. The user's 100/125/150% and minimum-window tests are required before claiming no clipping.
- The UI pumps Windows messages only between pipes. Revit host compatibility, ownership changes and re-entry protection need manual verification. Cancel cannot interrupt a slow API call/current pipe.
- Insulation phase/workset mutability and BreakCurve inheritance vary by model/API behavior. Mismatches intentionally reject the affected pipe rather than alter its original insulation baseline.
- Shadow warnings are deduplicated within the current dialog; opening a new dialog can report them again. Row warning KPIs do not include rule-shadow notes.
- Existing logging/audit disk-permission failures remain reported. Completed model work is not claimed rolled back merely because reporting fails. Excel rendering of Arabic needs user verification despite the passing byte/Unicode harness checks.
- Phase 4 deployment/ribbon/restructuring is not included. Stop here for visual and Revit results.
