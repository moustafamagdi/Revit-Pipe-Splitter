# Revit Pipe Splitter

Revit 2024 add-in that splits straight pipes to a **Max finished length** after unions, respects a **Min piece length**, and preserves insulation. Current assembly version: **3.0.0.2**. Command: `PipeSplitter2024.Command`.

## Build and use

Run `Build.ps1` with Revit 2024 installed at `C:\Program Files\Autodesk\Revit 2024`. It uses .NET Framework csc with `/langversion:5` and overwrites the single `PipeSplitter2024.dll` beside the sources. No NuGet or SDK migration. Autodesk assemblies are not distributed.

Load that DLL with Add-In Manager (Manual), then run the command in an editable project. Alternatively run the existing `Install.ps1` to register this repository path and restart Revit. Ribbon/copied deployment remains Phase 4 and is not included.

1. **Scope:** choose Scan scope or Pick pipes. Preview scope always starts on Visible rows; All eligible is an explicit alternative.
2. **Limits:** set Max finished length (default 6 m), Min piece length (default 0.5 m), remainder placement and Strict mode. Inline errors disable Scan/Preview/Apply until valid.
3. **Rules:** Open rules, Reload, Insert example into an empty list, or Export combinations from the scan. The JSON format is in RULES-AND-SETTINGS.md. Shadow warnings appear in non-modal Rules notes and are logged once per content change in the current dialog.
4. **Run:** Scan estimates, Preview verifies and rolls back the trial, Apply confirms and uses the exact Verified set.
5. **Results:** Select rows or all Failed/Skipped/Blocked to close the form and zoom in Revit; reopen and Scan again to continue. Export visible CSV follows current filters/sort. Export audit CSV includes all session batches and cancelled/unprocessed pipes.

## Lengths and preserved algorithm

Lengths are actual Revit pipe curve lengths in metres. Full pieces reach Max finished length AFTER unions; remainder pieces may be shorter while respecting Min piece length. The sum of finished lengths can be less than the original route because unions occupy space. No separate stock/weld/insertion allowance is guessed.

End / Start follow the stored curve direction, not flow direction. Balanced equalizes end pieces where feasible. Preview measures family-dependent takeout and retains FinishedTargets/CompensateCuts, with at most eight attempts per pipe. Apply first uses the saved final Preview cuts; failed seeds fall back to the guarded measured cache/iterative path. Every pipe still receives all final connection, metadata, length and insulation checks. The cache is cleared on Scan.

Each pipe is atomic. Preview rolls back its TransactionGroup. Successful Apply changes are assimilated into one Undo step. Strict mode OFF logs/dismisses warnings; ON also rejects warning-producing pipes. New pipe pieces/unions retain source pipe workset and phases. **Insulation uses its ORIGINAL insulation workset and phases**, even when they differ from the host pipe. That difference is logged; new insulation is matched and inherited insulation verified against the insulation baseline. Read-only/mismatched placement rejects only that pipe. Type/thickness checks remain. All of these Revit behaviors need manual verification.

## Verified set, filters and status

The quick Status filter defaults to **Actionable**, and combines with every right-click column filter using AND. Headers have friendly labels but filters/sorts still use the same internal names. Click a header to sort. Numeric length filters compare the displayed three decimals; internal calculations retain doubles. Warnings is a numeric count column with warning text in its tooltip and the row log. Status uses color plus text.

- Actionable: eligible pipes, including retryable failures and unprocessed cancellations.
- Will split: eligible, unverified pipes excluding failures.
- Verified: currently verified rows; stale Preview text alone is not sufficient.
- Skipped / Blocked / Failed / Applied: corresponding status.
- Unchanged: existing within-limit/short pipes and cancelled/unprocessed rows.
- All: every scanned row.

Preview freezes successful IDs and their final cuts. **Apply uses exactly that set, even if later filters hide rows.** Sorting/narrowing cannot shrink it. Introducing eligible IDs outside the requested Preview scope invalidates it. Failed Preview rows are excluded. Changes to limits, rules or scan scope require Scan and Preview again. Execution scope is never saved; other preferences/window size still persist. The UI and confirmation show `Verified set: N pipes`.

The status strip covers the full scan: scanned, to split (eligible), verified, applied, failed, row warnings, resulting pieces and unions. Piece/union totals combine current estimates with verified/applied results. Running counts are provisional until the batch result is known. Rule-shadow notes are separate from row warning counts.

## Progress and cancellation

