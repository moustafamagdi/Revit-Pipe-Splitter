# Active Workset creation-context test (3.0.0.4)

All NEEDS MANUAL VERIFICATION. No Revit execution occurred.

1. Note the user's Active Workset, then Preview a few previously failing pipes whose insulation has a valid user workset (e.g. 38548). Logs must show activation of the original insulation workset before the cuts, the resulting insulation actual/expected placement, and restoration of the original user choice.
2. Check every resulting inherited/created insulation workset, type, thickness and phase. If active-workset switching is not the source of the observed behavior, failure must remain with explicit actual/expected diagnostics, not be accepted silently.
3. Source pipe workset differs from insulation workset: pipe pieces/unions must retain PIPE placement; insulation retains INSULATION placement. Uninsulated pipes and non-workshared documents remain unaffected.
4. Original insulation Workset 0, missing ID or non-user workset: verify existence/kind checks and clear failure before cutting. No guess, host-workset substitution or deletion of insulation.
5. Restore user Active Workset after successful Preview, successful Apply, each compensation retry, family/placement/ownership failure, Preview Cancel, both Apply Cancel choices and whole-batch rollback. Check again after one Undo. If already on target, no unnecessary switch.
6. Mixed batch with several different valid original insulation worksets: each pipe uses its own target; the next pipe and final user setting must not inherit the previous temporary choice. Check ordinary full-length/connection/phase metadata regression.
7. Failed active-workset activation must leave the pipe unchanged; a restoration failure must be reported, never silently treated as success. Existing per-pipe and group transaction guarantees still require verification.
8. Read-only versus missing Workset errors must now be distinguishable. Keep the newly captured row log if the attempt still fails. Confirm version 3.0.0.4 and recheck header right-click/Pick pipes/active-view selection.

---
# Insulation + header-menu fix - priority manual checks (3.0.0.3)

All NEEDS MANUAL VERIFICATION. The assistant did not execute the DLL in Revit or launch its WinForms UI.

1. Preview a small selection of previously failing source pipes (e.g. 27956548 and 27956558 if still present). Inspect Matching insulation messages with expected/actual values, then matched-original messages. Either successful Preview with final checks, or a clear field-specific failure is expected; no blanket success is assumed.
2. Test inherited insulation, missing insulation requiring Create, and original insulation with a different workset/phase from its host. All resulting insulation must retain original INSULATION placement, type and thickness. Pipes/unions retain PIPE placement.
3. Test already-correct read-only parameters: no write should be attempted. A differing read-only/ownership-blocked parameter must fail only its pipe and report field + expected/actual-before values. A failed repair must leave no partial pipes/unions/placement edits after rollback.
4. Test creation-phase changes with existing demolition, demolition-only changes and no phase changes. Verify legal phase order after regeneration; mismatch after regeneration must still reject the pipe.
5. Apply successful Preview rows, inspect finished lengths, connections and metadata, then Undo once. Repeat Preview cancellation/Apply rollback to confirm repairs are within the same atomic transactions.
6. Right-click every header repeatedly. Test Escape, outside-click dismissal, switching columns rapidly, scrolling and resizing. No disposed ContextMenuStrip errors.
7. Use Filter (Apply/Clear/Cancel), Clear this filter, Sort ascending/descending on numeric/text/Warnings columns. Each action must target the clicked column; underlying filter/Verified-set behavior unchanged.
8. Right-click then Pick pipes/Finish/Esc, return and right-click again. Also close the main form after menu use and during pending queued actions. No orphan/disposed-menu exception or unwanted action on a hidden form. All actions remain blocked during Preview/Apply.
9. Confirm title/log/audit version 3.0.0.3. If a pipe still fails, provide the new row log: the actual-before/expected placement and field name now distinguish read-only/ownership/phase issues. Worksharing failures still need the owner to resolve ownership as Revit directs.

---
# Active-view selection - priority manual checks (3.0.0.2)

All NEEDS MANUAL VERIFICATION; no Revit execution occurred.

1. In plan/section/3D, click Select pipes in active view. Verify selected host IDs and count; Scan must include that same active-view scope, not the whole model or previous picked IDs.
2. Test visible insulation on pipes; host pipes should appear once. Exclude fittings, insulation hosted on fittings, links and FlexPipe.
3. Test empty and unsupported views. Empty view clears selection and scans zero pipes; unsupported view gives a useful error; neither falls back to all-model processing.
4. Test crop (rectangular and nonrectangular), 3D section box, view filters, phases, hidden elements, temporary hide/isolate and occluded pipes. Review IDs before Apply: Revit's collector may include occluded/outside-crop elements; zoom extents are not the scope boundary.
5. Switch between All model, Selected pipes only and Pipes in active view. Changing scan scope invalidates verification. Preview/Apply still use only the exact saved successful IDs.
6. Close/reopen: active-view scan preference restores; Preview scope remains Visible rows. Older settings without ActiveViewOnly preserve their previous scope behavior.
7. Check title/log/audit version 3.0.0.2 and repeat Pick pipes/Finish/Esc regression checks below.

