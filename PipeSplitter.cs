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



namespace PipeSplitter2024 {
 [Transaction(TransactionMode.Manual)]
 public class Command : IExternalCommand {
  public Result Execute(ExternalCommandData data, ref string message, ElementSet elements) {
   try {
    UIDocument ui = data.Application.ActiveUIDocument;
    if (ui == null || ui.Document.IsFamilyDocument || ui.Document.IsReadOnly) { TaskDialog.Show("Pipe Splitter", "Open an editable Revit project first."); return Result.Cancelled; }
    using (var form = new SplitForm(ui)) {
     var owner = new WindowOwner(data.Application.MainWindowHandle);
     while (true)
     {
      form.DialogResult = WF.DialogResult.None;
      form.ShowDialog(owner);
      if (!form.PickRequested) break;
      // Pick only after ShowDialog returns and releases its modal owner lock.
      form.CompletePickRequest();
     }
     if(form.SelectionToApply!=null) {
      var ids=form.SelectionToApply.Where(id=>ui.Document.GetElement(id)!=null).ToList();ui.Selection.SetElementIds(ids);
      if(ids.Count>0) try {ui.ShowElements(ids);}catch(Exception ex){DiagnosticLog.Error("Selection succeeded but zoom failed",ex);TaskDialog.Show("Pipe Splitter","Elements selected, but zoom failed: "+ex.Message);}
     }
    }
    return Result.Succeeded;
   } catch (Exception ex) { DiagnosticLog.Error("Command",ex); message = ex.ToString(); return Result.Failed; }
  }
 }
 class WindowOwner : WF.IWin32Window { public IntPtr Handle {get; private set;} public WindowOwner(IntPtr h) {Handle=h;} }
 public class Row {
  public long Id {get;set;} public string Type {get;set;} public double Length_m {get;set;} public int Pieces {get;set;} public int Unions {get;set;}
  public double Min_m {get;set;} public double Max_m {get;set;} public string Lengths_m {get;set;} public string Status {get;set;}
  public string Log {get;set;}
  public int WarningCount { get { return Warnings.Count; } }
  public string SystemType {get;set;} public double Diameter_mm {get;set;}
  public double MaxFinished_m {get;set;} public double Minimum_m {get;set;} public string Rule {get;set;}
  internal RemainderPlacement Placement;
  internal List<long> ResultPipeIds=new List<long>(),ResultUnionIds=new List<long>();
  internal List<string> Warnings=new List<string>();
  internal List<double> ActualLengths;
  internal List<double> VerifiedCutPlan;
  internal string WithWarnings(string status) {return Warnings.Count==0?status:status+" ("+Warnings.Count+" warnings)";}
  internal void AddLog(string text) {Log=(string.IsNullOrEmpty(Log)?"":Log+Environment.NewLine)+text;DiagnosticLog.Write("Pipe "+Id,text);}
  internal XYZ Start, End; internal List<double> Plan; internal bool Eligible, Passed; internal double PlanMinimum;
 }
 class StrictFailures : IFailuresPreprocessor {
  public string Detail = "";
  readonly Row row; readonly bool strict;
  internal StrictFailures(Row r,bool strictMode) {row=r;strict=strictMode;}
  public FailureProcessingResult PreprocessFailures(FailuresAccessor a) {
   try {
   bool rollback=false;
   foreach(var failure in a.GetFailureMessages()) {
    FailureSeverity severity=failure.GetSeverity();
    string description=severity+": "+failure.GetDescriptionText();
    row.AddLog(description);Detail+=(Detail.Length==0?"":"; ")+description;
    if(severity==FailureSeverity.Warning) {
     row.Warnings.Add(failure.GetDescriptionText());
     if(strict) rollback=true;
     else a.DeleteWarning(failure); // Logged before dismissal, so non-strict batches avoid warning dialogs.
    } else if(severity!=FailureSeverity.None) rollback=true;
   }
   if(rollback) return FailureProcessingResult.ProceedWithRollBack;
   return FailureProcessingResult.Continue;
   } catch(Exception ex) {Detail="Failure handling could not complete: "+ex.Message;row.AddLog(ex.ToString());return FailureProcessingResult.ProceedWithRollBack;}
  }
 }
 public static class Engine {
  internal const double FeetPerMetre = 1.0 / 0.3048;
  internal static double Metres(double f) {return f * 0.3048;}
  internal static List<Connector> Ends(Pipe p) {return p.ConnectorManager.Connectors.Cast<Connector>().Where(c=>c.ConnectorType==ConnectorType.End).ToList();}
  internal static Line Axis(Pipe p) {var location=p==null?null:p.Location as LocationCurve;return location==null?null:location.Curve as Line;}
  internal static void SetLengths(Row r, List<double> lengths) {r.ActualLengths=new List<double>(lengths);r.Pieces=lengths.Count;r.Unions=lengths.Count-1;r.Min_m=lengths.Min();r.Max_m=lengths.Max();r.Lengths_m=string.Join(" + ",lengths.Select(v=>v.ToString("0.000",CultureInfo.InvariantCulture)));}
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
  internal static List<Row> Scan(Document d, ICollection<ElementId> selected, double defaultMax, double defaultMin, RuleFile rules, RemainderPlacement placement) {
   var tolerance=new ModelTolerance(d);
   var routing=new Dictionary<string,string>();
   var pipes=new FilteredElementCollector(d).OfClass(typeof(Pipe)).Cast<Pipe>();
   if(selected!=null) {var selectedIds=new HashSet<ElementId>(selected);pipes=pipes.Where(p=>selectedIds.Contains(p.Id));}
   var rows=new List<Row>();
   foreach(var p in pipes.OrderBy(p=>p.Id.Value)) {
    var type=d.GetElement(p.GetTypeId());
    var r=new Row {Id=p.Id.Value,Type=type==null?"Unknown pipe type":type.Name,Log=""};
    rows.Add(r);
    try {
    Parameter systemParameter=p.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
    Element system=systemParameter==null?null:d.GetElement(systemParameter.AsElementId());
    r.SystemType=system==null?"":system.Name;r.Diameter_mm=p.Diameter*304.8;r.Placement=placement;
    PipeRule rule=rules.Match(r.Type,r.SystemType,r.Diameter_mm);
    double max=rule==null?defaultMax:rule.MaximumM;
    double min=rule!=null && rule.MinimumM.HasValue?rule.MinimumM.Value:defaultMin;
    r.MaxFinished_m=max;r.Minimum_m=min;r.Rule=rule==null?"Global default":rule.Name;
    var location=p.Location as LocationCurve;
    var curve=location==null?null:location.Curve;
    if(curve==null || !curve.IsBound) {r.Status="Skipped: missing or unbounded pipe curve";r.Pieces=1;continue;}
    r.Length_m=Metres(curve.Length);r.Start=curve.GetEndPoint(0);r.End=curve.GetEndPoint(1);
    SetLengths(r,new List<double>{r.Length_m});
    string skip=Exclusion(d,p);
    if(skip!=null) r.Status=skip;
    else if(min>max || min*FeetPerMetre<=tolerance.ShortFeet*2) r.Status="Blocked: rule limits are incompatible or below Revit short-curve tolerance";
    else if(r.Length_m<min-tolerance.LengthMetres) r.Status="Existing short pipe: unchanged";
    else if(r.Length_m<=max+tolerance.LengthMetres) r.Status="Within limits: unchanged";
    else {
     string key=UnionRouting.Key(p),problem;
     if(!routing.TryGetValue(key,out problem)) {
      try {UnionRouting.Resolve(d,p);problem="";}catch(Exception ex){problem=ex.Message;DiagnosticLog.Error("Routing precheck "+key,ex);}
      routing[key]=problem;
     }
     if(problem.Length>0) {r.Status="Blocked: "+problem;r.AddLog(problem);continue;}
     r.Plan=Planner.Split(r.Length_m,max,min,placement);SetLengths(r,r.Plan);r.Eligible=true;r.PlanMinimum=min;r.Status="Estimated: needs preview";
    }
    } catch(Exception ex) {r.Status="Blocked: "+ex.Message;r.AddLog(ex.ToString());}
   }
   return rows;
  }
  static List<string> ExternalRefs(Connector c, HashSet<long> own) {return c.AllRefs.Cast<Connector>().Where(x=>x.ConnectorType==ConnectorType.End && !own.Contains(x.Owner.Id.Value)).Select(x=>x.Owner.Id.Value+":"+x.Id).OrderBy(x=>x).ToList();}
  sealed class InsulationSpec {
   public ElementId TypeId;
   public double Thickness;
   public InsulationPlacement Placement;
  }
  static InsulationSpec ReadInsulation(Document d, ElementId pipeId) {
   var insulationIds=InsulationLiningBase.GetInsulationIds(d,pipeId);
   if(insulationIds.Count==0) return null;
   if(insulationIds.Count!=1) throw new Exception("Insulation: unexpected number of layers; pipe unchanged");
   var insulation=d.GetElement(insulationIds.First()) as PipeInsulation;
   if(insulation==null) throw new Exception("Insulation: cannot read original pipe insulation");
   return new InsulationSpec {TypeId=insulation.GetTypeId(),Thickness=insulation.Thickness,Placement=InsulationPlacement.Capture(insulation)};
  }
  static void PreserveInsulation(Document d, IList<ElementId> pipeIds, InsulationSpec original, ModelTolerance tolerance) {
   foreach(var pipeId in pipeIds) {
    var current=ReadInsulation(d,pipeId);
    // BreakCurve can propagate insulation. Keep it when present and only create missing hosts.
    if(original!=null && current==null) {
     var created = PipeInsulation.Create(d, pipeId, original.TypeId, original.Thickness);
     original.Placement.MatchPlacement(created);
    } else if(original==null && current!=null) {
     throw new Exception("Insulation: unexpected insulation appeared on an uninsulated pipe");
    } else if(original!=null && (current.TypeId!=original.TypeId || Math.Abs(current.Thickness-original.Thickness)>tolerance.LengthFeet)) {
     throw new Exception("Insulation: inherited type or thickness differs from the original");
    }
   }
   d.Regenerate();
   foreach(var pipeId in pipeIds) {
    var actual=ReadInsulation(d,pipeId);
    if(original!=null && (actual==null || actual.TypeId!=original.TypeId || Math.Abs(actual.Thickness-original.Thickness)>tolerance.LengthFeet))
     throw new Exception("Insulation: final verification failed for pipe "+pipeId.Value);
    if(original==null && actual!=null) throw new Exception("Insulation: final verification failed for uninsulated pipe "+pipeId.Value);
    foreach(var insulationId in InsulationLiningBase.GetInsulationIds(d,pipeId)) original.Placement.VerifyPlacement(d.GetElement(insulationId));
   }
  }
  // Each pipe is atomic. Failed union placement or failed validation rolls back all of its cuts.
  internal static bool Process(Document d, Row r, double max, double min, bool strict, Dictionary<string,TakeoutMeasurement> cache, bool useVerifiedPlan, out string error) {
   error="";
   r.ResultPipeIds.Clear();r.ResultUnionIds.Clear();r.Warnings.Clear();
   try {
    var initial=d.GetElement(new ElementId(r.Id)) as Pipe;
    string exclusion=initial==null?"Pipe no longer exists":Exclusion(d,initial);
    if(exclusion!=null) {error=exclusion;r.AddLog(exclusion);return false;}
   }catch(Exception ex){error=ex.Message;r.AddLog(ex.ToString());return false;}
   var tolerance=new ModelTolerance(d);
   bool verifiedSeeded=useVerifiedPlan && r.VerifiedCutPlan!=null;
   var plans=verifiedSeeded?new List<double>(r.VerifiedCutPlan):Planner.Split(r.Length_m,max,min,r.Placement);
   if(verifiedSeeded) r.AddLog("Apply first attempt reuses the verified Preview cut plan.");
   double finishedTolerance=tolerance.LengthMetres;
   bool cacheSeeded=false;
   bool seedFallback=!verifiedSeeded;
   string cacheKey="";
   for(int attempt=0;attempt<8;attempt++) {
    using(var tx=new Autodesk.Revit.DB.Transaction(d,"Split pipe "+r.Id)) {
     var handler=new StrictFailures(r,strict);
     try {
      var original=d.GetElement(new ElementId(r.Id)) as Pipe;
      if(original==null) throw new Exception("Pipe no longer exists");
      var axis=Axis(original);
      if(axis.GetEndPoint(0).DistanceTo(r.Start)>tolerance.PointFeet || axis.GetEndPoint(1).DistanceTo(r.End)>tolerance.PointFeet) throw new Exception("Geometry changed; scan again");
      ElementId routingPart=UnionRouting.Resolve(d,original);
      var metadata=PipeMetadata.Capture(original);
      cacheKey=UnionRouting.Key(original);
      if(seedFallback) {
       seedFallback=false;
       TakeoutMeasurement measured;
       if(cache.TryGetValue(cacheKey,out measured) && measured.RoutingPart==routingPart.Value) {
        try {plans=measured.Seed(r.Length_m,plans.Count,max,min,r.Placement);cacheSeeded=true;r.AddLog("Starting from cached union takeout; final verification remains required.");}
        catch(ArgumentException ex) {r.AddLog("Cached takeout cannot satisfy these limits; using iterative fallback. "+ex.Message);cache.Remove(cacheKey);}
       }
      }
      if(plans.Any(length=>length*FeetPerMetre<=tolerance.ShortFeet)) throw new Exception("Cut plan contains a Revit short curve");
      var insulation = ReadInsulation(d, original.Id);
      if (insulation != null && !insulation.Placement.Matches(original))
          r.AddLog("Original insulation differs from host placement; preserving insulation baseline: " + insulation.Placement.Description());
      var endpoints=Ends(original); var own=new HashSet<long>{r.Id};
      var references=endpoints.Select(c=>ExternalRefs(c,own)).ToList();var origins=endpoints.Select(c=>c.Origin).ToList();
      tx.Start();tx.SetFailureHandlingOptions(tx.GetFailureHandlingOptions().SetFailuresPreprocessor(handler).SetClearAfterRollback(true).SetForcedModalHandling(true));
      var ids=new List<ElementId>{original.Id}; var joins=new List<XYZ>();var unionIds=new List<ElementId>();double distance=0;
      for(int i=0;i<plans.Count-1;i++) {
       distance+=plans[i]*FeetPerMetre; XYZ point=r.Start+(r.End-r.Start).Normalize()*distance;
       Pipe container=ids.Select(id=>d.GetElement(id) as Pipe).FirstOrDefault(p=> {var l=Axis(p);return l!=null && l.Distance(point)<=tolerance.PointFeet && l.GetEndPoint(0).DistanceTo(point)>tolerance.ShortFeet && l.GetEndPoint(1).DistanceTo(point)>tolerance.ShortFeet;});
       if(container==null) throw new Exception("Cut point is outside the remaining pipe");
       ElementId added=PlumbingUtils.BreakCurve(d,container.Id,point);
       if(added==ElementId.InvalidElementId) throw new Exception("Revit rejected pipe break");
       ids.Add(added);joins.Add(point);d.Regenerate();
       metadata.MatchPlacement(d.GetElement(added));
      }
      // Resolve connectors by cut point after all breaks; a stored segment id can change ownership of a later joint.
      foreach(var join in joins) {
       var pair=ids.SelectMany(id=>Ends((Pipe)d.GetElement(id))).Where(c=>c.Origin.DistanceTo(join)<=tolerance.PointFeet).ToList();
       if(pair.Count!=2) throw new Exception("Cannot locate both cut connectors");
       if(pair[0].IsConnectedTo(pair[1])) pair[0].DisconnectFrom(pair[1]);
       var union=d.Create.NewUnionFitting(pair[0],pair[1]);
       if(union==null) throw new Exception("Routing preferences did not create a union");
       unionIds.Add(union.Id);metadata.MatchPlacement(union);
       d.Regenerate();
       if(!pair.All(c=>c.AllRefs.Cast<Connector>().Any(x=>x.Owner.Id==union.Id))) throw new Exception("Union connection failed");
      }
      d.Regenerate();
      var pieces=ids.Select(id=>(Pipe)d.GetElement(id)).OrderBy(p=>Axis(p).Evaluate(0.5,true).DistanceTo(r.Start)).ToList();
      var actual=pieces.Select(p=>Metres(Axis(p).Length)).ToList();
      own=new HashSet<long>(ids.Select(id=>id.Value));
      for(int k=0;k<origins.Count;k++) {
       var end=pieces.SelectMany(p=>Ends(p)).FirstOrDefault(c=>c.Origin.DistanceTo(origins[k])<=tolerance.PointFeet);
       if(end==null || !ExternalRefs(end,own).SequenceEqual(references[k])) throw new Exception("Original end connection or position changed");
      }
      // Union takeout is measured from the actual fitted geometry, never guessed from family names.
      // Greedily retain full finished lengths, redistributing only the remainder to satisfy the minimum.
      var targets=Planner.FinishedTargets(actual.Sum(),actual.Count,max,min,r.Placement);
      bool finished=actual.Select((length,i)=>Math.Abs(length-targets[i])).All(delta=>delta<=finishedTolerance);
      if(!finished) {
       var corrected=Planner.CompensateCuts(plans,actual,targets);
       if(Math.Abs(corrected.Sum()-r.Length_m)>finishedTolerance) throw new Exception("Compensation would change the original route length");
       if(cacheSeeded) {r.AddLog("Cached estimate did not meet finished targets; iterative compensation used.");cache.Remove(cacheKey);cacheSeeded=false;}
       tx.RollBack();
       if(attempt==7) throw new Exception("Could not achieve finished pipe lengths after unions within 8 attempts; pipe unchanged");
       if(verifiedSeeded) {verifiedSeeded=false;seedFallback=true;plans=Planner.Split(r.Length_m,max,min,r.Placement);r.AddLog("Verified plan no longer meets finished targets; retrying cache/iterative path.");continue;}
       plans=corrected;continue;
      }
      if(actual.Min()<min-finishedTolerance || actual.Max()>max+finishedTolerance) throw new Exception("Finished pipe lengths are outside the specified limits");
      PreserveInsulation(d,ids,insulation,tolerance);
      foreach(var piece in pieces) metadata.VerifyPipe(piece,tolerance);
      foreach(var unionId in unionIds) {
       var fitting=(FamilyInstance)d.GetElement(unionId);metadata.VerifyPlacement(fitting);
       var unionEnds=fitting.MEPModel.ConnectorManager.Connectors.Cast<Connector>().Where(c=>c.ConnectorType==ConnectorType.End).ToList();
       if(unionEnds.Count!=2 || unionEnds.Any(c=>!c.AllRefs.Cast<Connector>().Any(x=>x.ConnectorType==ConnectorType.End && own.Contains(x.Owner.Id.Value) && c.IsConnectedTo(x)))) throw new Exception("Final union connection verification failed");
      }
      var finalLengths=pieces.Select(p=>Metres(Axis(p).Length)).ToList();
      if(finalLengths.Select((value,index)=>Math.Abs(value-targets[index])).Any(delta=>delta>finishedTolerance)) throw new Exception("Finished lengths changed during final regeneration");
      actual=finalLengths;
      for(int k=0;k<origins.Count;k++) {
       var end=pieces.SelectMany(p=>Ends(p)).FirstOrDefault(c=>c.Origin.DistanceTo(origins[k])<=tolerance.PointFeet);
       if(end==null || !ExternalRefs(end,own).SequenceEqual(references[k])) throw new Exception("Original end connection changed during final regeneration");
      }
      var measurement=TakeoutMeasurement.Measure(plans,actual,routingPart.Value,finishedTolerance);
      var status=tx.Commit();
      if(status!=TransactionStatus.Committed) throw new Exception("Revit rolled back: "+handler.Detail);
      if(measurement!=null) {cache[cacheKey]=measurement;r.AddLog("Measured takeout cached for pipe type/size; attempts: "+(attempt+1));}
      else {cache.Remove(cacheKey);r.AddLog("Takeout varies between joins; measurement not cached.");}
      r.ResultPipeIds=pieces.Select(p=>p.Id.Value).ToList();r.ResultUnionIds=unionIds.Select(id=>id.Value).ToList();
      r.Plan=new List<double>(plans);SetLengths(r,actual);r.Passed=true;return true;
     } catch(Exception ex) {
      if(tx.GetStatus()==TransactionStatus.Started) tx.RollBack();
      r.AddLog(ex.ToString());
      if(verifiedSeeded && !(ex is Autodesk.Revit.Exceptions.RegenerationFailedException) && handler.Detail.Length==0) {
       verifiedSeeded=false;seedFallback=true;plans=Planner.Split(r.Length_m,max,min,r.Placement);r.AddLog("Verified-plan attempt rolled back; retrying cache/iterative path.");continue;
      }
      if(cacheSeeded && !(ex is Autodesk.Revit.Exceptions.RegenerationFailedException) && handler.Detail.Length==0) {
       cache.Remove(cacheKey);cacheSeeded=false;plans=Planner.Split(r.Length_m,max,min,r.Placement);r.AddLog("Cached trial rolled back; retrying without cache.");continue;
      }
      error=ex.Message;return false;
     }
    }
   }
   error="Unable to satisfy length limits";return false;
  }
 }
 public partial class SplitForm : WF.Form {
  readonly UIDocument ui; readonly Document doc;
  WF.NumericUpDown maximum=new WF.NumericUpDown(), minimum=new WF.NumericUpDown();
  WF.ComboBox scope=new WF.ComboBox(); WF.DataGridView grid=new WF.DataGridView();
  WF.CheckBox strictMode=new WF.CheckBox {Text="Strict mode (roll back on warnings)",AutoSize=true,Checked=false};
  readonly Dictionary<string,TakeoutMeasurement> takeoutCache=new Dictionary<string,TakeoutMeasurement>();
  WF.Button scan, preview, apply, export;
  List<Row> rows=new List<Row>();
  List<Row> visibleRows=new List<Row>();
  readonly Dictionary<string,ColumnFilter> filters=new Dictionary<string,ColumnFilter>();
  string sortColumn="Id"; bool descending=false;
  WF.Label filterInfo=new WF.Label();
  sealed class ColumnFilter {public string Operator; public string Text; public decimal Number;}
  bool Matches(Row row,string column,ColumnFilter filter) {
   object value=RowColumns.Value(row,column);
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
   // Match the three decimal places displayed in length columns; IDs/counts stay exact.
   decimal number=value is double?Convert.ToDecimal(Math.Round((double)value,3)):Convert.ToDecimal(value);
   switch(filter.Operator) {case "=":return number==filter.Number;case "!=":return number!=filter.Number;case ">":return number>filter.Number;case ">=":return number>=filter.Number;case "<":return number<filter.Number;case "<=":return number<=filter.Number;}
   return false;
  }
  void Scan() {
   if (running || !ValidateInputs()) return;
   double max=(double)maximum.Value,min=(double)minimum.Value;
   var tolerances=new ModelTolerance(doc);
   DiagnosticLog.Write("Scan", "Document="+doc.Title+"; vertex tolerance (ft)="+tolerances.PointFeet.ToString("R",CultureInfo.InvariantCulture)+"; short curve (ft)="+tolerances.ShortFeet.ToString("R",CultureInfo.InvariantCulture)+"; finished tolerance (m)="+tolerances.LengthMetres.ToString("R",CultureInfo.InvariantCulture));
   if(min>max || min*Engine.FeetPerMetre<=doc.Application.ShortCurveTolerance*2) throw new Exception("Min piece length must not exceed Max finished length and must exceed the Revit short-curve safety limit.");
   var selected = scope.SelectedIndex == 2 ? GetActiveViewPipeIds()
       : (scope.SelectedIndex == 1 ? (pickedIds ?? ui.Selection.GetElementIds()) : null);
   if(scope.SelectedIndex == 1 && selected.Count==0) throw new Exception("Use Pick pipes in Revit or preselect pipes before opening this command.");
   InvalidateScan();LoadRulesSnapshot();takeoutCache.Clear();rows=Engine.Scan(doc,selected,max,min,rules,CurrentPlacement());scanValid=true;RefreshRows("ESTIMATE");export.Enabled=rows.Count>0;SaveSettings();
  }
  void Export() {
   using(var dialog=new WF.SaveFileDialog {Filter="CSV files|*.csv",FileName="PipeSplitter-report.csv"}) {
    if(dialog.ShowDialog(this)!=WF.DialogResult.OK)return;
    var lines=new List<string>{"ElementId,Type,OriginalLength_m,Pieces,Unions,Min_m,Max_m,Lengths_m,Status,Log"};
    foreach(var r in visibleRows) lines.Add(string.Join(",",new object[]{r.Id,r.Type,r.Length_m,r.Pieces,r.Unions,r.Min_m,r.Max_m,r.ActualLengths==null?"":string.Join(" + ",r.ActualLengths.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))),r.Status,r.Log}.Select(v=>"\""+Convert.ToString(v,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"")));
    File.WriteAllLines(dialog.FileName,lines,new UTF8Encoding(true));
   }
  }
}
}
