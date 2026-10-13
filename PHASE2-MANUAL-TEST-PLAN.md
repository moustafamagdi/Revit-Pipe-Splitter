> Historical Phase 2 document. Phase 3 carry-over fixes supersede host-based insulation placement; UI/cancellation and current verification expectations are in PHASE3-CHANGELOG.md and PHASE3-MANUAL-TEST-PLAN.md.

# Phase 2 review fixes - manual verification (2.0.0.0)

**Every scenario below: NEEDS MANUAL VERIFICATION. The assistant has not executed the DLL in Revit. Compilation and the Revit-free harness do not validate Revit API behavior.**

Use a disposable model copy. Retain the Phase 1 manual plan for prior coverage. This plan supersedes earlier Phase 2 expectations about subset Apply, persisted scope and overwritten audit.csv. Phase 3 is not started.

| # | Action | Expected behavior needing manual verification |
| --- | --- | --- |
| 1 | Open the command and inspect title, assembly and log session header. | Version 2.0.0.0; Revit version/build, Windows user and Document.PathName recorded correctly. Unsaved documents may have an empty path. |
| 2 | Preview a multi-piece pipe, then Apply unchanged. | Log identifies saved verified-plan first attempt; no extra initial raw trial; actual finished lengths meet targets after unions. |
| 3 | Use a family/condition where a seeded plan fails or final measured targets differ. | Trial rolls back and guarded cache/iterative fallback executes, with all final checks. Failure-processing errors and regeneration errors reject the pipe. |
| 4 | Preview several pipes with mixed success/failure. | Verified set counts only successful rows; Apply operates exactly on those IDs, never failed/unverified rows. |
| 5 | Narrow filters or change sort after Preview, including Status filter hiding successes. | Verified set and Apply count stay unchanged; confirmation explicitly includes hidden verified rows. |
| 6 | Widen filters or switch to All eligible, introducing previously unrequested eligible IDs. | Snapshot cleared and Apply disabled until a new Preview. Switching labels without introducing new eligible IDs need not invalidate. |
| 7 | Change max/min, strict mode, remainder, scan selection or rules; also edit rules externally before Apply. | Scan/Preview invalidated; file changes caught before execution. No stale cut plans used. |
| 8 | Close on All eligible, reopen; test legacy settings containing AllEligible=true. | Always Visible rows; other preferences/window size restore. Saved file no longer contains execution scope. |
| 9 | Misspell a root member and then a rule member (including MaximumM); test direct unknown selector. | Loading blocked; error names field and root or rule name/index. No silent default matching. |
| 10 | Put broad enabled rule before specific rule; reverse order; disable broad rule. | Correct shadow warning for the covered rule only; first-match precedence unchanged. |
| 11 | Export scan combinations with grid filters active. Empty rules list: insert example; repeat with nonempty list. | CSV covers all scanned rows with correct type/system TYPE/nominal diameter/count. Example disabled until edited; nonempty rules not overwritten. |
| 12 | Preview then Apply in one dialog; reopen and run again rapidly. | One timestamped audit per session with operations, batches appended once, no prior session overwritten; collision suffix if same second. Every row has correct session metadata. |
| 13 | Export audit to an existing filename; inspect Preview, failed and committed rows. | Existing file refused. Preview/rollback IDs empty, simulated lengths labelled; committed result/union IDs resolve and align with lengths. |
| 14 | Prepare log above 5 MB, run command, repeat with prior backup present. | Rotation to log.txt.bak, only one backup; new log retains session metadata. I/O errors surfaced without claiming model rollback after commit. |
| 15 | Split insulated pipes on non-active worksets and different creation/demolition phases. | Newly created insulation matches original pipe placement; inherited insulation verified. Type/thickness preserved. |
| 16 | Test inherited insulation with a placement mismatch, read-only placement or unavailable phase. | Entire pipe rolls back on mismatch; no leftover unions/pieces/insulation. Confirm failure identifies offending element. |
| 17 | Warning-only operation, Strict OFF then ON, including batch rollback. | Default accepts warning with '(n warnings)' status and summary total; strict rejects pipe; full warning text in row/audit/log. |
| 18 | Workshared model: another user's pipe, editable pipe, then ownership conflict before commit. | Exclusion/checkout checked once per processing call; correct skip/error and per-pipe atomicity despite retries. Scan has its own check. |
| 19 | End/Start/Balanced with reversed, vertical and sloped curves; two-piece and asymmetric-union cases. | Measured endpoint losses correspond to piece order; target lengths and original endpoint connections preserved. |
| 20 | Non-uniform middle unions/takeout. | Measurement is not cached; iterative final verification remains active; incorrect results never accepted. |
| 21 | Select rows and Select all Failed/Skipped, including hidden failures and previously applied results. | Dialog closes; Selection.SetElementIds selects valid original/result IDs; ShowElements zooms. Reopen requires a new Scan. Zoom failure reports separately. |
| 22 | Full regression: pipe between elbows, pinned/grouped/branch pipe, missing union by size, existing short pipe, exactly max and slightly above max, high minimum. | Routing and exclusions remain; BreakCurve/NewUnionFitting/regeneration preserve system/type/level/diameter/workset/phases and both endpoint connections; infeasible pipes unchanged. |
| 23 | Preview rollback and Apply with multiple pipes, one fails; one Undo after Apply. | Preview leaves model geometry unchanged; only successful Apply pipes committed; entire batch restored in one Undo. Check insulation/fittings too. Preview ownership relinquishment is not guaranteed. |
| 24 | 1000+ pipes and smaller windows at user's actual display scale. | Saved-plan path reduces redundant work where valid; warning/verified counts correct; requested buttons/notes accessible in existing layout. Record timings and clipping; responsiveness/layout work remains deferred. |

API behavior relied on: document/version metadata; collectors and Pipe/SystemType/diameter reads; LocationCurve direction/length and model tolerances; routing rule lookup; worksharing checkout; BreakCurve and union connector creation/disconnection; Regenerate; workset/phase setters and inherited insulation; PipeInsulation.Create/GetInsulationIds/type/thickness; FailurePreprocessor warning/error handling; Transaction/TransactionGroup rollback/commit/Assimilate; element identity across rollback; PickObjects/ISelectionFilter cancellation with hide/re-show; SetElementIds and ShowElements after modal close. **All need manual verification**, including the prior Phase 1 tests.

Report scenario number, family/type/size, placement, rule file, expected/actual result and session log/audit excerpt. Stop after this review; do not proceed to Phase 3 until the user confirms.