Preview/Apply display progress and accept Cancel **between pipes**. The current pipe finishes its attempt sequence first; a slow Revit API call cannot be interrupted. API work stays on Revit's main thread. UI messages are pumped only between pipes while toolbar/filter/grid actions and form close are blocked.

- Cancel Preview: roll back the whole trial, clear the Verified set, and require Preview again. Successful trial lengths remain simulated in the audit.
- Cancel Apply: choose **Keep completed pipes (one Undo step)** or **Roll back everything**. Escape/closing that choice means rollback. If no pipe changed, there may be no Undo entry.
- Every remaining target is audited as **Not processed (cancelled)** with original lengths and no output IDs. Applied/rolled-back IDs are finalized only after the TransactionGroup result.
- Kept successful rows become ineligible for repeat Apply. Unprocessed rows remain eligible but need a fresh Preview. Other batches already applied in the session are not rolled back by cancelling the current batch.

## Files and diagnostics

Runtime files live in `%APPDATA%\PipeSplitter`: rules.json, settings.json, log.txt (rotation near 5 MB, one log.txt.bak), and unique per-session audit_yyyyMMdd_HHmmss.csv files. Audits have a UTF-8 BOM once at creation; appended batches add no BOM, supporting Arabic text in Excel. Same-second collisions get a numeric suffix. Existing audit files are never overwritten, including explicit exports.

AssemblyInfo.cs is the sole version declaration. Title, session logs and every audit row read that assembly version; logs/audits also include Revit version/build, Windows user and document path. Audit remains historical after Undo.

New UI code is in SplitForm.Layout.cs, SplitForm.View.cs and SplitForm.Execution.cs. Phase3Policy.cs contains Revit-free filter/validation policy. InsulationPlacement.cs holds the separate insulation baseline. Planner.cs, TakeoutMeasurement.cs and PreviewSnapshot.cs remain unchanged in Phase 3.

## Scope, validation and open risks

Only straight rigid host-model Pipe elements are considered. FlexPipe, Fabrication Parts and links are outside scope. Pinned/grouped/design-option/placeholder/branch pipes are excluded. Union routing must support the size. Existing short pipes remain unchanged. Custom insulation parameters/tags are not explicitly copied; unions are not insulated. Preview rollback does not guarantee worksharing ownership relinquishment.

Run `Test-Planner.ps1` for standalone arithmetic, rules, encoding and UI-policy checks without Revit. **The DLL has not been executed in Revit or visually tested by the assistant.** Actual 100/125/150% display scaling, minimum-window layout, Revit message pumping, cancellation/Undo, placement setters and family-dependent results need manual verification. Layout uses DPI autoscaling, auto-sized rows/groups, a scrollable viewport and fixed-access progress/Cancel and status areas; no absolute table row heights.

See PHASE3-CHANGELOG.md, PHASE3-MANUAL-TEST-PLAN.md and BUILD-RESULT.txt for this handoff. Earlier phase plans are historical where superseded. Phase 4 has not started. All future source edits stay in this repository; fixed filenames are overwritten, not versioned copies.

## Pick pipes fix (3.0.0.1)

Pick pipes now returns from ShowDialog before calling Revit selection, then reopens the same form. This avoids starting selection inside a hidden modal dialog. Pick a bare pipe OR its insulation; insulation resolves to its host pipe and duplicate hosts are removed. Finish selects those host pipes, changes Scan scope to Selected pipes only and requests Scan. Esc/empty selection preserves the previous scan and selection. Fittings, linked elements and non-Pipe hosts remain excluded. The existing audit session, filters, rules and form state are retained. No splitting algorithm change. Needs manual verification in Revit.
## Active-view selection (3.0.0.2)

Scope > Select pipes in active view selects the host pipes returned for the currently active Revit view and changes Scan scope to Pipes in active view. Click Scan next, then Preview/Apply as usual. You can also choose Pipes in active view directly from Scan scope. Scan collects the active view afresh; it does not reuse manual picks. Insulation hits resolve to host pipes and are deduplicated. No linked pipes, fittings or FlexPipe are included. An empty view stays an empty scope; it never falls back to the whole model. The scan-scope preference is saved; Preview scope still always starts Visible rows.

Visibility follows Revit's view-scoped FilteredElementCollector, not screen pixels or the current zoom rectangle. Per Autodesk's installed API documentation, occluded pipes and some elements outside crop boundaries can still be returned. Check crop/section-box, temporary hide/isolate, phase and filter behavior manually in your views before applying. Unsupported views report an error. No geometry/splitting algorithm change; no Revit execution by the assistant.