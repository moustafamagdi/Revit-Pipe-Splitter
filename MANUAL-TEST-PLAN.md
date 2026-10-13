# Phase 1 manual Revit verification plan

**Every item below: NEEDS MANUAL VERIFICATION. No Revit API behavior has been tested.**

Use a disposable copy of a Revit 2024 project. The assistant must not execute these scenarios. Record actual result, screenshots/element IDs, and relevant APPDATA/PipeSplitter/log.txt excerpts. Do not proceed to Phase 2 until the user confirms.

## API dependencies that need manual verification

- ActiveUIDocument access, family/read-only rejection, Revit-owned modal form and selected-element retrieval.
- FilteredElementCollector for host Pipe elements, LocationCurve/Line geometry, bounds and endpoints, connector enumeration, group/design-option/pinned checks and worksharing checkout status.
- RoutingConditions nominal-diameter units, Unions rule evaluation and returned FamilySymbol for each pipe type/size.
- BreakCurve output, regenerated geometry, connector identities, DisconnectFrom and NewUnionFitting with active routing rules.
- Workset parameter writability plus CreatedPhaseId/DemolishedPhaseId mutation and phase-order constraints.
- RBS_PIPING_SYSTEM_TYPE_PARAM, RBS_START_LEVEL_PARAM and RBS_PIPE_DIAMETER_PARAM availability/inheritance on all supported pipes.
- VertexTolerance point matching, ShortCurveTolerance safety and the scalar ShortCurveTolerance/100 acceptance policy.
- Connector AllRefs/IsConnectedTo behavior and preserved connections to existing elbows/accessories.
- PipeInsulation creation/propagation, insulation host/type/thickness retrieval and regeneration.
- IFailuresPreprocessor severity delivery, DeleteWarning, error/strict-warning rollback, forced modal failure handling, and clear-after-rollback semantics.
- Transaction commit/rollback, failed-pipe atomicity, TransactionGroup preview rollback and Assimilate single-undo behavior, including exceptions.

## Scenarios

| # | Setup/action | Expected result to verify | Status |
| --- | --- | --- | --- |
| 1 | Open DLL via Add-In Manager in an editable project; also try Family Editor/read-only context. | English form opens only for supported project context; unsupported context gives a clear message. | Needs manual verification |
| 2 | Straight horizontal pipe longer than 18 m, target 6 m, min 0.5 m. Preview then Apply. | Full finished pieces reach 6 m within logged model tolerance; remaining pieces meet bounds; endpoints unchanged. | Needs manual verification |
| 3 | Sloped pipe. | Original slope/endpoints and external connections remain; finished lengths use actual curve length. | Needs manual verification |
| 4 | Vertical pipe, then same type/size drawn in reverse direction. | Connector matching works; cache cannot silently accept reversed/asymmetric takeout. | Needs manual verification |
| 5 | Pipe between two elbows, with a connected system. | Both elbow connector references and original endpoint positions survive; system type matches. | Needs manual verification |
| 6 | Insulated pipe. | Every piece has the same insulation type and thickness; no missing/duplicate pipe insulation; no new union insulation is expected. | Needs manual verification |
| 7 | Pipe with Mark and custom parameters/tags. | Document actual propagation; no new cloning guarantee is made. Confirm required metadata/insulation is preserved and assess tag behavior. | Needs manual verification |
| 8 | Original on Workset A while active workset is B. | All new pieces/unions are on A; original workset remains unchanged. | Needs manual verification |
| 9 | Original created in an earlier phase than active view. Repeat with valid demolition phase. | New pieces/unions match created/demolished phases; read-only/unmatchable cases fully roll back. | Needs manual verification |
| 10 | Inspect each result's pipe type, System Type, Reference Level, Diameter, phases and workset. | All values match the original; an induced mismatch/read-only assignment rejects the entire source pipe. | Needs manual verification |
| 11 | Workshared pipe owned by another user. | Scan skips it; no partial cuts. Also inspect borrowed ownership after Preview rollback. | Needs manual verification |
| 12 | Pinned, grouped, design-option and placeholder pipes; branch/tap pipe. | Explicit skip reasons; no model edits. | Needs manual verification |
| 13 | Remove all union rules for one type; exclude one diameter in another rule. | Scan reports No union defined for <type> <size>; no BreakCurve on those rows. | Needs manual verification |
| 14 | Routing rule resolves but union family cannot place for the selected size. | Full pipe rollback with precise logged exception; other eligible pipes can continue. | Needs manual verification |
| 15 | Warning-only pipe transaction, Strict OFF (default). | Warning appears in row Log and log.txt; successful commit allowed; only newly posted callback warnings dismissed. Existing unrelated warnings remain. | Needs manual verification |
| 16 | Same warning-producing input, Strict ON. | Entire source pipe rolled back, warning logged, failure shown; changing strict mode requires rescan/repreview. | Needs manual verification |
| 17 | Induce a Revit Error or failed final validation. | No unresolved error is automatically resolved; source pipe unchanged and full stack trace logged. A batch-level exception rolls back the group. | Needs manual verification |
| 18 | Exactly max length; slightly above max outside logged tolerance; existing pipe below minimum. | Exact/within-tolerance max remains unchanged; just-over-max goes through planning; existing short stays unchanged and flagged. | Needs manual verification |
| 19 | 12.05 m, max 6, min 0.5; and an infeasible min/max pair. | Takeout-compensated full pieces and valid redistributed remainder; impossible allocation fails without partial edits. | Needs manual verification |
| 20 | Several same-type/same-size pipes. Inspect logs and elapsed time. | First successful measurement cached; subsequent pipes use seed, fewer trials when valid; all final checks still run. No promised speed target. | Needs manual verification |
| 21 | Same type/size with opposite orientation or family conditions producing different takeout; rescan after changing routing. | Wrong seed is corrected iteratively or rolled back with reason, never accepted incorrectly. Scan clears cache; different routing part prevents reuse. | Needs manual verification |
| 22 | Preview multiple valid/invalid pipes, close form, compare counts/geometry/metadata with baseline. | All preview model changes roll back, including insulation and unions; diagnostic file persists intentionally. | Needs manual verification |
| 23 | Apply mixed successful/failed pipes; then one Undo. | Successful pipes applied, each failed pipe unchanged; one Undo restores the whole successful batch. | Needs manual verification |
| 24 | 1000+ pipes; selection scope; repeated sorts and combined filters. | No missing rows or crash; numeric sort remains numeric; summary counts correct. UI may block because progress/cancel is Phase 3. Filters do not alter execution scope yet. | Needs manual verification |
| 25 | Sort/filter each column, including Log; export CSV. | 3-decimal UI and filtering; full internal precision retained; exported segment values and quoted multiline logs readable. | Needs manual verification |
| 26 | Missing/unbounded LocationCurve (if reproducible in diagnostic model). | Scan creates a skipped/blocked row instead of aborting. Unsupported geometry is not split. | Needs manual verification |
| 27 | Read log after scan/failure; deny write permission to log folder in an isolated Windows test. | Normal log contains tolerances/cache events/full stack traces; denied logging does not corrupt transactions and is reported after batch. | Needs manual verification |
| 28 | Inspect small lengths near ShortCurveTolerance and finished lengths with high-precision Revit properties. | No short curves are created; acceptance stays within logged derived tolerance. Three-decimal display must not hide an out-of-tolerance result. | Needs manual verification |

## Report back

Provide the scenario number, expected/actual behavior, pipe type/size/union family, strict-mode setting, Revit build, error text and relevant log excerpt. Build success and standalone arithmetic PASS do not mark any scenario above as passed.
