# Pipe length rules and saved settings

Files used at runtime:

- `%APPDATA%\PipeSplitter\rules.json`: per-pipe rules, read at every Scan or Reload rules.
- `%APPDATA%\PipeSplitter\settings.json`: last-used controls and window size.
- `%APPDATA%\PipeSplitter\audit_yyyyMMdd_HHmmss.csv`: unique audit per dialog session with operations; later batches append to the same file. A numeric suffix avoids same-second collisions. Earlier session files are never overwritten.
- `%APPDATA%\PipeSplitter\log.txt`: diagnostic log, rotated at approximately 5 MB with one `log.txt.bak` backup.

Rules > Open rules opens Notepad. Save the file, return to the tool, then Reload rules and Scan. If no rules file exists, the tool creates an empty rule list. Invalid JSON or invalid rules block a new scan; they do not silently apply global defaults. Existing rows cannot be executed after a failed reload/scan.

`rules.example.json` is a template with DISABLED examples; it is not automatically installed. Copy its contents into the runtime rules file, use exact names from the model, then enable the desired entries.

## Format

```json
{
  "SchemaVersion": 1,
  "Rules": [
    {
      "Name": "Steel supply 100 mm",
      "Enabled": true,
      "PipeType": "Steel",
      "SystemType": "Chilled Water Supply",
      "DiameterMm": 100,
      "MaximumM": 6,
      "MinimumM": 0.5
    }
  ]
}
```

- Property names are case-sensitive; use the spelling above. JSON comments and trailing commas are unsupported. Unknown members, including wrong capitalization, are rejected with the field and rule name/index (or root context). ExtensionData is retained and checked rather than silently ignored.
- SchemaVersion must be 1. Rules must be an array, including `[]` for no overrides.
- Name and MaximumM are required. Names must be unique ignoring case.
- Enabled defaults to true if omitted. Disabled rules are still validated.
- PipeType matches the Revit pipe type name, not material text.
- SystemType matches the piping SYSTEM TYPE name, not a system instance name or abbreviation.
- Type/system name comparisons ignore case but otherwise require exact text (including spaces); no wildcard expressions.
- DiameterMm matches nominal diameter in millimetres within 0.001 mm.
- Omit a selector or set it to null to ignore it. Empty type/system names are also ignored. At least one selector is required per rule.
- All selectors on one rule must match. FIRST matching enabled rule wins; a later enabled rule covered by an earlier equal-or-broader selector receives a warning; put specific rules before general rules. Limits are not merged from several rules.
- MaximumM is **Max finished length**, and MinimumM is **Min piece length**, both in metres. JSON keys remain unchanged for compatibility. Omitted MinimumM uses the global minimum. No matching rule uses both global limits.
- Effective Min piece length greater than Max finished length, or below the Revit safety threshold, blocks that row. Other rows may remain eligible.
- Scan displays effective limits and selected Rule. They are frozen for that scan/preview/apply cycle. Any file edit detected before Preview/Apply invalidates the scan and requires Scan and Preview again.

## Remainder placement

Start and End mean LocationCurve.GetEndPoint(0) and GetEndPoint(1). They do not imply flow direction.

- End: original behavior, longest/full pieces first; redistribute the tail as needed.
- Start: reverse the target length sequence so the remainder is at the start.
- Balanced: use equal first/last pieces and maximum-length middle pieces where feasible. If that would violate minimum end lengths, reduce the middle pieces equally and leave the ends at minimum. A two-piece split is divided equally.

Example before union takeout, 12.05 m / Max finished length 6 / Min piece length 0.5:

- End: 6 + 5.55 + 0.5 m.
- Start: 0.5 + 5.55 + 6 m.
- Balanced: 3.025 + 6 + 3.025 m.

Final targets are recomputed from measured net length after union takeout using the same placement rule. Original endpoint connections remain constraints.

## Execution scope and settings

Preview scope (previously Apply to) ALWAYS starts on Visible rows, including when older settings contain AllEligible. Execution scope is not persisted. Other preferences remain saved.

Preview uses eligible rows surviving all filters, or All eligible if explicitly chosen. It freezes the successful IDs and final cut plans. Apply uses exactly that verified set, including rows hidden by subsequent filtering; confirmation states its exact count. Failed Preview rows are excluded. Broadening to eligible IDs outside the original requested Preview scope invalidates the snapshot. Sorting and narrowing do not change the set. Settings/rule changes invalidate the scan and preview. A Status filter hiding successes cannot silently shrink Apply.

