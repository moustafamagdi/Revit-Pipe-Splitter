using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Autodesk.Revit.DB;
using WF = System.Windows.Forms;

namespace PipeSplitter2024
{
    public partial class SplitForm
    {
        private void SetRunning(bool value)
        {
            running = value;
            toolbar.Enabled = !value;
            filterBar.Enabled = !value;
            grid.Enabled = !value;
            cancelRun.Enabled = value;
            if (value) cancelRequested = false;
            UpdateActionButtons();
        }

        private void PumpProgress(int completed, int total, bool commit)
        {
            progress.Maximum = Math.Max(1, total);
            progress.Value = Math.Min(completed, progress.Maximum);
            progressText.Text = (commit ? "Applying " : "Previewing ") + completed + " / " + total;
            RefreshKpis();
            // Revit API calls remain synchronous on its main thread. Pump only between pipes,
            // when no per-pipe transaction is active. All mutation controls and form close are blocked.
            WF.Application.DoEvents();
        }

        private bool AskKeepCompleted()
        {
            using (var dialog = new WF.Form())
            {
                dialog.Text = "Apply cancelled";
                dialog.Font = Font;
                dialog.AutoScaleDimensions = new SizeF(96, 96);
                dialog.AutoScaleMode = WF.AutoScaleMode.Dpi;
                dialog.AutoSize = true;
                dialog.AutoSizeMode = WF.AutoSizeMode.GrowAndShrink;
                dialog.StartPosition = WF.FormStartPosition.CenterParent;
                dialog.FormBorderStyle = WF.FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                var panel = new WF.FlowLayoutPanel { AutoSize = true, Dock = WF.DockStyle.Fill, FlowDirection = WF.FlowDirection.TopDown, WrapContents = false, Padding = new WF.Padding(16) };
                panel.Controls.Add(Label("What should happen to pipes completed in this Apply batch?"));
                var keep = new WF.Button { Text = "Keep completed pipes (one Undo step)", AutoSize = true, DialogResult = WF.DialogResult.Yes };
                var rollback = new WF.Button { Text = "Roll back everything", AutoSize = true, DialogResult = WF.DialogResult.No };
                panel.Controls.Add(keep);
                panel.Controls.Add(rollback);
                dialog.Controls.Add(panel);
                dialog.AcceptButton = rollback;
                dialog.CancelButton = rollback;
                Tip(keep, "Commit successful completed pipes as one Undo step. Unprocessed pipes remain unchanged and are audited.");
                Tip(rollback, "Roll back all changes made in this Apply batch. Unprocessed pipes also remain unchanged.");
                AddDefaultTips(panel);
                // Closing this question or Escape is the rollback choice.
                return dialog.ShowDialog(this) == WF.DialogResult.Yes;
            }
        }