---
# Pick pipes fix - priority manual checks (3.0.0.1)

All NEEDS MANUAL VERIFICATION; no Revit execution occurred.

1. Pick pipes: form disappears; select two bare host pipes, Finish. Same form returns, note reports two pipes, scope Selected pipes only; Scan contains exactly those IDs.
2. Click insulation surfaces, including selecting both a pipe and its insulation. Host pipe is selected once; no insulation ID passed to Scan.
3. Try fitting insulation, fittings, FlexPipe and linked pipes. They remain excluded.
4. With an existing scan/Verified set, click Pick pipes then Esc (also try empty Finish). Previous picked IDs, scan and verified set remain. Reopened dialog is modal and usable.
5. Repeat Pick/Finish/Pick/Esc several times. No hidden/duplicate/orphan form; unchanged filters, rules and window state; one continuous audit session. Selecting a new nonempty set requires Scan and Preview again.
6. After returning, test Preview/Apply, Select rows/zoom/close, and the window X. No command loop remains after real close. Cancel remains blocked during an active batch as before.
7. Check title/log/audit version is 3.0.0.1. If picking still fails, report the exact non-modal note and the Pick pipes log entry, view type and whether you clicked pipe or insulation.

---
# Phase 3 manual verification - 3.0.0.0

**Every scenario is NEEDS MANUAL VERIFICATION. No Revit execution, UI launch or visual DPI test was performed by the assistant. Compilation and pure console tests do not establish these behaviors.**

Use a disposable model copy. Keep prior geometry/metadata/connection/routing regression scenarios. This plan supersedes old host-based insulation placement and old UI/layout/cancellation expectations.

## Visual matrix

| Scenario | Scale | Window states to verify | Expected observation |
| --- | --- | --- | --- |
| V1 | 100% | Default, minimum, maximized, restore | All Scope/Limits/Rules/Run/Results controls readable; wrap/scroll rather than clip; grid usable; progress/Cancel and KPIs accessible. |
| V2 | 125% | Default, minimum, maximized, restore | Same; ErrorProvider icons, dropdowns, buttons, captions and status-strip wrapping remain legible. |
| V3 | 150% | Default, minimum, maximized, restore | Same; bottom help/notes reachable by scrolling; no inaccessible controls or overlap. |
| V4 | Mixed-monitor if available | Move between different scales, reopen on each | Revit host DPI behavior and restored window size usable; report any scaling/clipping. |
| V5 | Each above scale | Right-click filter and cancel-choice dialogs | Auto-size to text; buttons/values/tooltips visible; keyboard Tab order, Enter and Escape understandable. |

## Functional and Revit matrix