Saved controls: global Max finished length/Min piece length, remainder placement, strict mode, scan scope, normal window width/height, maximized state. Grid filters/sort order and element selections are session-only. Invalid saved settings show a warning and load defaults; invalid rules never silently fall back.

Export scan combinations exports ALL scanned rows grouped by PipeType, SystemType, DiameterMm and PipeCount, independent of filters. Insert example rule works only with an empty rule list and inserts a DISABLED rule: edit its selector and limits, enable it, then Scan and Preview. Shadow warnings do not change first-match precedence. They appear in the non-modal Rules notes area and log once per rules-file content change in the current dialog; repeated Scan/Reload of identical content does not repeat the notice.

Select rows in Revit and Select all Failed/Skipped close the dialog, set the selection and zoom using ShowElements. Reopen and Scan again to continue. A zoom failure is reported separately if selection succeeded.
## Audit semantics

Export audit CSV includes every attempted pipe and each unprocessed cancellation target in this dialog's Preview/Apply batches, even if current filters hide it. Export visible CSV remains a separate table export.

Each row includes tool version, Revit version/build, Windows user, document path and session ID. Audit also includes batch ID, UTC timestamp, document title, operation/scope/outcome, original ID, resulting IDs, newly created IDs excluding the retained source, union IDs, effective rules, before/after lengths, warnings, status and diagnostic text.

- Session audit writes a UTF-8 BOM only on creation; all later appends are BOM-free. Arabic text is preserved for Excel. Tool version comes from the assembly version, also used by the title and log session header.
- Apply IDs are reported only after the enclosing TransactionGroup commits.
- Preview output IDs are EMPTY because trial elements are rolled back; its after-lengths are explicitly marked simulated.
- Failed/rolled-back operations clear output IDs. OriginalElementId identifies the unchanged source.
- ResultPipeIds and AfterLengths_m correspond in order along the original curve. CreatedPipeIds exclude OriginalElementId. UnionIds are in cut order.
- Audit describes the completed operation; a subsequent user Undo/delete is not tracked.
- Each session audit is created exclusively, then appended without replacing earlier batches. Explicit Export audit CSV also refuses an existing filename; choose a new path. Sessions with no operations need not create an audit file.
- Model-derived text is quoted; leading spreadsheet formula characters are prefixed with an apostrophe in audit CSV.

All Revit-dependent picking, selection, geometry and audit ID behavior needs manual verification.

## Quick status, validation and cancellation

Status defaults to Actionable (all eligible, including retryable failures and unprocessed cancellations) and combines with all column filters using AND. Will split excludes verified/failed rows; Verified uses current verification state. Other choices are Skipped, Blocked, Failed, Applied, Unchanged and All. Narrowing never shrinks the Verified set; widening to previously unrequested eligible IDs clears it.

Max finished length and Min piece length have live validation. Invalid, empty or out-of-range values, Min greater than Max, and values at/below the Revit safety threshold disable Scan/Preview/Apply. Rules remain validated separately per effective row limits.

Cancel is cooperative between pipes; the current pipe completes first. Cancelled Preview always rolls back and clears the Verified set. Cancelled Apply asks whether to keep completed pipes in one Undo step or roll back the entire current batch. Closing/Escape on that question chooses rollback. Unprocessed targets have Status/Outcome `Not processed (cancelled)`, original lengths and no output IDs. A cancelled Preview's completed trial lengths remain explicitly simulated. Kept Apply output IDs resolve only after group commit; rolled-back output IDs are cleared. A fresh Preview is required after cancellation.

Rules, filters and length arithmetic are unchanged by the new grouped toolbar. Use the Phase 3 manual plan for UI scaling, cancellation, insulation baseline and regression checks. All Revit behaviors and Excel/display behavior need manual verification.
Scan scope also supports Pipes in active view. Select pipes in active view sets Revit selection and this scope, then requires Scan. ActiveViewOnly saves this scan preference; execution/Preview scope remains unsaved. Visibility follows Revit's view-scoped collector (may include occluded/outside-crop elements), not screen pixels. Empty views stay empty and links are excluded. Needs manual verification.