        private void Run(bool commit)
        {
            if (running) return;
            if (!ValidateInputs()) throw new InvalidOperationException("Correct the length inputs first.");
            if (!scanValid) throw new InvalidOperationException("Scan first with the current settings and rules.");
            EnsureRulesUnchanged();
            UpdateActionButtons();
            List<Row> targets = commit ? VerifiedRows() : ExecutionRows();
            if (targets.Count == 0) throw new InvalidOperationException("No eligible pipes in the current execution scope.");
            if (commit && (previewSnapshot == null || targets.Count != previewSnapshot.Count
                || targets.Any(row => !row.Passed || row.VerifiedCutPlan == null)))
                throw new InvalidOperationException("Run Preview again to establish the Verified set.");
            string scopeName = commit ? "Verified Preview set (" + previewSnapshot.Scope + ")" : executionScope.SelectedItem.ToString();
            if (commit && WF.MessageBox.Show(this, "Verified set: " + targets.Count
                + " pipes\nApply uses exactly this Preview set, including any now hidden by filters.\n"
                + "First attempts reuse the verified cut plans. One Undo restores kept changes.",
                "Apply pipe splitting", WF.MessageBoxButtons.YesNo, WF.MessageBoxIcon.Question) != WF.DialogResult.Yes) return;
            if (commit)
            {
                foreach (Row row in targets) row.VerifiedCutPlan = previewSnapshot.PlanFor(row.Id);
            }
            else ClearSnapshot();

            string batch = Guid.NewGuid().ToString("N");
            var records = new List<AuditRecord>();
            bool groupFinished = false;
            bool keptChanges = false;
            bool cancelled = false;
            int completed = 0;
            DiagnosticLog.Write(commit ? "Apply" : "Preview", "Batch=" + batch + "; scope=" + scopeName
                + "; strict=" + strictMode.Checked + "; pipes=" + targets.Count);
            SetRunning(true);
            try
            {
                using (var group = new TransactionGroup(doc, "Pipe Splitter - " + (commit ? "Apply" : "Preview")))
                {
                    if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("Cannot start transaction group.");
                    for (int index = 0; index < targets.Count; index++)
                    {
                        PumpProgress(completed, targets.Count, commit);
                        if (cancelRequested) break;
                        Row row = targets[index];
                        row.Passed = false;
                        string error;
                        bool success = Engine.Process(doc, row, row.MaxFinished_m, row.Minimum_m,
                            strictMode.Checked, takeoutCache, commit, out error);
                        row.Status = row.WithWarnings(success ? (commit ? "Applied" : "Preview OK") : "Failed: " + error);
                        if (!success) Engine.SetLengths(row, new List<double> { row.Length_m });
                        records.Add(AuditRecord.Capture(row, batch, commit ? "Apply" : "Preview", scopeName, doc.Title, success, session));
                        completed++;
                    }
                    PumpProgress(completed, targets.Count, commit);
                    cancelled = cancelRequested;
                    if (cancelled)
                    {
                        AddUnprocessed(targets.Skip(completed), records, batch, commit, scopeName);
                        cancelRun.Enabled = false;
                    }
                    bool keep = commit && (!cancelled || AskKeepCompleted());
                    if (keep)
                    {
                        if (group.Assimilate() != TransactionStatus.Committed) throw new InvalidOperationException("Group commit failed.");
                        keptChanges = true;
                    }
                    else if (group.RollBack() != TransactionStatus.RolledBack)
                        throw new InvalidOperationException("Group rollback failed.");
                    groupFinished = true;
                }

                foreach (AuditRecord record in records) record.FinalizeResult(keptChanges, !commit);
                if (commit && !keptChanges)
                {
                    MarkCancelledRollback(targets.Take(completed), records);
                    takeoutCache.Clear();
                }
                audit.AddRange(records);
                PersistAudit();
                foreach (Row row in targets)
                {
                    if (!commit)
                    {
                        row.ResultPipeIds.Clear();
                        row.ResultUnionIds.Clear();
                        if (cancelled && row.Passed)
                            row.Status = row.WithWarnings("Cancelled Preview: trial rolled back; Preview again");
                        if (row.Passed && !cancelled) row.VerifiedCutPlan = new List<double>(row.Plan);
                    }
                    else if (keptChanges && row.Passed)
                    {
                        row.Eligible = false;
                        row.Passed = false;
                    }
                }
                if (commit || cancelled) ClearSnapshot();
                else previewSnapshot = new PreviewSnapshot(targets.Select(row => row.Id),
                    targets.Where(row => row.Passed).ToDictionary(row => row.Id, row => row.VerifiedCutPlan), scopeName);
                string result = cancelled
                    ? (keptChanges ? "Cancelled: completed pipes kept in one Undo step." : "Cancelled: all model changes rolled back.")
                    : (commit ? "Applied. One Undo restores this batch." : "Preview complete; trial model changes rolled back.");
                if (cancelled) result += " " + (targets.Count - completed) + " pipes not processed. Preview again before Apply.";
                DiagnosticLog.Write("Batch finished " + batch, result);
                RefreshRows(result);
                progressText.Text = (cancelled ? "Cancelled " : "Completed ") + completed + " / " + targets.Count;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("Batch " + batch, ex);
                scanValid = false;
                ClearSnapshot();
                if (groupFinished)
                {
                    foreach (AuditRecord record in records)
                    {
                        record.FinalizeResult(keptChanges, !commit);
                        if (!audit.Contains(record)) audit.Add(record);
                    }
                    PersistAudit();
                    throw new InvalidOperationException((keptChanges ? "Apply was committed" : "Model changes were rolled back")
                        + " but result reporting failed. Export the audit and Scan again. " + ex.Message, ex);
                }
                takeoutCache.Clear();
                var recordIds = new HashSet<long>(records.Select(record => record.OriginalId));
                foreach (Row row in targets)
                {
                    row.Passed = false;
                    row.ResultPipeIds.Clear();
                    row.ResultUnionIds.Clear();
                    row.Status = row.WithWarnings("Failed: batch rolled back; scan again");
                    Engine.SetLengths(row, new List<double> { row.Length_m });
                    row.AddLog(ex.ToString());
                    if (!recordIds.Contains(row.Id)) records.Add(AuditRecord.Capture(row, batch, commit ? "Apply" : "Preview", scopeName, doc.Title, false, session));
                }
                foreach (AuditRecord record in records)
                {
                    record.FinalizeResult(false, false);
                    record.Status = "Batch rolled back: " + ex.Message;
                }
                audit.AddRange(records);
                PersistAudit();
                RefreshRows("Batch rolled back. Scan again.");
                throw;
            }
            finally
            {
                SetRunning(false);
                if (DiagnosticLog.LastWriteError.Length > 0)
                    SetOperationNote(DiagnosticLog.LastWriteError);
            }
        }

        private void AddUnprocessed(IEnumerable<Row> unprocessed, List<AuditRecord> records, string batch, bool commit, string scopeName)
        {
            foreach (Row row in unprocessed)
            {
                row.Passed = false;
                row.VerifiedCutPlan = null;
                row.ResultPipeIds.Clear();
                row.ResultUnionIds.Clear();
                row.Warnings.Clear();
                row.Status = "Not processed (cancelled)";
                Engine.SetLengths(row, new List<double> { row.Length_m });
                row.AddLog("Not processed (cancelled): no split attempted in batch " + batch);
                AuditRecord record = AuditRecord.Capture(row, batch, commit ? "Apply" : "Preview", scopeName, doc.Title, false, session);
                record.Outcome = "Not processed (cancelled)";
                records.Add(record);
            }
        }

        private void MarkCancelledRollback(IEnumerable<Row> completed, List<AuditRecord> records)
        {
            var byId = records.ToDictionary(record => record.OriginalId);
            foreach (Row row in completed)
            {
                row.ResultPipeIds.Clear();
                row.ResultUnionIds.Clear();
                if (row.Passed)
                {
                    row.Status = row.WithWarnings("Cancelled: rolled back; unchanged");
                    AuditRecord record = byId[row.Id];
                    record.Status = row.Status;
                    record.Outcome = "Cancelled - batch rolled back";
                }
                row.Passed = false;
                Engine.SetLengths(row, new List<double> { row.Length_m });
            }
        }
    }
}