| # | Action | Expected observation needing manual verification |
| --- | --- | --- |
| 1 | Open command, inspect title/session log/audit/file metadata. | All identify assembly version 3.0.0.0; correct Revit version/build, Windows user and document path (empty allowed for unsaved document). |
| 2 | Audit Arabic pipe/type/rule/log text across Preview and Apply; reopen CSV in Excel. | Arabic renders correctly, a single initial UTF-8 BOM, no hidden BOM in later rows. New session/collision gets a different file; old rows retained. |
| 3 | Original insulation placement equals its host; exercise inherited and newly created insulation. | Type/thickness and original insulation placement preserved on every resulting host. |
| 4 | Original insulation on DIFFERENT workset and/or creation/demolition phases from its pipe. | Difference logged; insulation retains ORIGINAL INSULATION placement; pipes/unions retain original PIPE placement. No host-forced insulation change. |
| 5 | Force missing/read-only insulation placement setter or inherited mismatch. | Clear offending insulation/parameter reason; affected pipe wholly rolled back, other pipes continue; no leftover pieces/unions/insulation. |
| 6 | Shadowed rules; repeat Scan/Reload without editing; change content then repeat. | Notes and log once per content change, no shadow-warning modal. Removing shadow clears notes. Invalid/unknown members still block with useful error. |
| 7 | Export combinations with filters; insert disabled example into empty/nonempty list. | All scanned combinations/counts correct; only empty list accepts insertion; current rules and limits still work. |
| 8 | Default Actionable and each quick Status choice, with a second column filter. | Correct AND combination; cancelled retryable and failed eligible rows stay actionable; Warning count numeric filters/sorts work. |
| 9 | Preview subset; narrow/sort/hide using Verified/Will split/column filter; Apply. | Exact saved ID set and cut plans reused, including hidden successes; confirmation count unchanged. |
| 10 | Widen to previously unrequested eligible IDs or change limits/remainder/strict/scan scope/rules. | Verification invalidated; settings/rules require Scan and Preview again. No stale plan application. |
| 11 | Reopen after choosing All eligible; restore other settings/window state. | Preview scope ALWAYS Visible rows; other preferences restored; Actionable quick status default. |
| 12 | Type empty/nonnumeric/out-of-range Max/Min, Min > Max, minimum at/below safety limit; correct it. | Inline errors; Scan/Preview/Apply disabled while invalid; correcting enables Scan but does not reuse stale verification. NumericUpDown normalization on focus loss remains understandable. |
| 13 | Ordinary multi-pipe Preview, then Apply. | Progress advances between pipes; saved-plan first attempt; status/Warn counts/KPIs and simulated/committed audit match results; finished full segments meet Max finished length after unions. |
| 14 | Cancel Preview before first pipe, midway, then at completion boundary. | All trials rolled back, Verified set cleared, a fresh Preview required. Remaining targets explicitly Not processed (cancelled), original lengths/no output IDs. Completed preview lengths labelled simulated in audit. |
| 15 | Cancel Apply midway; Keep completed pipes (one Undo step). | Only successful completed pipes kept; failed/unprocessed sources unchanged; output IDs only for kept results; one Undo restores kept changes. |
| 16 | Cancel Apply midway; Roll back everything. Repeat using Escape/X on choice. | All current-batch changes rolled back; no output IDs reported committed; prior batches untouched; remaining targets audited cancelled. |
| 17 | Cancel Apply before first pipe; test all-failed batch and after-last-pipe boundary. | Correct counts and no phantom output IDs; empty/no-change batch may have no Undo item; every target represented exactly once in batch audit. |
| 18 | During operation click other buttons, change filters, press keyboard shortcuts, attempt X/Alt+F4. | No re-entry, settings change, selection, export or close while running. Cancel remains accessible. Main form close only works after operation finishes. |
| 19 | Slow family/API operation and 1000+ pipes. | UI responds at boundaries, not mid-API call. Cancel delay understood; no background-thread Revit calls. Inspect family speed and ownership behavior; no promise of immediate interruption. |
| 20 | Mixed accepted warnings, strict-warning failure and ordinary error. | Per-pipe atomicity retained; colored status plus text; Warnings count/text tooltip, KPI/log/audit correct. Failed rows remain identifiable despite filters. |
| 21 | Select rows and all Failed/Skipped/Blocked, with hidden rows and applied results. | Form closes; valid IDs selected and ShowElements zooms; reopen and Scan again. No rollback-only temporary IDs selected. |
| 22 | Sloped/vertical/reversed pipes, between elbows, asymmetric/non-uniform unions; all remainder placements. | Same compensation/connection guards; final lengths, endpoint positions and external connections preserved; guarded cache fallback if necessary. |
| 23 | Workshared owned-by-other pipe, pinned/grouped/design option/branch pipe, missing union by size. | Existing exclusions/routing errors and once-per-processing checkout check unchanged; later ownership conflict fails safely per pipe. |
| 24 | Exactly Max finished length, slightly above it, min-length edge, existing short pipe. | Existing planner feasibility and tolerance rules unchanged; live global validation and per-rule validation consistent. |
| 25 | Preview rollback, successful Apply, single Undo; inspect system/type/level/diameter/phase/workset/insulation. | All prior guards retained; single Undo restores whole committed batch including insulation. Preview worksharing ownership relinquishment is not guaranteed. |
| 26 | Audit/log directory unwritable or full in disposable setup; batch-reporting failure. | Error reported without misrepresenting an already committed Apply as rolled back; export available after operation; prior audit files not overwritten. |

API dependencies needing manual verification: Document.PathName/Application version and tolerances; original insulation WorksetId/CreatedPhaseId/DemolishedPhaseId/parameter writability; PipeInsulation.Create and inherited insulation discovery; element setters; BreakCurve/NewUnionFitting/Regenerate; endpoint/connector identity; worksharing checks; FailurePreprocessor warning/error behavior; TransactionGroup.Start/Assimilate/RollBack with cancelled/mixed/empty batches; modal form plus Application.DoEvents on the Revit thread; selection/picking/ShowElements after form closure. All prior Phase 1/2 API assumptions remain subject to verification.

Report scenario ID, Windows scale, window size, Revit build, pipe/union family/type/size, rules, placement, log/audit excerpt and expected versus actual. No Phase 4 until explicitly requested.
