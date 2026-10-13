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
        private void InitializePhase2()
        {
            session=new SessionInfo {RevitVersion=doc.Application.VersionNumber+" / "+doc.Application.VersionBuild,WindowsUser=Environment.UserDomainName+"\\"+Environment.UserName,DocumentPath=doc.PathName};
            DiagnosticLog.BeginSession(session);sessionAudit=new AuditSessionFile(SettingsStore.Folder,session.Started);
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
            strictMode.Checked=saved.Strict;
            scope.SelectedIndex = saved.ActiveViewOnly ? 2 : (saved.SelectedOnly ? 1 : 0);
            remainder.SelectedItem=saved.Remainder;executionScope.SelectedIndex=0;
            Rectangle screen=WF.Screen.FromPoint(WF.Cursor.Position).WorkingArea;
            Width=Math.Max(MinimumSize.Width,Math.Min(saved.WindowWidth,screen.Width));Height=Math.Max(MinimumSize.Height,Math.Min(saved.WindowHeight,screen.Height));
            if(saved.Maximized) WindowState=WF.FormWindowState.Maximized;
            remainder.SelectedIndexChanged+=(s,e)=>InvalidateScan();
            executionScope.SelectedIndexChanged+=(s,e)=>UpdateActionButtons();

        }
        private void SaveSettings()
        {
            try
            {
                if(minimum.Value<=maximum.Value) {saved.MaximumM=(double)maximum.Value;saved.MinimumM=(double)minimum.Value;}
                saved.Remainder=CurrentPlacement().ToString();saved.Strict=strictMode.Checked;saved.SelectedOnly=scope.SelectedIndex==1;
                saved.ActiveViewOnly = scope.SelectedIndex == 2;
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
            SetOperationNote("Loaded " + rules.Rules.Count + " rules. Click Scan to resolve per-pipe limits.");
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
        private List<Row> VerifiedRows()
        {
            if(previewSnapshot==null) return new List<Row>();
            var ids=new HashSet<long>(previewSnapshot.VerifiedIds);return rows.Where(row=>ids.Contains(row.Id)).ToList();
        }
        private void LoadRulesSnapshot()
        {
            rules=SettingsStore.LoadRules(out scannedRulesText);
            ShowRuleNotes(rules.ShadowWarnings(), scannedRulesText);

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
