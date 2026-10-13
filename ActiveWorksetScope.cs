using System;
using Autodesk.Revit.DB;

namespace PipeSplitter2024
{
    // Revit's creation-workset behavior and restoration across rollback need manual verification.
    internal sealed class ActiveWorksetScope : IDisposable
    {
        private readonly Document document;
        private readonly Action<string> log;
        private readonly WorksetId original;

        internal ActiveWorksetScope(Document doc, Action<string> writeLog)
        {
            document = doc;
            log = writeLog;
            if (doc.IsWorkshared)
                original = doc.GetWorksetTable().GetActiveWorksetId();
        }

        internal void UseInsulationWorkset(int value)
        {
            if (original == null)
            {
                log("Active Workset: non-workshared document; no temporary switch.");
                return;
            }
            WorksetTable table = document.GetWorksetTable();
            var targetId = new WorksetId(value);
            Workset target = table.GetWorkset(targetId);
            if (target == null)
                throw new InvalidOperationException("Original insulation Workset " + value
                    + " does not exist in this document. Cannot use it as the creation workset; pipe unchanged.");
            if (target.Kind != WorksetKind.UserWorkset)
                throw new InvalidOperationException("Original insulation Workset " + value + " ('" + target.Name
                    + "') is " + target.Kind + ", not a user workset. Cannot activate it; pipe unchanged.");

            WorksetId before = table.GetActiveWorksetId();
            log("Active Workset before split=" + before.IntegerValue + "; target ORIGINAL insulation Workset="
                + value + " ('" + target.Name + "'); saved user Workset=" + original.IntegerValue);
            if (before.IntegerValue != value)
                table.SetActiveWorksetId(targetId);
            int actual = table.GetActiveWorksetId().IntegerValue;
            if (actual != value)
                throw new InvalidOperationException("Could not activate original insulation Workset. Expected="
                    + value + ", actual=" + actual + ". Pipe unchanged.");
            log("Active Workset during pipe/insulation creation=" + actual);
        }

        internal void Restore()
        {
            if (original == null) return;
            try
            {
                WorksetTable table = document.GetWorksetTable();
                int before = table.GetActiveWorksetId().IntegerValue;
                if (before == original.IntegerValue) return;
                table.SetActiveWorksetId(original);
                int actual = table.GetActiveWorksetId().IntegerValue;
                if (actual != original.IntegerValue)
                    throw new InvalidOperationException("Expected=" + original.IntegerValue + ", actual=" + actual);
                log("Restored user Active Workset: " + before + " -> " + actual);
            }
            catch (Exception ex)
            {
                log("ERROR restoring user Active Workset " + original.IntegerValue + ": " + ex);
                throw new InvalidOperationException("Could not restore user Active Workset " + original.IntegerValue
                    + ". Stop the batch and check the active workset in Revit. " + ex.Message, ex);
            }
        }

        public void Dispose()
        {
            Restore();
        }
    }
}
