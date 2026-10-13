using System;

namespace PipeSplitter2024
{
    // Revit-free UI rules shared with the console harness.
    internal static class Phase3Policy
    {
        internal static bool MatchesStatus(string filter, string status, bool eligible, bool verified)
        {
            status = status ?? "";
            switch (filter)
            {
                case "Actionable": return eligible;
                case "Will split": return eligible && !verified && !status.StartsWith("Failed", StringComparison.Ordinal);
                case "Verified": return verified;
                case "Skipped": return status.StartsWith("Skipped", StringComparison.Ordinal);
                case "Blocked": return status.StartsWith("Blocked", StringComparison.Ordinal);
                case "Failed": return status.StartsWith("Failed", StringComparison.Ordinal);
                case "Applied": return status.StartsWith("Applied", StringComparison.Ordinal);
                case "Unchanged": return status.IndexOf("unchanged", StringComparison.OrdinalIgnoreCase) >= 0
                    || status.StartsWith("Not processed", StringComparison.Ordinal)
                    || status.StartsWith("Cancelled", StringComparison.Ordinal);
                case "All": return true;
                default: return false;
            }
        }

        internal static string ValidateLimits(decimal max, decimal min, double minimumSafeMetres)
        {
            if (max < .001m || max > 100000m)
                return "Max finished length must be between 0.001 and 100000 m.";
            if (min < .001m || min > 100000m)
                return "Min piece length must be between 0.001 and 100000 m.";
            if (min > max)
                return "Min piece length must not exceed Max finished length.";
            if ((double)min <= minimumSafeMetres)
                return "Min piece length must exceed the Revit short-curve safety limit ("
                    + minimumSafeMetres.ToString("0.######", System.Globalization.CultureInfo.CurrentCulture) + " m).";
            return "";
        }
    }
}
