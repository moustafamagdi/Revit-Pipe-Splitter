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
        private PreviewSnapshot previewSnapshot;
        private string scannedRulesText;
        private SessionInfo session;
        private AuditSessionFile sessionAudit;
        private int auditRowsWritten;

        private RemainderPlacement CurrentPlacement() { return SettingsStore.ParsePlacement(remainder.SelectedItem.ToString()); }
        private void InitializePhase2(WF.TableLayoutPanel layout)
        {
            session=new SessionInfo {RevitVersion=doc.Application.VersionNumber+" / "+doc.Application.VersionBuild,WindowsUser=Environment.UserDomainName+"\\"+Environment.UserName,DocumentPath=doc.PathName};
            DiagnosticLog.BeginSession(session);sessionAudit=new AuditSessionFile(SettingsStore.Folder,session.Started);
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
            Button(options,"Export scan combinations",ExportCombinations);
            Button(options,"Insert example rule",InsertExampleRule);
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
            remainder.SelectedItem=saved.Remainder;executionScope.SelectedIndex=0;
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
                saved.Remainder=CurrentPlacement().ToString();saved.Strict=strictMode.Checked;saved.SelectedOnly=scope.SelectedIndex==1;
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
            InvalidateScan();LoadRulesSnapshot();
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
                if(row.Status.StartsWith("Applied") && row.ResultPipeIds.Count>0) foreach(long id in row.ResultPipeIds) ids.Add(id);
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
            if(!running && previewSnapshot!=null && previewSnapshot.IncludesNewIds(candidates.Select(row=>row.Id))) ClearSnapshot();
            preview.Enabled=!running && scanValid && candidates.Count>0;
            apply.Enabled=!running && scanValid && previewSnapshot!=null && previewSnapshot.Count>0;
            ShowSnapshotCount();
        }
        private void Run(bool commit)
        {
            if(!scanValid) throw new InvalidOperationException("Scan first with the current settings and rules");
            EnsureRulesUnchanged();UpdateActionButtons();
            var targets=commit?VerifiedRows():ExecutionRows();
            if(targets.Count==0) throw new InvalidOperationException("No eligible pipes in the current execution scope");
            if(commit && (previewSnapshot==null || targets.Count!=previewSnapshot.Count || targets.Any(r=>!r.Passed || r.VerifiedCutPlan==null))) throw new InvalidOperationException("Run Preview again to establish the verified set");
            string scopeName=commit?"Verified Preview set ("+previewSnapshot.Scope+")":executionScope.SelectedItem.ToString();
            if(commit && WF.MessageBox.Show(this,"Verified set: "+targets.Count+" pipes\nApply uses exactly this Preview set, including any now hidden by filters.\nFirst attempts reuse the verified cut plans. One Undo restores the batch.","Apply pipe splitting",WF.MessageBoxButtons.YesNo,WF.MessageBoxIcon.Question)!=WF.DialogResult.Yes) return;
            if(commit) foreach(var row in targets) row.VerifiedCutPlan=previewSnapshot.PlanFor(row.Id);
            else ClearSnapshot();
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
                        bool success=Engine.Process(doc,row,row.MaxFinished_m,row.Minimum_m,strictMode.Checked,takeoutCache,commit,out error);
                        if(success) {row.Status=row.WithWarnings(commit?"Applied":"Preview OK");ok++;}
                        else {row.Status=row.WithWarnings("Failed: "+error);Engine.SetLengths(row,new List<double>{row.Length_m});failed++;}
                        records.Add(AuditRecord.Capture(row,batch,commit?"Apply":"Preview",scopeName,doc.Title,success,session));
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
                    if(!commit) {row.ResultPipeIds.Clear();row.ResultUnionIds.Clear();if(row.Passed) row.VerifiedCutPlan=new List<double>(row.Plan);}
                    else if(row.Status.StartsWith("Applied")) {row.Eligible=false;row.Passed=false;}
                }
                // Apply can change connectors shared with hidden rows: require a fresh preview for them.
                if(commit) ClearSnapshot();
                else previewSnapshot=new PreviewSnapshot(targets.Select(row=>row.Id),targets.Where(row=>row.Passed).ToDictionary(row=>row.Id,row=>row.VerifiedCutPlan),scopeName);
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
                    scanValid=false;ClearSnapshot();
                    throw new InvalidOperationException((commit?"Apply was committed":"Preview was rolled back")+" but the result display failed. Export the audit and scan again. "+ex.Message,ex);
                }
                DiagnosticLog.Error("Batch rolled back "+batch,ex);takeoutCache.Clear();scanValid=false;ClearSnapshot();
                foreach(var row in targets)
                {
                    row.Passed=false;row.ResultPipeIds.Clear();row.ResultUnionIds.Clear();row.Status=row.WithWarnings("Failed: batch rolled back; scan again");
                    Engine.SetLengths(row,new List<double>{row.Length_m});row.AddLog(ex.ToString());
                    if(!records.Any(record=>record.OriginalId==row.Id)) records.Add(AuditRecord.Capture(row,batch,commit?"Apply":"Preview",scopeName,doc.Title,false,session));
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
            using(var dialog=new WF.SaveFileDialog {Filter="CSV files|*.csv",FileName="PipeSplitter-audit-"+session.Started.ToString("yyyyMMdd_HHmmss")+".csv",OverwritePrompt=false})
                if(dialog.ShowDialog(this)==WF.DialogResult.OK) AuditCsv.Write(dialog.FileName,audit);
        }
        private void PersistAudit()
        {
            try {sessionAudit.Append(AuditCsv.Header,AuditCsv.Lines(audit.Skip(auditRowsWritten)));auditRowsWritten=audit.Count;}
            catch(Exception ex) {DiagnosticLog.Error("Save audit",ex);WF.MessageBox.Show(this,"The model operation finished, but the audit file could not be saved. Use Export audit CSV.\n"+ex.Message,"Pipe Splitter");}
        }
        private void ClearSnapshot()
        {
            previewSnapshot=null;foreach(var row in rows) {row.Passed=false;row.VerifiedCutPlan=null;}
            ShowSnapshotCount();
        }
        private void ShowSnapshotCount()
        {
            string text=filterInfo.Text;
            if(text.StartsWith("Verified set:",StringComparison.Ordinal)) {int marker=text.IndexOf(" | ",StringComparison.Ordinal);text=marker<0?"":text.Substring(marker+3);}
            filterInfo.Text="Verified set: "+(previewSnapshot==null?0:previewSnapshot.Count)+" pipes | "+text;
        }
        private List<Row> VerifiedRows()
        {
            if(previewSnapshot==null) return new List<Row>();
            var ids=new HashSet<long>(previewSnapshot.VerifiedIds);return rows.Where(row=>ids.Contains(row.Id)).ToList();
        }
        private void LoadRulesSnapshot()
        {
            rules=SettingsStore.LoadRules(out scannedRulesText);
            var warnings=rules.ShadowWarnings();
            if(warnings.Count>0) {DiagnosticLog.Write("Shadowed rules",string.Join(Environment.NewLine,warnings));WF.MessageBox.Show(this,string.Join(Environment.NewLine,warnings),"Rule warnings");}
        }
        private void EnsureRulesUnchanged()
        {
            if(!File.Exists(SettingsStore.RulesPath) || File.ReadAllText(SettingsStore.RulesPath)!=scannedRulesText) {
                InvalidateScan();throw new InvalidOperationException("Rules file changed. Scan and Preview again before Apply.");
            }
        }
        private void ExportCombinations()
        {
            if(!scanValid) throw new InvalidOperationException("Scan first to export combinations");
            using(var dialog=new WF.SaveFileDialog {Filter="CSV files|*.csv",FileName="PipeSplitter-scan-combinations.csv"}) {
                if(dialog.ShowDialog(this)!=WF.DialogResult.OK) return;
                var lines=new List<string>{"PipeType,SystemType,DiameterMm,PipeCount"};
                foreach(var group in rows.GroupBy(row=>new {row.Type,row.SystemType,row.Diameter_mm}).OrderBy(group=>group.Key.Type).ThenBy(group=>group.Key.SystemType).ThenBy(group=>group.Key.Diameter_mm))
                    lines.Add(string.Join(",",new object[]{group.Key.Type,group.Key.SystemType,group.Key.Diameter_mm,group.Count()}.Select(AuditCsv.Cell)));
                File.WriteAllLines(dialog.FileName,lines,new System.Text.UTF8Encoding(true));
            }
        }
        private void InsertExampleRule()
        {
            var file=SettingsStore.LoadRules();if(file.Rules.Count!=0) throw new InvalidOperationException("Insert example rule is available only when the rules list is empty");
            file.Rules.Add(new PipeRule {Name="Example - edit and enable",Enabled=false,PipeType="REPLACE WITH PIPE TYPE NAME",MaximumM=6,MinimumM=.5});
            SettingsStore.Save(SettingsStore.RulesPath,file);InvalidateScan();
            WF.MessageBox.Show(this,"Inserted a disabled example rule. Open rules JSON, edit the type name and limits, enable it, then Scan and Preview.","Pipe Splitter");
        }
    }
}
