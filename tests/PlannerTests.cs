using System;
using System.Collections.Generic;
using System.Linq;
using PipeSplitter2024;

internal static class PlannerTests
{
    private static int assertions;
    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, message);
    }
    private static void Near(double actual, double expected, string message)
    {
        Assert(Math.Abs(actual - expected) < 1e-7, message);
    }
    private static void EdgeCases()
    {
        List<double> sample = Planner.Split(12.05, 6, .5);
        Assert(sample.Count == 3, "Requested example count");
        Near(sample[0], 6, "Requested example first");
        Near(sample[1], 5.55, "Requested example middle");
        Near(sample[2], .5, "Requested example remainder");
        Assert(Planner.Split(12, 6, .5).Count == 2, "Exact multiple");
        Assert(Planner.Split(6, 6, .5).Count == 1, "Exactly maximum");
        Assert(Planner.Split(6.00001, 6, .5).Count == 2, "Slightly above maximum");
        Near(Planner.Split(.05, 6, .5)[0], .05, "Existing short retained");
        Assert(Planner.Split(12, 6, 6).All(x => x == 6), "Minimum equals maximum feasible");
        Reject(() => Planner.Split(12.05, 6, 6), "Minimum equals maximum infeasible");
        Reject(() => Planner.Split(6.01, 6, 4), "Impossible remainder");
        Reject(() => Planner.Split(1, 0, .5), "Zero max");
        Reject(() => Planner.Split(1, 6, 0), "Zero min");
        Reject(() => Planner.Split(-1, 6, .5), "Negative length");
        Reject(() => Planner.Split(double.NaN, 6, .5), "NaN length");
        Reject(() => Planner.Split(1, double.PositiveInfinity, .5), "Infinite maximum");
        Reject(() => Planner.Split(double.MaxValue, .0001, .00001), "Count overflow");
        Reject(() => Planner.Split(60006, 6, .5), "Piece limit");
        Assert(Planner.Split(60000, 6, .5).Count == Planner.MaximumPieceCount, "Piece limit inclusive");
        Reject(() => Planner.FinishedTargets(7.9, 2, 6, 4), "Takeout makes minimum infeasible");
        Reject(() => Planner.FinishedTargets(12.1, 2, 6, .5), "Net exceeds maximum allocation");
        Reject(() => Planner.FinishedTargets(1, 0, 6, .5), "Zero count");
        Reject(() => Planner.FinishedTargets(1, 1, double.NaN, .5), "Invalid finished max");
        Reject(() => Planner.CompensateCuts(null, null, null), "Null lists");
        Reject(() => Planner.CompensateCuts(new List<double>{1}, new List<double>{1}, new List<double>{2}), "Net conservation");
        Reject(() => Planner.CompensateCuts(new List<double>{.1, 2}, new List<double>{1,1}, new List<double>{.1,1.9}), "Nonpositive compensated cut");
        Reject(() => Planner.CompensateCuts(new List<double>{1}, new List<double>{double.NaN}, new List<double>{1}), "Invalid actual length");
    }
    private static void RandomAllocations()
    {
        Random random = new Random(42);
        int feasible = 0, rejected = 0;
        for (int i = 0; i < 50000; i++)
        {
            double maximum = .05 + random.NextDouble() * 20;
            double minimum = .001 + random.NextDouble() * (maximum - .001);
            double length = .001 + random.NextDouble() * 200;
            int count = (int)Math.Ceiling(length / maximum - 1e-10);
            bool possible = length <= maximum || length >= count * minimum - 1e-9;
            try
            {
                List<double> values = Planner.Split(length, maximum, minimum);
                Assert(possible, "Accepted impossible allocation");
                Near(values.Sum(), length, "Conservation");
                Assert(values.All(v => v > 0 && v <= maximum + 1e-8), "Max bound");
                if (values.Count > 1) Assert(values.All(v => v >= minimum - 1e-8), "Min bound");
                Assert(values.Count == Math.Max(1, count), "Fewest pieces");
                Assert(values.All(v => Planner.Split(v, maximum, minimum).Count == 1), "Idempotence");
                feasible++;
            }
            catch (ArgumentException)
            {
                Assert(!possible, "Rejected feasible allocation");
                rejected++;
            }
        }
        Console.WriteLine("50,000 seeded allocations: " + feasible + " feasible; " + rejected + " correctly rejected.");
    }
    private static void SyntheticTakeout()
    {
        Random random = new Random(314159);
        for (int i = 0; i < 10000; i++)
        {
            int count = 2 + random.Next(20);
            double left = .001 + random.NextDouble() * .04;
            double right = .001 + random.NextDouble() * .04;
            double net = (count - 1) * 6.0 + .5 + random.NextDouble() * 5.4;
            List<double> targets = Planner.FinishedTargets(net, count, 6, .5);
            List<double> cuts = Enumerable.Repeat((net + (count - 1) * (left + right)) / count, count).ToList();
            List<double> actual = cuts.Select((v, j) => v - (j == 0 ? left : j == count - 1 ? right : left + right)).ToList();
            List<double> corrected = Planner.CompensateCuts(cuts, actual, targets);
            Near(corrected.Sum(), cuts.Sum(), "Compensation conserves original route");
            for (int j = 0; j < count; j++)
            {
                double fitted = corrected[j] - (j == 0 ? left : j == count - 1 ? right : left + right);
                Near(fitted, targets[j], "Synthetic finished length");
                Assert(fitted >= .5 - 1e-8 && fitted <= 6 + 1e-8, "Synthetic final bounds");
            }
        }
        Console.WriteLine("10,000 synthetic asymmetric-takeout cases passed (arithmetic only).");
    }
    private static int Main()
    {
        try
        {
            EdgeCases(); RandomAllocations(); SyntheticTakeout();
            Console.WriteLine("PASS: " + assertions + " assertions. No Revit assemblies loaded; no Revit API behavior tested.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }
}
