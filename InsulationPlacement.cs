using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace PipeSplitter2024
{
    // A separate baseline: insulation placement is not necessarily its host's placement.
    internal sealed class InsulationPlacement
    {
        private int workset;
        private ElementId created;
        private ElementId demolished;

        internal int WorksetValue { get { return workset; } }

        internal static InsulationPlacement Capture(Element element)
        {
            return new InsulationPlacement
            {
                workset = element.WorksetId.IntegerValue,
                created = element.CreatedPhaseId,
                demolished = element.DemolishedPhaseId
            };
        }

        internal bool Matches(Element element)
        {
            return element.WorksetId.IntegerValue == workset
                && element.CreatedPhaseId == created
                && element.DemolishedPhaseId == demolished;
        }

        internal string Description()
        {
            return "Workset=" + workset + "; CreatedPhase=" + created.Value
                + "; DemolishedPhase=" + demolished.Value;
        }

        internal string Differences(Element element)
        {
            var differences = new List<string>();
            if (element.WorksetId.IntegerValue != workset)
                differences.Add("Workset expected=" + workset + ", actual=" + element.WorksetId.IntegerValue);
            if (element.CreatedPhaseId != created)
                differences.Add("Phase Created expected=" + created.Value + ", actual=" + element.CreatedPhaseId.Value);
            if (element.DemolishedPhaseId != demolished)
                differences.Add("Phase Demolished expected=" + demolished.Value + ", actual=" + element.DemolishedPhaseId.Value);
            return string.Join("; ", differences);
        }

        internal void MatchPlacement(Element element, Action<string> log)
        {
            log("Insulation " + element.Id.Value + " placement after creation/split: expected ["
                + Description() + "]; actual [" + Capture(element).Description() + "]");
            if (Matches(element)) return;
            string before = Capture(element).Description();
            string field = "placement verification";
            log("Matching insulation " + element.Id.Value + " to ORIGINAL insulation. " + Differences(element));
            try
            {
                if (element.WorksetId.IntegerValue != workset)
                {
                    field = "Workset";
                    Parameter parameter = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    RequireWritable(parameter, "Workset");
                    if (!parameter.Set(workset))
                        throw new InvalidOperationException("Workset assignment was rejected");
                }
                if (element.CreatedPhaseId != created)
                {
                    // Clear demolition before changing creation, retaining the original insulation baseline.
                    if (element.DemolishedPhaseId != ElementId.InvalidElementId)
                    {
                        field = "Phase Demolished (temporary clear before changing Phase Created)";
                        RequireWritable(element.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED), "Phase Demolished");
                        element.DemolishedPhaseId = ElementId.InvalidElementId;
                    }
                    field = "Phase Created";
                    RequireWritable(element.get_Parameter(BuiltInParameter.PHASE_CREATED), "Phase Created");
                    element.CreatedPhaseId = created;
                }
                if (element.DemolishedPhaseId != demolished)
                {
                    field = "Phase Demolished";
                    RequireWritable(element.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED), "Phase Demolished");
                    element.DemolishedPhaseId = demolished;
                }
                field = "placement verification";
                VerifyPlacement(element);
                log("Insulation " + element.Id.Value + " matched original placement: " + Description());
            }
            catch (Autodesk.Revit.Exceptions.RegenerationFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Insulation " + element.Id.Value
                    + ": cannot preserve " + field + ". Expected [" + Description()
                    + "]; actual before repair [" + before + "]. " + ex.Message, ex);
            }
        }

        private static void RequireWritable(Parameter parameter, string name)
        {
            if (parameter == null)
                throw new InvalidOperationException(name + " parameter is missing");
            if (parameter.IsReadOnly)
                throw new InvalidOperationException(name + " parameter is read-only");
        }

        internal void VerifyPlacement(Element element)
        {
            if (!Matches(element))
                throw new InvalidOperationException("Insulation " + element.Id.Value
                    + ": placement differs from original insulation: " + Differences(element));
        }
    }
}
