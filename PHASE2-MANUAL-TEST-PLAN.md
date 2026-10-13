# Phase 2 manual verification

**All scenarios: NEEDS MANUAL VERIFICATION. No Revit execution occurred.**

Keep using the Phase 1 manual plan for transactions, warnings, metadata/worksets/phases, insulation, routing, tolerances and rollback. Its old display-only-filter expectation is superseded by the Phase 2 scope tests below. Phases 3/4 are pending.

New API dependencies needing manual verification: Pipe.Diameter and piping system type lookup for rule matching; LocationCurve endpoint ordering for placement; ISelectionFilter and Selection.PickObjects while the modal form is hidden; OperationCanceledException handling; Show/re-activation; Selection.SetElementIds after dialog closure; resulting pipe/fitting ElementId identities and lengths before/after group rollback/commit.

| # | Action | Expected observation | Status |
| --- | --- | --- | --- |
| 1 | 12.05 m pipe, max 6/min 0.5, End then Start in separate previews. | Tail/head distribution reverses relative to stored curve direction; full finished segments meet target after unions. | Needs manual verification |
| 2 | Same pipe with Balanced; also a two-piece pipe and a high-minimum case. | End pieces equal; middle pieces full where feasible; reduced equally when needed; all limits respected. | Needs manual verification |
| 3 | Repeat placements on vertical/sloped/reversed pipes with asymmetric unions. | Endpoint connections stay fixed; cache cannot bypass finished-length validation. | Needs manual verification |
| 4 | Configure specific type+system+diameter rule ahead of a broad type rule. | First matching enabled rule wins, selectors combine with AND; grid shows rule/effective limits. | Needs manual verification |
| 5 | Steel 6 m, PVC 3 m, unmatched Copper with global max. | Each row uses its appropriate max; no matching rule falls back globally. | Needs manual verification |
| 6 | Size boundary, system type vs system instance names, disabled rule. | Nominal millimetres and system TYPE matching work as documented; disabled rules do not apply. | Needs manual verification |
| 7 | Rule max below global minimum; malformed JSON; missing max. | Incompatible effective pair blocks only that row; invalid file prevents new Scan/Apply and reports cause. | Needs manual verification |
| 8 | Edit rules after Scan without Reload, then Reload and Scan. | Existing rows retain frozen limits until reload; reload invalidates previews and new scan updates limits. | Needs manual verification |
| 9 | Change controls, size/maximize window, close/reopen. Corrupt settings.json in a disposable test. | Valid preferences restored; corrupt preferences warn and use defaults; source model IDs not restored across documents. | Needs manual verification |
| 10 | Pick pipes in Revit; attempt picking fitting/linked element; Finish. | Only host Pipe selections accepted; form restores; selected-only Scan follows picked IDs. | Needs manual verification |
| 11 | Pick then Esc; no selection; attempt unsupported view switch during picking. | Previous selection survives cancellation and form restores; failures show clear error without model change. | Needs manual verification |
| 12 | Select several grid rows, then Select rows in Revit. | Form closes, intended existing pipes selected afterwards; successful Apply rows map to resulting pieces. | Needs manual verification |
| 13 | Select all Failed/Skipped with hidden errors and Blocked rows. | All matching original rows in scan selected, regardless of grid filters; no invalid temporary preview IDs. | Needs manual verification |
| 14 | Fresh preferences, filter to a known subset, Preview/Apply Visible rows. | Default visible scope; only eligible visible rows processed; confirmation count matches; hidden pipes unchanged. | Needs manual verification |
| 15 | Widen filters or switch to All eligible after subset Preview. | Apply unavailable until every newly included eligible row passes Preview; no silent partial apply. | Needs manual verification |
| 16 | Explicit All eligible with most rows hidden. | Preview/Apply include all eligible scan rows; confirmation clearly says All eligible and exact total. | Needs manual verification |
| 17 | Filter Status=Estimated, then Preview. | Successes may disappear as status changes; Apply never silently processes hidden rows in Visible mode. Adjust filters to see successful rows. | Needs manual verification |
| 18 | Apply subset, then process another subset; try repeating Apply on committed rows. | Committed rows no longer eligible; totals still include results; remaining rows require another Preview. | Needs manual verification |
| 19 | Audit after Preview. | Operation/outcome say simulated and rolled back; resulting/created/union ID fields empty; before and simulated after lengths present. | Needs manual verification |
| 20 | Audit after Apply with mixed successes/failures. | Committed result/new pipe IDs and union IDs resolve in document; lengths align with result IDs; failed rows have no output IDs. | Needs manual verification |
| 21 | Warning-only vs strict failure, plus forced batch rollback. | Audit records warnings and operation outcome; no rolled-back IDs appear as committed. | Needs manual verification |
| 22 | Close form via Select rows; inspect APPDATA audit.csv. Start another session. | Latest completed dialog audit survives close; next session intentionally overwrites on first operation; export preserves separate copy. | Needs manual verification |
| 23 | Export audit with commas, quotes, newlines and formula-like names. | CSV opens with correct fields and text values; no interpreted spreadsheet formulas. | Needs manual verification |
| 24 | Apply then one Undo. | Whole committed batch restored in one step; audit remains a historical record (does not automatically track Undo). | Needs manual verification |
| 25 | Re-run Phase 1 workset/phase/insulation/routing/connection scenarios with per-rule limits. | All prior guards still active and failures remain per-pipe atomic. | Needs manual verification |
| 26 | Review full interface on current monitor and reopen at smaller window size. | All new controls usable; report clipping/layout issues for Phase 3. | Needs manual verification |

Report scenario number, rule file, placement, scan/execution scope, family/type/size, error/log excerpt and actual vs expected result. Do not proceed to Phase 3 until the user confirms.
