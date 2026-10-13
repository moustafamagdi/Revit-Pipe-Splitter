using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;

namespace PipeSplitter2024
{
    internal sealed class PipePickFilter : ISelectionFilter
    {
        internal static Pipe ResolvePipe(Element element)
        {
            Pipe pipe = element as Pipe;
            if (pipe != null) return pipe;
            PipeInsulation insulation = element as PipeInsulation;
            if (insulation == null) return null;
            return insulation.Document.GetElement(insulation.HostElementId) as Pipe;
        }

        public bool AllowElement(Element element)
        {
            return ResolvePipe(element) != null;
        }
        public bool AllowReference(Reference reference, XYZ point) { return false; }
    }
    internal sealed class AuditRecord
    {
        internal string Batch, Operation, Scope, Outcome, Time, Document;
        internal string ToolVersion,RevitVersion,WindowsUser,DocumentPath,SessionId;
        internal long OriginalId;
        internal string Type, System, Rule, Placement, Status, Warnings, Log;
        internal double Diameter, Maximum, Minimum, Before;
        internal List<double> After;
        internal List<long> ResultPipes, CreatedPipes, Unions;
        internal static AuditRecord Capture(Row row, string batch, string operation, string scope, string document, bool success, SessionInfo session)
        {
            return new AuditRecord {
                ToolVersion=SessionInfo.ToolVersion,RevitVersion=session.RevitVersion,WindowsUser=session.WindowsUser,DocumentPath=session.DocumentPath,SessionId=session.Id,
                Batch=batch, Operation=operation, Scope=scope, Outcome=success?"Pending group result":"Failed - unchanged",
                Time=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture), Document=document, OriginalId=row.Id,
                Type=row.Type, System=row.SystemType, Rule=row.Rule, Placement=row.Placement.ToString(), Status=row.Status,
                Diameter=row.Diameter_mm, Maximum=row.MaxFinished_m, Minimum=row.Minimum_m, Before=row.Length_m,
                After=row.ActualLengths==null?new List<double>():new List<double>(row.ActualLengths),
                ResultPipes=new List<long>(row.ResultPipeIds), CreatedPipes=row.ResultPipeIds.Where(id=>id!=row.Id).ToList(),
                Unions=new List<long>(row.ResultUnionIds), Warnings=string.Join(" | ",row.Warnings), Log=row.Log
            };
        }
        internal void FinalizeResult(bool committed, bool preview)
        {
            if (Outcome=="Pending group result") Outcome=committed?"Committed":preview?"Preview - rolled back (lengths simulated)":"Batch rolled back";
            if (!committed)
            {
                ResultPipes.Clear();CreatedPipes.Clear();Unions.Clear();
                if (!preview) After=new List<double>{Before};
            }
        }
    }
    internal static class AuditCsv
    {
        internal static string Cell(object value)
        {
            double? number=value as double?;
            string text=number.HasValue?number.Value.ToString("R",CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture)??"";
            // Text fields from model names/logs must not become spreadsheet formulas.
            if (value is string && text.Length>0 && ("=+-@\t\r\n".IndexOf(text[0])>=0)) text="'"+text;
            return "\""+text.Replace("\"","\"\"")+"\"";
        }
        internal const string Header="SessionId,ToolVersion,RevitVersion,WindowsUser,DocumentPath,BatchId,UTC,Document,Operation,ExecutionScope,Outcome,OriginalElementId,ResultPipeIds,CreatedPipeIds,UnionIds,PipeType,SystemType,Diameter_mm,Rule,MaxFinished_m,Minimum_m,RemainderPlacement,BeforeLength_m,AfterLengths_m,Status,Warnings,Log";
        internal static IEnumerable<string> Lines(IEnumerable<AuditRecord> records)
        {
            foreach(var record in records)
                yield return string.Join(",",new object[]{record.SessionId,record.ToolVersion,record.RevitVersion,record.WindowsUser,record.DocumentPath,record.Batch,record.Time,record.Document,record.Operation,record.Scope,record.Outcome,record.OriginalId,string.Join(";",record.ResultPipes),string.Join(";",record.CreatedPipes),string.Join(";",record.Unions),record.Type,record.System,record.Diameter,record.Rule,record.Maximum,record.Minimum,record.Placement,record.Before,string.Join(";",record.After.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))),record.Status,record.Warnings,record.Log}.Select(Cell));
        }
        internal static void Write(string path,IEnumerable<AuditRecord> records)
        {
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(true)))
                foreach(string line in new[]{Header}.Concat(Lines(records))) writer.WriteLine(line);
        }
    }
}
