# Pipe length rules and saved settings

Files used at runtime:

- `%APPDATA%\PipeSplitter\rules.json`: per-pipe rules, read at every Scan or Reload rules.
- `%APPDATA%\PipeSplitter\settings.json`: last-used controls and window size.
- `%APPDATA%\PipeSplitter\audit.csv`: audit for the most recent dialog session that completed an operation, updated after each batch. Export it elsewhere to retain it across sessions.
- `%APPDATA%\PipeSplitter\log.txt`: append-only diagnostic log.

Open rules JSON opens Notepad. Save the file, return to the tool, then Reload rules and Scan. If no rules file exists, the tool creates an empty rule list. Invalid JSON or invalid rules block a new scan; they do not silently apply global defaults. Existing rows cannot be executed after a failed reload/scan.

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

- Property names are case-sensitive; use the spelling above. JSON comments and trailing commas are unsupported. Unknown fields are ignored by the .NET serializer: do not rely on them as selectors.
- SchemaVersion must be 1. Rules must be an array, including `[]` for no overrides.
- Name and MaximumM are required. Names must be unique ignoring case.
- Enabled defaults to true if omitted. Disabled rules are still validated.
- PipeType matches the Revit pipe type name, not material text.
- SystemType matches the piping SYSTEM TYPE name, not a system instance name or abbreviation.
- Type/system name comparisons ignore case but otherwise require exact text (including spaces); no wildcard expressions.
- DiameterMm matches nominal diameter in millimetres within 0.001 mm.
- Omit a selector or set it to null to ignore it. Empty type/system names are also ignored. At least one selector is required per rule.
- All selectors on one rule must match. FIRST matching enabled rule wins; put specific rules before general rules. Limits are not merged from several rules.
- MaximumM and MinimumM are metres. Omitted MinimumM uses the global minimum. No matching rule uses both global limits.
- Effective minimum greater than maximum, or below the Revit safety threshold, blocks that row. Other rows may remain eligible.
- Scan displays effective limits and selected Rule. They are frozen for that scan/preview/apply cycle. File edits require Reload/Scan to take effect.

## Remainder placement

Start and End mean LocationCurve.GetEndPoint(0) and GetEndPoint(1). They do not imply flow direction.

- End: original behavior, longest/full pieces first; redistribute the tail as needed.
- Start: reverse the target length sequence so the remainder is at the start.
- Balanced: use equal first/last pieces and maximum-length middle pieces where feasible. If that would violate minimum end lengths, reduce the middle pieces equally and leave the ends at minimum. A two-piece split is divided equally.

Example before union takeout, 12.05 m / max 6 / min 0.5:

- End: 6 + 5.55 + 0.5 m.
- Start: 0.5 + 5.55 + 6 m.
- Balanced: 3.025 + 6 + 3.025 m.

Final targets are recomputed from measured net length after union takeout using the same placement rule. Original endpoint connections remain constraints.

## Execution scope and settings

Apply to defaults to Visible rows on a fresh settings file. Both Preview and Apply use the chosen scope. All eligible is an explicit alternative and is remembered if selected.

Visible means rows surviving ALL column filters. Only eligible rows are processed. Sorting does not remove rows. Selected grid rows are for Select rows in Revit, not an execution subset. Apply confirmation states the exact eligible count. Widening filters/scope after Preview requires Preview for any newly included eligible rows. Filters on Status can hide rows as Preview updates their status; adjust filters before Apply if needed.

Saved controls: global max/min, remainder placement, strict mode, scan scope, execution scope, normal window width/height, maximized state. Grid filters/sort order and element selections are session-only to avoid carrying document IDs into another model. Invalid saved settings show a warning and load defaults. Invalid rules never silently fall back.

## Audit semantics

Export audit CSV includes every attempted pipe in this dialog's Preview/Apply batches, even if current filters hide it. Export visible CSV remains a separate table export.

Audit includes batch ID, UTC timestamp, document title, operation/scope/outcome, original ID, resulting IDs, newly created IDs excluding the retained source, union IDs, effective rules, before/after lengths, warnings, status and diagnostic text.

- Apply IDs are reported only after the enclosing TransactionGroup commits.
- Preview output IDs are EMPTY because trial elements are rolled back; its after-lengths are explicitly marked simulated.
- Failed/rolled-back operations clear output IDs. OriginalElementId identifies the unchanged source.
- ResultPipeIds and AfterLengths_m correspond in order along the original curve. CreatedPipeIds exclude OriginalElementId. UnionIds are in cut order.
- Audit describes the completed operation; a subsequent user Undo/delete is not tracked.
- The automatic audit.csv is overwritten by the next dialog session's first operation. Export a copy if you need permanent history.
- Model-derived text is quoted; leading spreadsheet formula characters are prefixed with an apostrophe in audit CSV.

All Revit-dependent picking, selection, geometry and audit ID behavior needs manual verification.
