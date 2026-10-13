using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;
using WF = System.Windows.Forms;

namespace PipeSplitter2024
{
    public partial class SplitForm
    {
        internal bool PickRequested { get; private set; }

        private ICollection<ElementId> GetActiveViewPipeIds()
        {
            View view = doc.ActiveView;
            if (view == null || view.IsTemplate
                || !FilteredElementCollector.IsViewValidForElementIteration(doc, view.Id))
                throw new InvalidOperationException("Open a model view (plan, section, elevation or 3D) before selecting pipes in the active view.");
            var pipeIds = new HashSet<ElementId>();
            using (var collector = new FilteredElementCollector(doc, view.Id))
            {
                var types = new LogicalOrFilter(new ElementClassFilter(typeof(Pipe)), new ElementClassFilter(typeof(PipeInsulation)));
                foreach (Element element in collector.WherePasses(types).WhereElementIsNotElementType())
                {
                    Pipe pipe = PipePickFilter.ResolvePipe(element);
                    if (pipe != null) pipeIds.Add(pipe.Id);
                }
            }
            DiagnosticLog.Write("Active view pipes", "View=" + view.Name + "; ViewId=" + view.Id.Value + "; pipes=" + pipeIds.Count);
            return pipeIds.OrderBy(id => id.Value).ToList();
        }

        private void SelectActiveViewPipes()
        {
            if (running) return;
            ICollection<ElementId> ids = GetActiveViewPipeIds();
            ui.Selection.SetElementIds(ids);
            scope.SelectedIndex = 2;
            InvalidateScan();
            SetOperationNote(ids.Count == 0
                ? "No host pipes found in the active view. Selection cleared. Click Scan to show the empty scope."
                : "Selected " + ids.Count + " pipes in the active view. Click Scan to inspect them.");
        }

        private void PickPipes()
        {
            if (running || PickRequested) return;
            PickRequested = true;
            // Do not call PickObjects from a hidden modal form's Click handler.
            // The command completes picking after ShowDialog has fully returned.
            DialogResult = WF.DialogResult.Retry;
        }

        internal void CompletePickRequest()
        {
            if (!PickRequested) return;
            PickRequested = false;
            try
            {
                if (Modal || Visible)
                    throw new InvalidOperationException("Pipe picking requires the dialog to be fully closed first.");
                DiagnosticLog.Write("Pick pipes", "Starting outside modal dialog; pipes and pipe insulation accepted.");
                var references = ui.Selection.PickObjects(ObjectType.Element, new PipePickFilter(),
                    "Pick pipes or pipe insulation, then click Finish. Esc cancels.");
                var selected = references.Select(reference => PipePickFilter.ResolvePipe(doc.GetElement(reference.ElementId)))
                    .Where(pipe => pipe != null)
                    .Select(pipe => pipe.Id)
                    .Distinct()
                    .ToList();
                if (selected.Count == 0)
                {
                    SetOperationNote("No pipes picked. Previous selection and scan retained.");
                    return;
                }
                ui.Selection.SetElementIds(selected);
                pickedIds = selected;
                scope.SelectedIndex = 1;
                InvalidateScan();
                SetOperationNote("Picked " + selected.Count + " pipes. Scan scope: Selected pipes only. Click Scan.");
                DiagnosticLog.Write("Pick pipes", "Resolved host pipe IDs: " + string.Join(",", selected.Select(id => id.Value)));
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                SetOperationNote("Pipe picking cancelled. Previous selection and scan retained.");
                DiagnosticLog.Write("Pick pipes", "Cancelled; previous selection and scan retained.");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("Pick pipes", ex);
                SetOperationNote("Pick pipes failed: " + ex.Message + " Previous scan retained.");
            }
            // The command reopens this same form, keeping rules, filters, audit and session state.
        }
    }
}
