# Phase 2 changelog - review fixes, 2.0.0.0

Only the requested review fixes are included. Phase 3 layout redesign and Phase 4 are not started. All Revit API behavior remains **needs manual verification**.

| Change | Reason and behavior |
| --- | --- |
| Store immutable successful Preview IDs and final cut plans. Apply first tries each stored plan. | Avoid repeats of the initial trial pass. All final length, connection, metadata, insulation and transaction checks remain. Failed verified attempts fall back to cache/iteration; regeneration and failure-processing errors remain fatal for that pipe. |
| Display Verified set: N pipes and confirm that exact set. | Later narrowing/sorting cannot silently shrink Apply. Eligible IDs outside the original requested Preview scope invalidate the snapshot. Failed Preview rows are excluded from Apply. Settings/rules require Scan and Preview again. |
| Always open on Visible rows; remove execution scope from saved settings. | Prevent a previously saved All eligible choice from unexpectedly widening a new session. All other settings continue to persist. |
| Reject unknown rules.json members, preserving and inspecting IExtensibleDataObject.ExtensionData. | Report the misspelled field and containing rule/root rather than silently accepting it. No private reflection. |
| Warn about earlier equal-or-broader enabled rules shadowing a later rule. | Explain why first-match precedence makes a rule unreachable. Disabled rules do not shadow. Warnings do not reorder rules. |
| Export scan combinations and Insert example rule in the existing options area. | Export all scanned PipeType/SystemType/DiameterMm/count combinations. Insert a disabled editable template only when the list is empty. No layout redesign. |
| Assembly/file/title version 2.0.0.0; session metadata in logs and every audit row. | Identify tool, Revit version/build, Windows user and document path for diagnosis. Session ID disambiguates sessions. Unsaved document paths may be empty. |
| Timestamped per-session audit, exclusive creation and append; log rotation near 5 MB. | Preserve previous sessions and batches. Same-second collisions use a numeric suffix. Explicit audit exports also refuse overwrite. Keep one log.txt.bak backup. |
| Match workset and creation/demolition phase for newly created PipeInsulation; verify inherited insulation. | Reject an entire pipe on placement mismatch, retaining existing type/thickness checks. No attempt to repair inherited mismatches silently. |
| Warning count in row Status and summary. | Make accepted warnings visible without opening individual logs. Summary counts row warnings from the latest processing attempt, not historical audit batches. |
| Exclusion/GetCheckoutStatus once per Engine.Process call. | Avoid redundant worksharing checks inside retries. Scan retains its own precheck; transaction failures still protect against later ownership changes. |
| Extract Revit-free TakeoutMeasurement.cs; rename loss fields FirstPieceLoss/LastPieceLoss. | Clarify orientation relative to the ordered pieces and exercise the same measurement/seed code in the console harness. Arithmetic algorithm remains unchanged. |
| Select in Revit also calls ShowElements after closing. | Make selected pipes easier to find. UI notes that the dialog closes and scanning must be repeated. Zoom failures are reported separately from successful selection. |

Preserved: per-pipe atomic transactions; measured union takeout; FinishedTargets/CompensateCuts; connection-integrity and metadata verification; TransactionGroup Preview rollback; single-undo Apply; insulation preservation; C# 5; fixed PipeSplitter2024.dll.

Complete changed/new code: PipeSplitter.cs, Phase1Support.cs, Phase2Settings.cs, Phase2Support.cs, Phase2Form.cs, TakeoutMeasurement.cs, PreviewSnapshot.cs, SessionFiles.cs, SessionInfo.cs, tests/PlannerTests.cs, Build.ps1, Test-Planner.ps1. README, rules/settings guide, this changelog, manual plan and build report are updated. The handoff contains complete files and unchanged build dependencies, not diffs.

## Build and standalone checks

Build.ps1 succeeded using /langversion:5 against local Revit 2024 references, with zero compiler errors/warnings. Test-Planner.ps1 passed 1,106,629 assertions without loading Revit assemblies. Tests include reversed takeout orientation, two pieces, non-uniform middle joints, all remainder placements, immutable snapshot behavior, unknown JSON/ExtensionData, rule shadowing, legacy settings, audit collisions/appends and log rotation.

## Open risks

- No Revit execution occurred. All API-dependent behavior and actual family performance require manual verification.
- Apply may retry when the saved plan no longer works; real-family speed improvement is unmeasured. Preview is still synchronous and has no cooperative Cancel (Phase 3 deferred).
- Insulation with incompatible/read-only workset or phases intentionally rejects the pipe. Test inherited insulation and creation paths separately.
- A modal dialog cannot promise worksharing ownership remains unchanged between the single checkout check and commit; transaction failures remain the safeguard.
- Added requested actions use the existing options area; clipping on small windows needs user review. No DPI/layout redesign was performed.
- File permission, disk-full and multi-process logging contention can cause logging/audit errors; these are reported. Rotation is approximately 5 MB and happens before a write, so a single large entry can exceed the threshold.
- There is no automatic audit retention cleanup. Paths/user names are intentionally recorded as requested. Audit is historical and does not track later Undo.
