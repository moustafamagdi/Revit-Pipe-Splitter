using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;
using System.Drawing;
using Color = System.Drawing.Color;
using WF = System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
[assembly: System.Reflection.AssemblyVersion("1.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.0.0")]

namespace PipeSplitter2024 {
 [Transaction(TransactionMode.Manual)]
 public class Command : IExternalCommand {
  public Result Execute(ExternalCommandData data, ref string message, ElementSet elements) {
   try {
    UIDocument ui = data.Application.ActiveUIDocument;
    if (ui == null || ui.Document.IsFamilyDocument || ui.Document.IsReadOnly) { TaskDialog.Show("Pipe Splitter", "Open an editable Revit project first."); return Result.Cancelled; }
    using (var form = new SplitForm(ui)) form.ShowDialog(new WindowOwner(data.Application.MainWindowHandle));
    return Result.Succeeded;
   } catch (Exception ex) { message = ex.ToString(); return Result.Failed; }
  }
 }
 class WindowOwner : WF.IWin32Window { public IntPtr Handle {get; private set;} public WindowOwner(IntPtr h) {Handle=h;} }
 public class Row {
  public long Id {get;set;} public string Type {get;set;} public double Length_m {get;set;} public int Pieces {get;set;} public int Unions {get;set;}
  public double Min_m {get;set;} public double Max_m {get;set;} public string Lengths_m {get;set;} public string Status {get;set;}
  internal XYZ Start, End; internal List<double> Plan; internal bool Eligible, Passed; internal double PlanMinimum;
 }
 class StrictFailures : IFailuresPreprocessor {
  public string Detail = "";
  public FailureProcessingResult PreprocessFailures(FailuresAccessor a) {
   var failures=a.GetFailureMessages();
   if(failures.Count>0) {Detail=string.Join("; ",failures.Select(f=>f.GetDescriptionText())); return FailureProcessingResult.ProceedWithRollBack;}
   return FailureProcessingResult.Continue;
  }
 }
 public static class Engine {
  internal const double FeetPerMetre = 1.0 / 0.3048;
  internal static double Metres(double f) {return f * 0.3048;}
  internal static List<Connector> Ends(Pipe p) {return p.ConnectorManager.Connectors.Cast<Connector>().Where(c=>c.ConnectorType==ConnectorType.End).ToList();}
  internal static Line Axis(Pipe p) {return ((LocationCurve)p.Location).Curve as Line;}
  internal static void SetLengths(Row r, List<double> lengths) {r.Pieces=lengths.Count;r.Unions=lengths.Count-1;r.Min_m=lengths.Min();r.Max_m=lengths.Max();r.Lengths_m=string.Join(" + ",lengths.Select(v=>v.ToString("0.000000",CultureInfo.InvariantCulture)));}
  internal static string Exclusion(Document d, Pipe p) {
   if(p.IsPlaceholder) return "Skipped: placeholder";
   if(Axis(p)==null) return "Skipped: non-straight pipe";
   if(p.Pinned) return "Skipped: pinned";
   if(p.GroupId!=ElementId.InvalidElementId) return "Skipped: group member";
   if(p.DesignOption!=null) return "Skipped: design option";
   if(p.ConnectorManager.Connectors.Cast<Connector>().Any(c=>c.ConnectorType!=ConnectorType.End && c.ConnectorType!=ConnectorType.Logical)) return "Skipped: branch/tap connector";
   if(d.IsWorkshared && WorksharingUtils.GetCheckoutStatus(d,p.Id)==CheckoutStatus.OwnedByOtherUser) return "Skipped: owned by another user";
   return null;
  }
  internal static List<Row> Scan(Document d, ICollection<ElementId> selected, double max, double min) {
   var pipes=new FilteredElementCollector(d).OfClass(typeof(Pipe)).Cast<Pipe>();
   if(selected!=null) pipes=pipes.Where(p=>selected.Contains(p.Id));
   var rows=new List<Row>();
   foreach(var p in pipes.OrderBy(p=>p.Id.Value)) {
    var curve=((LocationCurve)p.Location).Curve;
    var r=new Row {Id=p.Id.Value,Type=d.GetElement(p.GetTypeId()).Name,Length_m=Metres(curve.Length),Start=curve.GetEndPoint(0),End=curve.GetEndPoint(1)};
    SetLengths(r,new List<double>{r.Length_m});
    string skip=Exclusion(d,p);
    if(skip!=null) r.Status=skip;
    else if(r.Length_m<min-1e-7) r.Status="Existing short pipe: unchanged";
    else if(r.Length_m<=max+1e-7) r.Status="Within limits: unchanged";
    else try {r.Plan=Planner.Split(r.Length_m,max,min);SetLengths(r,r.Plan);r.Eligible=true;r.PlanMinimum=min;r.Status="Estimated: needs preview";} catch(Exception ex) {r.Status="Blocked: "+ex.Message;}
    rows.Add(r);
   }
   return rows;
  }
  static List<string> ExternalRefs(Connector c, HashSet<long> own) {return c.AllRefs.Cast<Connector>().Where(x=>x.ConnectorType==ConnectorType.End && !own.Contains(x.Owner.Id.Value)).Select(x=>x.Owner.Id.Value+":"+x.Id).OrderBy(x=>x).ToList();}
  sealed class InsulationSpec {
   public ElementId TypeId; public double Thickness;
  }
  static InsulationSpec ReadInsulation(Document d, ElementId pipeId) {
   var insulationIds=InsulationLiningBase.GetInsulationIds(d,pipeId);
   if(insulationIds.Count==0) return null;
   if(insulationIds.Count!=1) throw new Exception("Insulation: unexpected number of layers; pipe unchanged");
   var insulation=d.GetElement(insulationIds.First()) as PipeInsulation;
   if(insulation==null) throw new Exception("Insulation: cannot read original pipe insulation");
   return new InsulationSpec {TypeId=insulation.GetTypeId(),Thickness=insulation.Thickness};
  }
  static void PreserveInsulation(Document d, IList<ElementId> pipeIds, InsulationSpec original) {
   foreach(var pipeId in pipeIds) {
    var current=ReadInsulation(d,pipeId);
    // BreakCurve can propagate insulation. Keep it when present and only create missing hosts.
    if(original!=null && current==null) {
     PipeInsulation.Create(d,pipeId,original.TypeId,original.Thickness);
    } else if(original==null && current!=null) {
     throw new Exception("Insulation: unexpected insulation appeared on an uninsulated pipe");
    } else if(original!=null && (current.TypeId!=original.TypeId || Math.Abs(current.Thickness-original.Thickness)>1e-7)) {
     throw new Exception("Insulation: inherited type or thickness differs from the original");
    }
   }
   d.Regenerate();
   foreach(var pipeId in pipeIds) {
    var actual=ReadInsulation(d,pipeId);
    if(original!=null && (actual==null || actual.TypeId!=original.TypeId || Math.Abs(actual.Thickness-original.Thickness)>1e-7))
     throw new Exception("Insulation: final verification failed for pipe "+pipeId.Value);
    if(original==null && actual!=null) throw new Exception("Insulation: final verification failed for uninsulated pipe "+pipeId.Value);
   }
  }
  // Each pipe is atomic. Failed union placement or failed validation rolls back all of its cuts.
  internal static bool Process(Document d, Row r, double max, double min, out string error) {
   error="";
   var plans=new List<double>(r.Plan);
   const double finishedTolerance=1e-7; // metres: numerical tolerance, not a fabrication allowance
   for(int attempt=0;attempt<8;attempt++) {
    using(var tx=new Autodesk.Revit.DB.Transaction(d,"Split pipe "+r.Id)) {
     var handler=new StrictFailures();
     try {
      var original=d.GetElement(new ElementId(r.Id)) as Pipe;
      if(original==null || Exclusion(d,original)!=null) throw new Exception("Pipe no longer editable");
      var axis=Axis(original);
      if(axis.GetEndPoint(0).DistanceTo(r.Start)>1e-6 || axis.GetEndPoint(1).DistanceTo(r.End)>1e-6) throw new Exception("Geometry changed; scan again");
      var insulation=ReadInsulation(d,original.Id);
      var endpoints=Ends(original); var own=new HashSet<long>{r.Id};
      var references=endpoints.Select(c=>ExternalRefs(c,own)).ToList();var origins=endpoints.Select(c=>c.Origin).ToList();
      tx.Start();tx.SetFailureHandlingOptions(tx.GetFailureHandlingOptions().SetFailuresPreprocessor(handler).SetClearAfterRollback(true));
      var ids=new List<ElementId>{original.Id}; var joins=new List<Tuple<ElementId,ElementId,XYZ>>();double distance=0;
      for(int i=0;i<plans.Count-1;i++) {
       distance+=plans[i]*FeetPerMetre; XYZ point=r.Start+(r.End-r.Start).Normalize()*distance;
       Pipe container=ids.Select(id=>d.GetElement(id) as Pipe).FirstOrDefault(p=> {var l=Axis(p);return l.Distance(point)<1e-6 && l.GetEndPoint(0).DistanceTo(point)>1e-6 && l.GetEndPoint(1).DistanceTo(point)>1e-6;});
       if(container==null) throw new Exception("Cut point is outside the remaining pipe");
       ElementId added=PlumbingUtils.BreakCurve(d,container.Id,point);
       if(added==ElementId.InvalidElementId) throw new Exception("Revit rejected pipe break");
       ids.Add(added);joins.Add(Tuple.Create(container.Id,added,point));d.Regenerate();
      }
      // Resolve connectors by cut point after all breaks; a stored segment id can change ownership of a later joint.
      foreach(var join in joins) {
       var pair=ids.SelectMany(id=>Ends((Pipe)d.GetElement(id))).Where(c=>c.Origin.DistanceTo(join.Item3)<1e-5).ToList();
       if(pair.Count!=2) throw new Exception("Cannot locate both cut connectors");
       if(pair[0].IsConnectedTo(pair[1])) pair[0].DisconnectFrom(pair[1]);
       var union=d.Create.NewUnionFitting(pair[0],pair[1]);
       if(union==null) throw new Exception("Routing preferences did not create a union");
       d.Regenerate();
       if(!pair.All(c=>c.AllRefs.Cast<Connector>().Any(x=>x.Owner.Id==union.Id))) throw new Exception("Union connection failed");
      }
      d.Regenerate();
      var pieces=ids.Select(id=>(Pipe)d.GetElement(id)).OrderBy(p=>Axis(p).Evaluate(0.5,true).DistanceTo(r.Start)).ToList();
      var actual=pieces.Select(p=>Metres(Axis(p).Length)).ToList();
      own=new HashSet<long>(ids.Select(id=>id.Value));
      for(int k=0;k<origins.Count;k++) {
       var end=pieces.SelectMany(p=>Ends(p)).FirstOrDefault(c=>c.Origin.DistanceTo(origins[k])<1e-6);
       if(end==null || !ExternalRefs(end,own).SequenceEqual(references[k])) throw new Exception("Original end connection or position changed");
      }
      // Union takeout is measured from the actual fitted geometry, never guessed from family names.
      // Greedily retain full finished lengths, redistributing only the remainder to satisfy the minimum.
      var targets=Planner.FinishedTargets(actual.Sum(),actual.Count,max,min);
      bool finished=actual.Select((length,i)=>Math.Abs(length-targets[i])).All(delta=>delta<=finishedTolerance);
      if(!finished) {
       var corrected=Planner.CompensateCuts(plans,actual,targets);
       if(Math.Abs(corrected.Sum()-r.Length_m)>1e-6) throw new Exception("Compensation would change the original route length");
       tx.RollBack();
       if(attempt==7) throw new Exception("Could not achieve finished pipe lengths after unions within 8 attempts; pipe unchanged");
       plans=corrected;continue;
      }
      if(actual.Min()<min-finishedTolerance || actual.Max()>max+finishedTolerance) throw new Exception("Finished pipe lengths are outside the specified limits");
      PreserveInsulation(d,ids,insulation);
      var status=tx.Commit();
      if(status!=TransactionStatus.Committed) throw new Exception("Revit rolled back: "+handler.Detail);
      r.Plan=new List<double>(plans);SetLengths(r,actual);r.Passed=true;return true;
     } catch(Exception ex) {if(tx.GetStatus()==TransactionStatus.Started) tx.RollBack();error=ex.Message;return false;}
    }
   }
   error="Unable to satisfy length limits";return false;
  }
 }
 public class SplitForm : WF.Form {
  readonly UIDocument ui; readonly Document doc;
  WF.NumericUpDown maximum=new WF.NumericUpDown(), minimum=new WF.NumericUpDown();
  WF.ComboBox scope=new WF.ComboBox(); WF.DataGridView grid=new WF.DataGridView();
  WF.Label summary=new WF.Label(), note=new WF.Label(); WF.Button scan, preview, apply, export;
  List<Row> rows=new List<Row>(); bool verified=false;
  List<Row> visibleRows=new List<Row>();
  readonly Dictionary<string,ColumnFilter> filters=new Dictionary<string,ColumnFilter>();
  string sortColumn="Id"; bool descending=false;
  WF.Label filterInfo=new WF.Label();
  sealed class ColumnFilter {public string Operator; public string Text; public decimal Number;}
  public SplitForm(UIDocument uidoc) {
   ui=uidoc;doc=ui.Document;Text="Pipe Splitter | Revit 2024 | Finished Length After Unions";Width=1250;Height=820;MinimumSize=new Size(950,650);StartPosition=WF.FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(244,247,250);
   var layout=new WF.TableLayoutPanel {Dock=WF.DockStyle.Fill,RowCount=6,ColumnCount=1,Padding=new WF.Padding(16)};
   layout.RowStyles.Add(new WF.RowStyle(WF.SizeType.Absolute,85));layout.RowStyles.Add(new WF.RowStyle(WF.SizeType.Absolute,65));layout.RowStyles.Add(new WF.RowStyle(WF.SizeType.Absolute,85));layout.RowStyles.Add(new WF.RowStyle(WF.SizeType.Absolute,65));layout.RowStyles.Add(new WF.RowStyle(WF.SizeType.Percent,100));layout.RowStyles.Add(new WF.RowStyle(WF.SizeType.Absolute,65));Controls.Add(layout);
   note.Text="Target length is the FINISHED pipe length AFTER unions (e.g. 6.000 m). Insulation is preserved.\nFull pieces meet the target; remainder pieces are redistributed to respect the minimum. All lengths are in metres.\nPreview measures union takeout and rolls back. Apply uses the compensated cut positions.";note.Dock=WF.DockStyle.Fill;note.Font=new Font(Font,FontStyle.Bold);layout.Controls.Add(note,0,0);
   var settings=new WF.FlowLayoutPanel {Dock=WF.DockStyle.Fill};layout.Controls.Add(settings,0,1);
   settings.Controls.Add(new WF.Label {Text="Finished target (m)",AutoSize=true,Padding=new WF.Padding(0,8,0,0)});
   ConfigureNumber(maximum,6);settings.Controls.Add(maximum);settings.Controls.Add(new WF.Label {Text="Minimum length (m)",AutoSize=true,Padding=new WF.Padding(12,8,0,0)});ConfigureNumber(minimum,0.5m);settings.Controls.Add(minimum);
   scope.DropDownStyle=WF.ComboBoxStyle.DropDownList;scope.Width=230;scope.Items.AddRange(new object[]{"All model pipes","Selected pipes only"});scope.SelectedIndex=0;settings.Controls.Add(scope);
   summary.Dock=WF.DockStyle.Fill;summary.Text="Set the maximum and minimum lengths, then click Scan.";layout.Controls.Add(summary,0,2);
   filterInfo.Dock=WF.DockStyle.Fill;filterInfo.AutoEllipsis=true;layout.Controls.Add(filterInfo,0,3);
   grid.Dock=WF.DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AutoSizeColumnsMode=WF.DataGridViewAutoSizeColumnsMode.DisplayedCells;grid.BackgroundColor=Color.White;grid.SelectionMode=WF.DataGridViewSelectionMode.FullRowSelect;grid.RowHeadersVisible=false;layout.Controls.Add(grid,0,4);
   grid.ColumnHeaderMouseClick+=(s,e)=> {
    if(e.ColumnIndex<0)return;
    string column=grid.Columns[e.ColumnIndex].DataPropertyName;
    if(e.Button==WF.MouseButtons.Left) {descending=sortColumn==column?!descending:false;sortColumn=column;ApplyView();}
    else if(e.Button==WF.MouseButtons.Right) {
     var menu=new WF.ContextMenuStrip();
     menu.Items.Add("Filter "+column+"...",null,(a,b)=>EditFilter(column));
     menu.Items.Add("Clear this filter",null,(a,b)=>{filters.Remove(column);ApplyView();});
     menu.Items.Add("Sort ascending",null,(a,b)=>{sortColumn=column;descending=false;ApplyView();});
     menu.Items.Add("Sort descending",null,(a,b)=>{sortColumn=column;descending=true;ApplyView();});
     menu.Closed+=(a,b)=>menu.Dispose();menu.Show(WF.Cursor.Position);
    }
   };
   var buttons=new WF.FlowLayoutPanel {Dock=WF.DockStyle.Fill,Padding=new WF.Padding(0,12,0,0)};layout.Controls.Add(buttons,0,5);
   scan=Button(buttons,"1. Scan",()=>Scan());preview=Button(buttons,"2. Preview",()=>Run(false));apply=Button(buttons,"3. Apply",()=>Run(true));export=Button(buttons,"Export visible CSV",()=>Export());Button(buttons,"Clear all filters",()=>{filters.Clear();ApplyView();});Button(buttons,"Close",()=>Close());
   preview.Enabled=apply.Enabled=export.Enabled=false;maximum.ValueChanged+=(s,e)=>InvalidateScan();minimum.ValueChanged+=(s,e)=>InvalidateScan();scope.SelectedIndexChanged+=(s,e)=>InvalidateScan();
   ApplyView();
  }
  void EditFilter(string column) {
   bool numeric=typeof(Row).GetProperty(column).PropertyType!=typeof(string);
   ColumnFilter existing;filters.TryGetValue(column,out existing);
   using(var dialog=new WF.Form {Text="Filter: "+column,Width=430,Height=230,StartPosition=WF.FormStartPosition.CenterParent,FormBorderStyle=WF.FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,Font=Font}) {
    var op=new WF.ComboBox {Left=20,Top=20,Width=370,DropDownStyle=WF.ComboBoxStyle.DropDownList};
    op.Items.AddRange(numeric?new object[]{"=","!=",">",">=","<","<="}:new object[]{"Contains","Does not contain","Equals","Not equal","Starts with","Ends with"});
    op.SelectedItem=existing==null?op.Items[0]:existing.Operator;
    var value=new WF.TextBox {Left=20,Top=60,Width=370,Text=existing==null?"":existing.Text};
    var hint=new WF.Label {Left=20,Top=92,Width=370,Height=30,Text=numeric?"Enter a number. Length columns use metres.":"Text matching is case-insensitive."};
    var accept=new WF.Button {Text="Apply filter",Left=20,Top=132,Width=115};
    var clear=new WF.Button {Text="Clear filter",Left=150,Top=132,Width=110};
    var cancel=new WF.Button {Text="Cancel",Left=275,Top=132,Width=110,DialogResult=WF.DialogResult.Cancel};
    accept.Click+=(s,e)=> {
     decimal number=0;
     if(numeric && !decimal.TryParse(value.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out number) && !decimal.TryParse(value.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out number)) {WF.MessageBox.Show(dialog,"Enter a valid number.");return;}
     if(!numeric && string.IsNullOrEmpty(value.Text)) {filters.Remove(column);} else filters[column]=new ColumnFilter {Operator=op.SelectedItem.ToString(),Text=value.Text,Number=number};
     dialog.DialogResult=WF.DialogResult.OK;
    };
    clear.Click+=(s,e)=>{filters.Remove(column);dialog.DialogResult=WF.DialogResult.OK;};
    dialog.Controls.AddRange(new WF.Control[]{op,value,hint,accept,clear,cancel});dialog.AcceptButton=accept;dialog.CancelButton=cancel;
    if(dialog.ShowDialog(this)==WF.DialogResult.OK)ApplyView();
   }
  }
  bool Matches(Row row,string column,ColumnFilter filter) {
   object value=typeof(Row).GetProperty(column).GetValue(row,null);
   if(value is string) {
    string text=(string)value??"",term=filter.Text;
    switch(filter.Operator) {
     case "Contains":return text.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0;
     case "Does not contain":return text.IndexOf(term,StringComparison.OrdinalIgnoreCase)<0;
     case "Equals":return string.Equals(text,term,StringComparison.OrdinalIgnoreCase);
     case "Not equal":return !string.Equals(text,term,StringComparison.OrdinalIgnoreCase);
     case "Starts with":return text.StartsWith(term,StringComparison.OrdinalIgnoreCase);
     case "Ends with":return text.EndsWith(term,StringComparison.OrdinalIgnoreCase);
    }
   }
   // Match the six decimal places displayed in length columns; IDs/counts stay exact.
   decimal number=value is double?Convert.ToDecimal(Math.Round((double)value,6)):Convert.ToDecimal(value);
   switch(filter.Operator) {case "=":return number==filter.Number;case "!=":return number!=filter.Number;case ">":return number>filter.Number;case ">=":return number>=filter.Number;case "<":return number<filter.Number;case "<=":return number<=filter.Number;}
   return false;
  }
  void ApplyView() {
   var query=rows.Where(r=>filters.All(f=>Matches(r,f.Key,f.Value)));
   var property=typeof(Row).GetProperty(sortColumn);
   visibleRows=(descending?query.OrderByDescending(r=>property.GetValue(r,null)):query.OrderBy(r=>property.GetValue(r,null))).ThenBy(r=>r.Id).ToList();
   grid.DataSource=null;grid.DataSource=visibleRows;
   foreach(WF.DataGridViewColumn c in grid.Columns) {
    c.SortMode=WF.DataGridViewColumnSortMode.Programmatic;
    c.HeaderText=c.DataPropertyName+(filters.ContainsKey(c.DataPropertyName)?" [filter]":"");
    c.HeaderCell.SortGlyphDirection=c.DataPropertyName==sortColumn?(descending?WF.SortOrder.Descending:WF.SortOrder.Ascending):WF.SortOrder.None;
    if(c.ValueType==typeof(double)) c.DefaultCellStyle.Format="0.000000";
   }
   filterInfo.Text="Showing "+visibleRows.Count+" of "+rows.Count+" rows. Click a header to sort; right-click to filter.\nFilters affect the table and CSV only. Preview, Apply and summary totals use the full scan scope.\n"+(filters.Count==0?"No column filters.":string.Join("; ",filters.Select(f=>f.Key+" "+f.Value.Operator+" "+f.Value.Text)));
  }
  void ConfigureNumber(WF.NumericUpDown n,decimal value) {n.DecimalPlaces=3;n.Minimum=0.001m;n.Maximum=100000;n.Increment=0.1m;n.Value=value;n.Width=100;}
  WF.Button Button(WF.Control parent,string text,Action action) {var b=new WF.Button {Text=text,AutoSize=true,Height=36};b.Click+=(s,e)=>{try {action();}catch(Exception ex){WF.MessageBox.Show(this,ex.Message,"Pipe Splitter");}};parent.Controls.Add(b);return b;}
  void InvalidateScan() {verified=false;preview.Enabled=apply.Enabled=export.Enabled=false;summary.Text="Settings changed. Click Scan again to update the results.";}
  void Scan() {
   double max=(double)maximum.Value,min=(double)minimum.Value;
   if(min>max || min*Engine.FeetPerMetre<=doc.Application.ShortCurveTolerance*2) throw new Exception("Minimum must be <= maximum and above Revit short-curve tolerance.");
   var selected=scope.SelectedIndex==1?ui.Selection.GetElementIds():null;
   if(selected!=null && selected.Count==0) throw new Exception("Select pipes in Revit before opening this command.");
   rows=Engine.Scan(doc,selected,max,min);verified=false;RefreshRows("ESTIMATE");preview.Enabled=rows.Any(r=>r.Eligible);apply.Enabled=false;export.Enabled=rows.Count>0;
  }
  void RefreshRows(string phase) {
   ApplyView();
   var applicable=rows.Where(r=>r.Eligible && (r.Passed || r.Status.StartsWith("Estimated"))).ToList();
   summary.Text=phase+" | Pipes scanned: "+rows.Count+" | Pipes to split: "+applicable.Count+" | Result pieces (split pipes): "+applicable.Sum(r=>r.Pieces)+" | Unions: "+applicable.Sum(r=>r.Unions)+"\n"+
    "Min / Max (split pipes): "+(applicable.Count>0?applicable.Min(r=>r.Min_m).ToString("0.000000")+" / "+applicable.Max(r=>r.Max_m).ToString("0.000000")+" m":"—")+" | Existing short: "+rows.Count(r=>r.Status.StartsWith("Existing short"))+" | Skipped / blocked: "+rows.Count(r=>r.Status.StartsWith("Skipped")||r.Status.StartsWith("Blocked")||r.Status.StartsWith("Failed"))+"\n"+
    "Total resulting pipes in scan scope: "+rows.Sum(r=>applicable.Contains(r)?r.Pieces:1)+" | Exact preview includes union takeout. Existing short pipes remain unchanged.";
  }
  void Run(bool commit) {
   if(commit && !verified) throw new Exception("Preview first");
   var targets=rows.Where(r=>r.Eligible && (!commit||r.Passed)).ToList();
   if(commit && WF.MessageBox.Show(this,"Split "+targets.Count+" pipes in the full scan scope and add unions?\nColumn filters do not limit execution.\nYou can undo the entire operation in one step.","Apply pipe splitting",WF.MessageBoxButtons.YesNo,WF.MessageBoxIcon.Question)!=WF.DialogResult.Yes) return;
   verified=false;scan.Enabled=preview.Enabled=apply.Enabled=false;UseWaitCursor=true;int ok=0,failed=0;
   try {
    using(var group=new TransactionGroup(doc,"Pipe Splitter - "+(commit?"Apply":"Preview"))) {
     group.Start();
     foreach(var r in targets) {
      r.Passed=false;string error;
      if(Engine.Process(doc,r,(double)maximum.Value,(double)minimum.Value,out error)) {r.Status=commit?"Applied":"Preview OK";ok++;}
      else {r.Status="Failed: "+error;Engine.SetLengths(r,new List<double>{r.Length_m});failed++;}
      summary.Text=(commit?"Applying":"Previewing")+" "+(ok+failed)+" / "+targets.Count;summary.Refresh();
     }
     if(commit) {if(group.Assimilate()!=TransactionStatus.Committed) throw new Exception("Group commit failed");}
     else if(group.RollBack()!=TransactionStatus.RolledBack) throw new Exception("Preview rollback failed");
    }
    verified=!commit;RefreshRows(commit?"APPLIED":"EXACT PREVIEW - trial changes rolled back");
    if(commit) {foreach(var r in rows) r.Eligible=false;WF.MessageBox.Show(this,"Applied: "+ok+"\nFailed and unchanged: "+failed+"\nScan again for updated model totals.","Pipe Splitter");}
   } catch {
    verified=false;
    foreach(var r in targets) {r.Passed=false;r.Status="Failed: batch rolled back; scan again";Engine.SetLengths(r,new List<double>{r.Length_m});}
    RefreshRows("ROLLED BACK");throw;
   } finally {UseWaitCursor=false;scan.Enabled=true;preview.Enabled=!commit && rows.Any(r=>r.Eligible);apply.Enabled=verified && rows.Any(r=>r.Passed);}
  }
  void Export() {
   using(var dialog=new WF.SaveFileDialog {Filter="CSV files|*.csv",FileName="PipeSplitter-report.csv"}) {
    if(dialog.ShowDialog(this)!=WF.DialogResult.OK)return;
    var lines=new List<string>{"ElementId,Type,OriginalLength_m,Pieces,Unions,Min_m,Max_m,Lengths_m,Status"};
    foreach(var r in visibleRows) lines.Add(string.Join(",",new object[]{r.Id,r.Type,r.Length_m,r.Pieces,r.Unions,r.Min_m,r.Max_m,r.Lengths_m,r.Status}.Select(v=>"\""+Convert.ToString(v,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"")));
    File.WriteAllLines(dialog.FileName,lines,new UTF8Encoding(true));
   }
  }
}
}
