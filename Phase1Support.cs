using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeSplitter2024
{
    internal static class DiagnosticLog
    {
        private static readonly object Gate=new object();
        private static string sessionHeader="";
        internal static string LastWriteError = "";
        internal static readonly string PathName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PipeSplitter", "log.txt");
        internal static void Write(string context, string detail)
        {
            lock(Gate)
            {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                if(SessionFiles.RotateLog(PathName,5L*1024*1024))
                {
                    File.AppendAllText(PathName,"Log rotated; "+sessionHeader+Environment.NewLine);
                }
                File.AppendAllText(PathName, DateTime.Now.ToString("o", CultureInfo.InvariantCulture) + " | " + context + Environment.NewLine + detail + Environment.NewLine);
                LastWriteError = "";
            }
            catch (Exception ex) { LastWriteError = "Cannot write diagnostic log: " + ex.Message; }
            }
        }
        internal static void BeginSession(SessionInfo session) {sessionHeader=session.Header();Write("Session start",sessionHeader);}
        internal static void Error(string context, Exception ex) { Write(context, ex.ToString()); }
    }

    // Revit tolerance semantics and all metadata/routing operations need manual verification (manual plan).
    internal sealed class ModelTolerance
    {
        internal readonly double PointFeet;
        internal readonly double ShortFeet;
        internal readonly double LengthFeet;
        internal double LengthMetres { get { return LengthFeet * 0.3048; } }
        internal ModelTolerance(Document doc)
        {
            PointFeet = doc.Application.VertexTolerance;
            ShortFeet = doc.Application.ShortCurveTolerance;
            // VertexTolerance is only used for point coincidence. A tighter fraction of the
            // short-curve threshold is used for scalar lengths, not for minimum curve creation.
            LengthFeet = ShortFeet / 100.0;
            if (PointFeet <= 0 || ShortFeet <= 0) throw new InvalidOperationException("Invalid Revit application tolerances");
        }
    }

    internal sealed class PipeMetadata
    {
        internal ElementId TypeId, SystemType, Level, Created, Demolished;
        internal double Diameter;
        internal int Workset;
        private static ElementId RequiredId(Element e, BuiltInParameter bip)
        {
            Parameter p = e.get_Parameter(bip);
            if (p == null || p.StorageType != StorageType.ElementId) throw new InvalidOperationException("Missing required parameter: " + bip);
            return p.AsElementId();
        }
        internal static PipeMetadata Capture(Pipe pipe)
        {
            Parameter diameter = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            if (diameter == null || diameter.StorageType != StorageType.Double) throw new InvalidOperationException("Missing pipe diameter");
            return new PipeMetadata { TypeId = pipe.GetTypeId(), SystemType = RequiredId(pipe, BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM), Level = RequiredId(pipe, BuiltInParameter.RBS_START_LEVEL_PARAM), Diameter = diameter.AsDouble(), Created = pipe.CreatedPhaseId, Demolished = pipe.DemolishedPhaseId, Workset = pipe.WorksetId.IntegerValue };
        }
        internal void MatchPlacement(Element element)
        {
            if (element.WorksetId.IntegerValue != Workset)
            {
                Parameter workset = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (workset == null || workset.IsReadOnly || !workset.Set(Workset)) throw new InvalidOperationException("Cannot match workset on " + element.Id.Value);
            }
            if (element.CreatedPhaseId != Created || element.DemolishedPhaseId != Demolished)
            {
                // Clear demolition before changing creation to avoid an invalid intermediate phase order.
                if (element.DemolishedPhaseId != ElementId.InvalidElementId) element.DemolishedPhaseId = ElementId.InvalidElementId;
                if (element.CreatedPhaseId != Created) element.CreatedPhaseId = Created;
                if (element.DemolishedPhaseId != Demolished) element.DemolishedPhaseId = Demolished;
            }
            VerifyPlacement(element);
        }
        internal void VerifyPlacement(Element element)
        {
            if (element.WorksetId.IntegerValue != Workset || element.CreatedPhaseId != Created || element.DemolishedPhaseId != Demolished)
                throw new InvalidOperationException("Workset/phase mismatch on " + element.Id.Value);
        }
        internal void VerifyPipe(Pipe pipe, ModelTolerance tolerance)
        {
            PipeMetadata current = Capture(pipe);
            VerifyPlacement(pipe);
            if (current.TypeId != TypeId || current.SystemType != SystemType || current.Level != Level || Math.Abs(current.Diameter - Diameter) > tolerance.LengthFeet)
                throw new InvalidOperationException("Pipe type, system type, reference level or diameter mismatch on " + pipe.Id.Value);
        }
    }

    internal static class UnionRouting
    {
        internal static string Key(Pipe pipe)
        {
            return pipe.GetTypeId().Value.ToString(CultureInfo.InvariantCulture) + ":" + pipe.Diameter.ToString("R", CultureInfo.InvariantCulture);
        }
        internal static ElementId Resolve(Document doc, Pipe pipe)
        {
            PipeType type = doc.GetElement(pipe.GetTypeId()) as PipeType;
            string description = (type == null ? "Unknown pipe type" : type.Name) + " " + (pipe.Diameter * 304.8).ToString("0.###", CultureInfo.InvariantCulture) + " mm";
            if (type == null) throw new InvalidOperationException("No union defined for " + description);
            using (RoutingConditions conditions = new RoutingConditions(RoutingPreferenceErrorLevel.None))
            {
                conditions.AppendCondition(new RoutingCondition(pipe.Diameter));
                ElementId id = type.RoutingPreferenceManager.GetMEPPartId(RoutingPreferenceRuleGroupType.Unions, conditions);
                if (id == ElementId.InvalidElementId || !(doc.GetElement(id) is FamilySymbol)) throw new InvalidOperationException("No union defined for " + description);
                return id;
            }
        }
    }

    internal static class RowColumns
    {
        internal static bool IsNumeric(string name) { return name == "WarningCount" || name == "Id" || name == "Length_m" || name == "Pieces" || name == "Unions" || name == "Min_m" || name == "Max_m" || name == "Diameter_mm" || name == "MaxFinished_m" || name == "Minimum_m"; }
        internal static object Value(Row row, string name)
        {
            switch (name)
            {
                case "WarningCount": return row.WarningCount;
                case "Id": return row.Id;
                case "Type": return row.Type ?? "";
                case "Length_m": return row.Length_m;
                case "Pieces": return row.Pieces;
                case "Unions": return row.Unions;
                case "Min_m": return row.Min_m;
                case "Max_m": return row.Max_m;
                case "Lengths_m": return row.Lengths_m ?? "";
                case "Status": return row.Status ?? "";
                case "Log": return row.Log ?? "";
                case "SystemType": return row.SystemType ?? "";
                case "Diameter_mm": return row.Diameter_mm;
                case "MaxFinished_m": return row.MaxFinished_m;
                case "Minimum_m": return row.Minimum_m;
                case "Rule": return row.Rule ?? "";
                default: throw new ArgumentException("Unknown column: " + name);
            }
        }
    }
}
