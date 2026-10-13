using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Rectangle = System.Drawing.Rectangle;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using WF = System.Windows.Forms;

namespace PipeSplitter2024
{
    public partial class SplitForm
    {
        internal ICollection<ElementId> SelectionToApply;
        private ICollection<ElementId> pickedIds;
        private WF.ComboBox remainder = new WF.ComboBox();
        private WF.ComboBox executionScope = new WF.ComboBox();
        private bool scanValid, running;
        private RuleFile rules = new RuleFile();
        private UserSettings saved = new UserSettings();
        private readonly List<AuditRecord> audit = new List<AuditRecord>();

        private RemainderPlacement CurrentPlacement() { return SettingsStore.ParsePlacement(remainder.SelectedItem.ToString()); }
        private void InitializePhase2(WF.TableLayoutPanel layout)
        {
            var options = new WF.FlowLayoutPanel { Dock=WF.DockStyle.Fill };
            layout.Controls.Add(options,0,2);
            options.Controls.Add(new WF.Label { Text="Remainder placement",AutoSize=true,Padding=new WF.Padding(0,8,0,0) });
            remainder.DropDownStyle=WF.ComboBoxStyle.DropDownList;remainder.Width=110;
            remainder.Items.AddRange(new object[]{"End","Start","Balanced"});remainder.SelectedIndex=0;options.Controls.Add(remainder);
            options.Controls.Add(new WF.Label { Text="Apply to",AutoSize=true,Padding=new WF.Padding(12,8,0,0) });
            executionScope.DropDownStyle=WF.ComboBoxStyle.DropDownList;executionScope.Width=160;
            executionScope.Items.AddRange(new object[]{"Visible rows","All eligible"});executionScope.SelectedIndex=0;options.Controls.Add(executionScope);
            Button(options,"Pick pipes in Revit",PickPipes);
            Button(options,"Open rules JSON",OpenRules);
            Button(options,"Reload rules",ReloadRules);
            options.Controls.Add(new WF.Label {Text="Start / End follow the stored Revit curve direction. Rules are applied when you Scan.",AutoSize=true});
            try
            {
                if(File.Exists(SettingsStore.SettingsPath))
                {
                    saved=SettingsStore.Read<UserSettings>(SettingsStore.SettingsPath);
                    if(saved==null) throw new ArgumentException("Settings file is null");saved.Validate();
                }
            }
            catch(Exception ex) {DiagnosticLog.Error("Load settings",ex);saved=new UserSettings();Shown+=(s,e)=>WF.MessageBox.Show(this,"Saved settings could not be loaded. Defaults are active.\n"+ex.Message,"Pipe Splitter");}
            maximum.Value=(decimal)saved.MaximumM;minimum.Value=(decimal)saved.MinimumM;
            strictMode.Checked=saved.Strict;scope.SelectedIndex=saved.SelectedOnly?1:0;
            remainder.SelectedItem=saved.Remainder;executionScope.SelectedIndex=saved.AllEligible?1:0;
            Rectangle screen=WF.Screen.FromPoint(WF.Cursor.Position).WorkingArea;
            Width=Math.Max(MinimumSize.Width,Math.Min(saved.WindowWidth,screen.Width));Height=Math.Max(MinimumSize.Height,Math.Min(saved.WindowHeight,screen.Height));
            if(saved.Maximized) WindowState=WF.FormWindowState.Maximized;
            remainder.SelectedIndexChanged+=(s,e)=>InvalidateScan();
            executionScope.SelectedIndexChanged+=(s,e)=>UpdateActionButtons();
            FormClosing+=(s,e)=>SaveSettings();
        }
        private void SaveSettings()
        {
            try
            {
                if(minimum.Value<=maximum.Value) {saved.MaximumM=(double)maximum.Value;saved.MinimumM=(double)minimum.Value;}
                saved.Remainder=CurrentPlacement().ToString();saved.Strict=strictMode.Checked;saved.SelectedOnly=scope.SelectedIndex==1;saved.AllEligible=executionScope.SelectedIndex==1;
                Rectangle bounds=WindowState==WF.FormWindowState.Normal?Bounds:RestoreBounds;
                saved.WindowWidth=Math.Max(950,bounds.Width);saved.WindowHeight=Math.Max(650,bounds.Height);saved.Maximized=WindowState==WF.FormWindowState.Maximized;
                saved.Validate();SettingsStore.Save(SettingsStore.SettingsPath,saved);
            }
            catch(Exception ex) {DiagnosticLog.Error("Save settings",ex);WF.MessageBox.Show(this,"Could not save preferences: "+ex.Message,"Pipe Splitter");}
        }
        private void OpenRules()
        {
            if(!File.Exists(SettingsStore.RulesPath)) SettingsStore.Save(SettingsStore.RulesPath,new RuleFile());
            Process.Start(new ProcessStartInfo("notepad.exe","\""+SettingsStore.RulesPath+"\""){UseShellExecute=true});
            InvalidateScan();
        }
        private void ReloadRules()
        {
            InvalidateScan();rules=SettingsStore.LoadRules();
            summary.Text="Loaded "+rules.Rules.Count+" rules. Click Scan to resolve per-pipe limits.";
        }
        private void PickPipes()
        {
            try
            {
                Hide();
                var references=ui.Selection.PickObjects(ObjectType.Element,new PipePickFilter(),"Pick pipes, then click Finish (Esc cancels).");
                var selected=references.Select(reference=>reference.ElementId).Distinct().ToList();
                if(selected.Count>0) {pickedIds=selected;ui.Selection.SetElementIds(selected);scope.SelectedIndex=1;InvalidateScan();}
            }
            catch(Autodesk.Revit.Exceptions.OperationCanceledException) { /* Preserve the previous selection and scan. */ }
            finally {Show();Activate();}
        }
        private void SelectRowsInRevit(bool problems)
        {
            IEnumerable<Row> chosen=problems?rows.Where(r=>r.Status!=null && (r.Status.StartsWith("Failed") || r.Status.StartsWith("Skipped") || r.Status.StartsWith("Blocked"))):grid.SelectedRows.Cast<WF.DataGridViewRow>().Select(item=>item.DataBoundItem as Row).Where(r=>r!=null);
            var ids=new HashSet<long>();
            foreach(var row in chosen)
            {
                if(row.Status=="Applied" && row.ResultPipeIds.Count>0) foreach(long id in row.ResultPipeIds) ids.Add(id);
                else ids.Add(row.Id);
            }
            SelectionToApply=ids.Select(id=>new ElementId(id)).Where(id=>doc.GetElement(id)!=null).ToList();
            if(SelectionToApply.Count==0) {SelectionToApply=null;WF.MessageBox.Show(this,"No existing model elements match this selection.","Pipe Splitter");return;}
            Close(); // Command sets the Revit selection after the modal dialog has closed.
        }
        private List<Row> ExecutionRows()
        {
            return (executionScope.SelectedIndex==1?rows:visibleRows).Where(r=>r.Eligible).ToList();
        }
        private void UpdateActionButtons()
        {
            if(preview==null || apply==null) return;
            var candidates=ExecutionRows();
            preview.Enabled=!running && scanValid && candidates.Count>0;
            apply.Enabled=!running && scanValid && candidates.Count>0 && candidates.All(r=>r.Passed);
        }
        private void Run(bool commit)
        {
            if(!scanValid) throw new InvalidOperationException("Scan first with the current settings and rules");
            var targets=ExecutionRows();
            if(targets.Count==0) throw new InvalidOperationException("No eligible pipes in the current execution scope");
            if(commit && targets.Any(r=>!r.Passed)) throw new InvalidOperationException("Preview all eligible pipes in the current scope before Apply");
            string scopeName=executionScope.SelectedItem.ToString();
            if(commit && WF.MessageBox.Show(this,"Apply to: "+scopeName+"\nExactly "+targets.Count+" eligible pipes will be processed.\nEach pipe uses its scanned rule limits.\nOne Undo restores the batch.","Apply pipe splitting",WF.MessageBoxButtons.YesNo,WF.MessageBoxIcon.Question)!=WF.DialogResult.Yes) return;
            string batch=Guid.NewGuid().ToString("N");var records=new List<AuditRecord>();
            DiagnosticLog.Write(commit?"Apply":"Preview","Batch="+batch+"; scope="+scopeName+"; strict="+strictMode.Checked+"; pipes="+targets.Count);
            running=true;scan.Enabled=false;UpdateActionButtons();UseWaitCursor=true;int ok=0,failed=0;bool groupFinished=false;
            try
            {
                using(var group=new TransactionGroup(doc,"Pipe Splitter - "+(commit?"Apply":"Preview")))
                {
                    group.Start();
                    foreach(var row in targets)
                    {
                        row.Passed=false;string error;
                        bool success=Engine.Process(doc,row,row.MaxFinished_m,row.Minimum_m,strictMode.Checked,takeoutCache,out error);
                        if(success) {row.Status=commit?"Applied":"Preview OK";ok++;}
                        else {row.Status="Failed: "+error;Engine.SetLengths(row,new List<double>{row.Length_m});failed++;}
                        records.Add(AuditRecord.Capture(row,batch,commit?"Apply":"Preview",scopeName,doc.Title,success));
                        summary.Text=(commit?"Applying":"Previewing")+" "+(ok+failed)+" / "+targets.Count;summary.Refresh();
                    }
                    if(commit) {if(group.Assimilate()!=TransactionStatus.Committed) throw new Exception("Group commit failed");}
                    else if(group.RollBack()!=TransactionStatus.RolledBack) throw new Exception("Preview rollback failed");
                    groupFinished=true;
                }
                foreach(var record in records) record.FinalizeResult(commit,!commit);
                audit.AddRange(records);
                PersistAudit();
                foreach(var row in targets)
                {
                    if(!commit) {row.ResultPipeIds.Clear();row.ResultUnionIds.Clear();}
                    else if(row.Status=="Applied") {row.Eligible=false;row.Passed=false;}
                }
                // Apply can change connectors shared with hidden rows: require a fresh preview for them.
                if(commit) foreach(var row in rows) row.Passed=false;
                RefreshRows(commit?"APPLIED":"EXACT PREVIEW - rolled back");
                if(commit) WF.MessageBox.Show(this,"Applied: "+ok+"\nFailed and unchanged: "+failed+"\nAudit CSV records this batch. Unprocessed rows require Preview before Apply.","Pipe Splitter");
            }
            catch(Exception ex)
            {
                if(groupFinished)
                {
                    DiagnosticLog.Error("Batch finished; post-operation UI failure "+batch,ex);
                    foreach(var record in records) {record.FinalizeResult(commit,!commit);if(!audit.Contains(record)) audit.Add(record);}
                    PersistAudit();
                    scanValid=false;
                    throw new InvalidOperationException((commit?"Apply was committed":"Preview was rolled back")+" but the result display failed. Export the audit and scan again. "+ex.Message,ex);
                }
                DiagnosticLog.Error("Batch rolled back "+batch,ex);takeoutCache.Clear();scanValid=false;
                foreach(var row in targets)
                {
                    row.Passed=false;row.ResultPipeIds.Clear();row.ResultUnionIds.Clear();row.Status="Failed: batch rolled back; scan again";
                    Engine.SetLengths(row,new List<double>{row.Length_m});row.AddLog(ex.ToString());
                    if(!records.Any(record=>record.OriginalId==row.Id)) records.Add(AuditRecord.Capture(row,batch,commit?"Apply":"Preview",scopeName,doc.Title,false));
                }
                foreach(var record in records) {record.FinalizeResult(false,false);record.Status="Batch rolled back: "+ex.Message;}
                audit.AddRange(records);PersistAudit();RefreshRows("ROLLED BACK");throw;
            }
            finally
            {
                running=false;UseWaitCursor=false;scan.Enabled=true;UpdateActionButtons();
                if(DiagnosticLog.LastWriteError.Length>0) WF.MessageBox.Show(this,DiagnosticLog.LastWriteError,"Diagnostic log unavailable");
            }
        }
        private void ExportAudit()
        {
            if(audit.Count==0) throw new InvalidOperationException("Run Preview or Apply first to create audit records");
            using(var dialog=new WF.SaveFileDialog {Filter="CSV files|*.csv",FileName="PipeSplitter-audit.csv"})
                if(dialog.ShowDialog(this)==WF.DialogResult.OK) AuditCsv.Write(dialog.FileName,audit);
        }
        private void PersistAudit()
        {
            try {Directory.CreateDirectory(SettingsStore.Folder);AuditCsv.Write(Path.Combine(SettingsStore.Folder,"audit.csv"),audit);}
            catch(Exception ex) {DiagnosticLog.Error("Save audit",ex);WF.MessageBox.Show(this,"The model operation finished, but the audit file could not be saved. Use Export audit CSV.\n"+ex.Message,"Pipe Splitter");}
        }
    }
}
