using System;
using Autodesk.Revit.DB;

namespace PipeSplitter2024
{
    // A separate baseline: insulation placement is not necessarily its host's placement.
    internal sealed class InsulationPlacement
    {
        private int workset;
        private ElementId created;
        private ElementId demolished;

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

        internal void MatchPlacement(Element element)
        {
            try
            {
                if (element.WorksetId.IntegerValue != workset)
                {
                    Parameter parameter = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    RequireWritable(parameter, "Workset");
                    if (!parameter.Set(workset))
                        throw new InvalidOperationException("Workset assignment was rejected");
                }
                if (element.CreatedPhaseId != created || element.DemolishedPhaseId != demolished)
                {
                    // Clear demolition before changing creation, retaining the original insulation baseline.
                    if (element.DemolishedPhaseId != ElementId.InvalidElementId)
                    {
                        RequireWritable(element.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED), "Phase Demolished");
                        element.DemolishedPhaseId = ElementId.InvalidElementId;
                    }
                    if (element.CreatedPhaseId != created)
                    {
                        RequireWritable(element.get_Parameter(BuiltInParameter.PHASE_CREATED), "Phase Created");
                        element.CreatedPhaseId = created;
                    }
                    if (element.DemolishedPhaseId != demolished)
                    {
                        RequireWritable(element.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED), "Phase Demolished");
                        element.DemolishedPhaseId = demolished;
                    }
                }
                VerifyPlacement(element);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Insulation " + element.Id.Value
                    + ": cannot preserve original insulation placement (" + Description() + "). " + ex.Message, ex);
            }
        }

        private static void RequireWritable(Parameter parameter, string name)
        {
            if (parameter == null || parameter.IsReadOnly)
                throw new InvalidOperationException(name + " is missing or read-only");
        }

        internal void VerifyPlacement(Element element)
        {
            if (!Matches(element))
                throw new InvalidOperationException("Insulation " + element.Id.Value
                    + ": placement differs from original insulation (" + Description() + ")");
        }
    }
}
