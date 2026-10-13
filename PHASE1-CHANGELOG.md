# Phase 1 changelog - Correctness and robustness

Status: implemented for user review; Revit behavior needs manual verification.
Phases 2, 3 and 4 have not been started.

| Change | Reason / behavior difference |
| --- | --- |
| Failure preprocessor rolls back Error, DocumentCorruption, unknown non-warning severities, or failure-handler exceptions. | Previously every warning failed a pipe. No automatic error resolution modifies the model. |
| Strict mode checkbox, OFF by default. Non-strict warnings are copied to the row/file log before DeleteWarning. Strict mode logs them and rolls back the pipe. | Allows warning-only commits without repeated native warning dialogs; no silent warning loss. Existing document warnings are not intentionally deleted. Warnings are those delivered to the failure callback; explicitly rolled-back intermediate trials may not deliver a callback. |
| Capture original pipe metadata before the first cut in each attempt; assign workset/created/demolished phases on each new pipe and union; verify final pieces. | Prevents inheritance from current workset/active view phase or silent loss of type/system/level/diameter. A read-only assignment or mismatch rejects the pipe. Demolition is temporarily cleared before changing creation to avoid an invalid phase order. |
| RoutingPreferences GetMEPPartId precheck for Unions at the nominal diameter; repeated scan lookups are cached per type/size. Execution rechecks routing before each trial. | Reports No union defined for <type> <size> before BreakCurve. A matching routing family still needs placement verification. |
| Numeric-only, per-dialog takeout cache keyed by pipe type and exact nominal diameter, with routing part identity. | Subsequent pipes start with compensated cuts. Measurements are cached only after a successful per-pipe commit; preview group rollback retains numbers, never element handles. Nonuniform per-joint loss is not cached. A changed routing part invalidates reuse. |
| Cache estimates remain subject to all geometry, length, metadata and insulation checks; infeasible seeds/failed first cached trials revert to the iterative method. | The cache is an optimization, never proof of validity. No speed claim is made until manually measured. |
| VertexTolerance for point coincidence; ShortCurveTolerance/100 for scalar lengths; short-curve threshold for cut safety. | Replaces hard-coded geometry thresholds. Planner's tiny constants are separately documented arithmetic roundoff allowances, not model tolerances. |
| UI lengths shown to 3 decimal places, with matching numeric filtering; internal per-segment doubles retained separately from displayed text. | Millimetre readability without feeding rounded display values into compensation. CSV segment arrays retain round-trip values. |
| Null/unbounded curve handling in Scan; one malformed pipe does not abort all scanning. | Produces a skipped/blocked row instead of dereferencing a missing LocationCurve. |
| HashSet selection lookup and summary membership; explicit column accessor switch instead of reflection. | Removes repeated linear membership scans and reflection without changing sort/filter scope. |
| Row Log column and CSV log field; APPDATA/PipeSplitter/log.txt with exception.ToString stack traces, warnings and cache events. | Makes failures diagnosable. File write failures are nonfatal and reported after a batch. Logging is append-only. |
| Final length, union-connection and original-end connection checks after insulation regeneration. | Prevents acceptance based only on geometry measured before the final regeneration. |
| C# 5 explicitly enforced in Build.ps1; System.Tuple use removed from cut-point bookkeeping. | No compiler migration and no modern language features required. |
| Planner rejects invalid/nonfinite inputs, count overflow, nonconserving compensation and nonpositive cuts. | Makes the arithmetic harness deterministic for malformed inputs and avoids unsafe casts on enormous counts. |
| Test-Planner.ps1 and tests/PlannerTests.cs. | Reproducible csc-only arithmetic checks: edge cases, 50,000 seeded allocations, 10,000 synthetic asymmetric takeouts; no NuGet or Revit references. |

## Preserved design

Per-pipe atomic transactions, measured union takeout, FinishedTargets/CompensateCuts, original connection verification, TransactionGroup rollback preview, single-undo Apply, and pipe insulation type/thickness preservation remain in place. All Revit-specific behavior needs manual verification.

## Open risks / deliberately deferred

- No DLL execution in Revit occurred. Family placement, connector behavior, routing lookup, writable workset/phase parameters and failure preprocessing are unverified.
- Asymmetric unions may reverse orientation between pipes. Cached estimates must fall back correctly; test opposite-direction, sloped and vertical pipes.
- A valid routing rule does not guarantee support for every family lookup-table size.
- The tolerance policy is derived from Revit application values but needs measurement in the user's project.
- Long previews still run synchronously. Cancellation, DPI/layout redesign and visible-row execution belong to later phases.
- Filters still affect display/export only; processing and summary retain the full scan scope.
- No Mark/custom parameter cloning was added. Insulation preserves type/thickness, not all custom values or tags; new unions are not insulated.
- Preview rollback may not relinquish worksharing ownership.
- Logs are append-only and can grow; failures delivered only during final failure processing are logged, not guaranteed warnings from explicitly rolled-back trial passes.
- The installer still registers the development DLL path; copied deployment/ribbon integration is deferred to Phase 4.
