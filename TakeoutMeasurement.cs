using System;
using System.Collections.Generic;

namespace PipeSplitter2024
{
    // Cache only numeric measurements; never retain temporary Revit elements after preview rollback.
    internal sealed class TakeoutMeasurement
    {
        internal long RoutingPart;
        internal double FirstPieceLoss, LastPieceLoss;
        internal static TakeoutMeasurement Measure(List<double> cuts, List<double> actual, long part, double tolerance)
        {
            if (cuts==null || actual==null || cuts.Count < 2 || cuts.Count != actual.Count || double.IsNaN(tolerance) || double.IsInfinity(tolerance) || tolerance<0) return null;
            for(int i=0;i<cuts.Count;i++) if(double.IsNaN(cuts[i]) || double.IsInfinity(cuts[i]) || double.IsNaN(actual[i]) || double.IsInfinity(actual[i]) || cuts[i]<=0 || actual[i]<=0) return null;
            double first = cuts[0] - actual[0], last = cuts[cuts.Count - 1] - actual[actual.Count - 1];
            if (first < -tolerance || last < -tolerance) return null;
            first = Math.Max(0, first); last = Math.Max(0, last);
            for (int i = 1; i < cuts.Count - 1; i++)
                if (Math.Abs(cuts[i] - actual[i] - first - last) > tolerance) return null;
            return new TakeoutMeasurement { RoutingPart = part, FirstPieceLoss = first, LastPieceLoss = last };
        }
        internal List<double> Seed(double length, int count, double max, double min, RemainderPlacement placement)
        {
            if(count<2) throw new ArgumentException("Takeout cache requires at least two pieces");
            List<double> targets = Planner.FinishedTargets(length - (count - 1) * (FirstPieceLoss + LastPieceLoss), count, max, min, placement);
            for (int i = 0; i < count; i++) targets[i] += (i == 0 ? FirstPieceLoss : (i == count - 1 ? LastPieceLoss : FirstPieceLoss + LastPieceLoss));
            return targets;
        }
    }

}
