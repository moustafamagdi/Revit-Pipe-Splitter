using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using WF = System.Windows.Forms;

namespace PipeSplitter2024
{
    public partial class SplitForm
    {
        private static string Header(string name)
        {
            switch (name)
            {
                case "Id": return "Element ID";
                case "Type": return "Pipe type";
                case "SystemType": return "System type";
                case "Diameter_mm": return "Diameter (mm)";
                case "Length_m": return "Length (m)";
                case "MaxFinished_m": return "Max finished length (m)";
                case "Minimum_m": return "Min piece length (m)";
                case "Min_m": return "Min (m)";
                case "Max_m": return "Max (m)";
                case "Lengths_m": return "Piece lengths (m)";
                case "WarningCount": return "Warnings";
                default: return name;
            }
        }

        private void ConfigureGrid()
        {
            grid.Dock = WF.DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToOrderColumns = true;
            grid.AutoSizeColumnsMode = WF.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            grid.BackgroundColor = Color.White;
            grid.SelectionMode = WF.DataGridViewSelectionMode.FullRowSelect;
            grid.RowHeadersVisible = false;
            grid.ShowCellToolTips = true;
            grid.ColumnHeadersHeightSizeMode = WF.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.AutoSizeRowsMode = WF.DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            grid.Height = 220;
            Tip(grid, "Click any header to sort; right-click it to filter. Filters combine with Status. Select rows for Select rows in Revit.");
            grid.ColumnHeaderMouseClick += delegate(object sender, WF.DataGridViewCellMouseEventArgs e)
            {
                if (running || e.ColumnIndex < 0) return;
                string column = grid.Columns[e.ColumnIndex].DataPropertyName;
                if (e.Button == WF.MouseButtons.Left)
                {
                    descending = sortColumn == column ? !descending : false;
                    sortColumn = column;
                    ApplyView();
                }
                else if (e.Button == WF.MouseButtons.Right)
                {
                    var menu = new WF.ContextMenuStrip();
                    menu.ShowItemToolTips = true;
                    menu.Items.Add("Filter " + Header(column) + "...", null, delegate { if (!running) EditFilter(column); });
                    menu.Items.Add("Clear this filter", null, delegate
                    {
                        if (running) return;
                        filters.Remove(column);
                        ApplyView();
                    });
                    menu.Items.Add("Sort ascending", null, delegate
                    {
                        if (running) return;
                        sortColumn = column;
                        descending = false;
                        ApplyView();
                    });
                    menu.Items.Add("Sort descending", null, delegate
                    {
                        if (running) return;
                        sortColumn = column;
                        descending = true;
                        ApplyView();
                    });
                    foreach (WF.ToolStripItem item in menu.Items)
                        item.ToolTipText = item.Text + " for " + Header(column);
                    menu.Closed += delegate { menu.Dispose(); };
                    menu.Show(WF.Cursor.Position);
                }
            };
            grid.CellFormatting += delegate(object sender, WF.DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].DataPropertyName != "Status") return;
                Row row = grid.Rows[e.RowIndex].DataBoundItem as Row;
                if (row == null) return;
                string status = row.Status ?? "";
                Color color = Color.FromArgb(232, 240, 250);
                if (status.StartsWith("Applied") || row.Passed) color = Color.FromArgb(219, 242, 222);
                else if (status.StartsWith("Failed")) color = Color.FromArgb(255, 219, 219);
                else if (status.StartsWith("Blocked") || row.WarningCount > 0) color = Color.FromArgb(255, 236, 195);
                else if (!row.Eligible) color = Color.FromArgb(235, 235, 235);
                e.CellStyle.BackColor = color;
                e.CellStyle.ForeColor = Color.Black;
            };
            grid.CellToolTipTextNeeded += delegate(object sender, WF.DataGridViewCellToolTipTextNeededEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                Row row = grid.Rows[e.RowIndex].DataBoundItem as Row;
                if (row == null) return;
                string name = grid.Columns[e.ColumnIndex].DataPropertyName;
                e.ToolTipText = name == "WarningCount" ? string.Join(Environment.NewLine, row.Warnings)
                    : name == "Status" ? row.Status + Environment.NewLine + row.Log
                    : Convert.ToString(RowColumns.Value(row, name), CultureInfo.CurrentCulture);
            };
        }

        private void WireInputEvents()
        {
            maximum.ValueChanged += delegate { InputsChanged(); };
            minimum.ValueChanged += delegate { InputsChanged(); };
            maximum.TextChanged += delegate { InputsChanged(); };
            minimum.TextChanged += delegate { InputsChanged(); };
            scope.SelectedIndexChanged += delegate { InvalidateScan(); };
            strictMode.CheckedChanged += delegate { InvalidateScan(); };
            quickStatus.SelectedIndexChanged += delegate { if (!running) ApplyView(); };
        }

        private void InputsChanged()
        {
            if (!uiReady || running) return;
            InvalidateScan();
            ValidateInputs();
            UpdateActionButtons();
        }

        private bool ValidateInputs()
        {
            decimal max;
            decimal min;
            bool maxParsed = decimal.TryParse(maximum.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out max);
            bool minParsed = decimal.TryParse(minimum.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out min);
            string maxError = maxParsed ? "" : "Enter a valid Max finished length in metres.";
            string minError = minParsed ? "" : "Enter a valid Min piece length in metres.";
            if (maxParsed && minParsed)
            {
                string error = Phase3Policy.ValidateLimits(max, min, minimumSafeMetres);
                maxError = error;
                minError = error;
            }
            inputErrors.SetError(maximum, maxError);
            inputErrors.SetError(minimum, minError);
            inputsValid = maxError.Length == 0 && minError.Length == 0;
            return inputsValid;
        }

        private void InvalidateScan()
        {
            if (!uiReady || running) return;
            scanValid = false;
            ClearSnapshot();
            SetOperationNote("Settings changed. Scan again to update the results.");
            UpdateActionButtons();
            RefreshKpis();
        }

        private void UpdateActionButtons()
        {
            if (!uiReady || preview == null || apply == null) return;
            var candidates = ExecutionRows();
            if (!running && previewSnapshot != null && previewSnapshot.IncludesNewIds(candidates.Select(row => row.Id)))
            {
                ClearSnapshot();
                SetOperationNote("Scope includes new eligible pipes. Preview again to establish the Verified set.");
            }
            ValidateInputs();
            scan.Enabled = !running && inputsValid;
            preview.Enabled = !running && inputsValid && scanValid && candidates.Count > 0;
            apply.Enabled = !running && inputsValid && scanValid && previewSnapshot != null && previewSnapshot.Count > 0;
            export.Enabled = !running && scanValid && rows.Count > 0;
            ShowSnapshotCount();
            RefreshKpis();
        }

        private void ApplyView()
        {
            string selectedStatus = Convert.ToString(quickStatus.SelectedItem, CultureInfo.InvariantCulture);
            var query = rows.Where(row => Phase3Policy.MatchesStatus(selectedStatus, row.Status, row.Eligible, row.Passed)
                && filters.All(filter => Matches(row, filter.Key, filter.Value)));
            visibleRows = (descending ? query.OrderByDescending(row => RowColumns.Value(row, sortColumn))
                : query.OrderBy(row => RowColumns.Value(row, sortColumn))).ThenBy(row => row.Id).ToList();
            grid.DataSource = null;
            grid.DataSource = visibleRows;
            foreach (WF.DataGridViewColumn column in grid.Columns)
            {
                string name = column.DataPropertyName;
                column.SortMode = WF.DataGridViewColumnSortMode.Programmatic;
                column.HeaderText = Header(name) + (filters.ContainsKey(name) ? " [filter]" : "");
                column.HeaderCell.ToolTipText = "Click to sort; right-click to filter " + Header(name) + ".";
                column.HeaderCell.SortGlyphDirection = name == sortColumn
                    ? (descending ? WF.SortOrder.Descending : WF.SortOrder.Ascending) : WF.SortOrder.None;
                if (column.ValueType == typeof(double)) column.DefaultCellStyle.Format = "0.000";
            }
            filterInfo.Text = "Showing " + visibleRows.Count + " of " + rows.Count
                + " pipes. Click headers to sort; right-click to filter. "
                + (filters.Count == 0 ? "No column filters." : string.Join("; ", filters.Select(filter => Header(filter.Key) + " " + filter.Value.Operator + " " + filter.Value.Text)));
            Tip(filterInfo, filterInfo.Text);
            UpdateActionButtons();
        }

        private void ShowSnapshotCount()
        {
            verifiedLabel.Text = "Verified set: " + (previewSnapshot == null ? 0 : previewSnapshot.Count) + " pipes";
        }

        private void RefreshRows(string phase)
        {
            ApplyView();
            RefreshKpis();
            SetOperationNote(phase);
        }

        private void RefreshKpis()
        {
            if (kpis.Count == 0) return;
            var applicable = new HashSet<long>(rows.Where(row => (row.Status ?? "").StartsWith("Applied")
                || (row.Eligible && (row.Passed || (row.Status ?? "").StartsWith("Estimated")))).Select(row => row.Id));
            SetKpi("Scanned", rows.Count);
            SetKpi("To split", rows.Count(row => row.Eligible));
            SetKpi("Verified", previewSnapshot == null ? 0 : previewSnapshot.Count);
            SetKpi("Applied", rows.Count(row => (row.Status ?? "").StartsWith("Applied")));
            SetKpi("Failed", rows.Count(row => (row.Status ?? "").StartsWith("Failed")));
            SetKpi("Warnings", rows.Sum(row => row.WarningCount));
            SetKpi("Pieces", rows.Sum(row => applicable.Contains(row.Id) ? row.Pieces : 1));
            SetKpi("Unions", rows.Where(row => applicable.Contains(row.Id)).Sum(row => row.Unions));
        }

        private void SetKpi(string name, int value)
        {
            kpis[name].Text = name + ": " + value.ToString(CultureInfo.CurrentCulture);
        }

        private void SetOperationNote(string message)
        {
            operationNote.Text = message;
            Tip(operationNote, message);
        }

        private void ShowRuleNotes(List<string> warnings, string source)
        {
            if (string.Equals(lastNotedRules, source, StringComparison.Ordinal)) return;
            lastNotedRules = source;
            ruleNotes.Text = warnings.Count == 0 ? "" : "Rules notes: " + string.Join(Environment.NewLine, warnings);
            ruleNotes.Visible = warnings.Count > 0;
            Tip(ruleNotes, ruleNotes.Text);
            if (warnings.Count > 0) DiagnosticLog.Write("Shadowed rules", string.Join(Environment.NewLine, warnings));
        }

        private void EditFilter(string column)
        {
            bool numeric = RowColumns.IsNumeric(column);
            ColumnFilter existing;
            filters.TryGetValue(column, out existing);
            using (var dialog = new WF.Form())
            {
                dialog.Text = "Filter: " + Header(column);
                dialog.Font = Font;
                dialog.AutoScaleDimensions = new SizeF(96, 96);
                dialog.AutoScaleMode = WF.AutoScaleMode.Dpi;
                dialog.AutoSize = true;
                dialog.AutoSizeMode = WF.AutoSizeMode.GrowAndShrink;
                dialog.StartPosition = WF.FormStartPosition.CenterParent;
                dialog.FormBorderStyle = WF.FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                var panel = new WF.FlowLayoutPanel { AutoSize = true, Dock = WF.DockStyle.Fill, FlowDirection = WF.FlowDirection.TopDown, WrapContents = false, Padding = new WF.Padding(16) };
                var op = new WF.ComboBox { DropDownStyle = WF.ComboBoxStyle.DropDownList, Width = 330 };
                op.Items.AddRange(numeric ? new object[] { "=", "!=", ">", ">=", "<", "<=" }
                    : new object[] { "Contains", "Does not contain", "Equals", "Not equal", "Starts with", "Ends with" });
                op.SelectedItem = existing == null ? op.Items[0] : existing.Operator;
                var value = new WF.TextBox { Width = 330, Text = existing == null ? "" : existing.Text };
                var hint = Label(numeric ? "Use the column's displayed units; lengths are metres, diameter is mm." : "Text matching is case-insensitive.");
                hint.MaximumSize = new Size(350, 0);
                panel.Controls.Add(op);
                panel.Controls.Add(value);
                panel.Controls.Add(hint);
                var buttons = new WF.FlowLayoutPanel { AutoSize = true };
                var accept = new WF.Button { Text = "Apply filter", AutoSize = true };
                var clear = new WF.Button { Text = "Clear filter", AutoSize = true };
                var cancel = new WF.Button { Text = "Cancel", AutoSize = true, DialogResult = WF.DialogResult.Cancel };
                accept.Click += delegate
                {
                    decimal number = 0;
                    if (numeric && !decimal.TryParse(value.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out number)
                        && !decimal.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    {
                        WF.MessageBox.Show(dialog, "Enter a valid number.");
                        return;
                    }
                    if (!numeric && string.IsNullOrEmpty(value.Text)) filters.Remove(column);
                    else filters[column] = new ColumnFilter { Operator = op.SelectedItem.ToString(), Text = value.Text, Number = number };
                    dialog.DialogResult = WF.DialogResult.OK;
                };
                clear.Click += delegate
                {
                    filters.Remove(column);
                    dialog.DialogResult = WF.DialogResult.OK;
                };
                buttons.Controls.AddRange(new WF.Control[] { accept, clear, cancel });
                panel.Controls.Add(buttons);
                dialog.Controls.Add(panel);
                dialog.AcceptButton = accept;
                dialog.CancelButton = cancel;
                Tip(op, "Comparison for this column.");
                Tip(value, "Filter value in the column's displayed units.");
                AddDefaultTips(panel);
                if (dialog.ShowDialog(this) == WF.DialogResult.OK) ApplyView();
            }
        }
    }
}
