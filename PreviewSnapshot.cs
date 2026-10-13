using System;
using System.Collections.Generic;
using System.Linq;

namespace PipeSplitter2024
{
    // Numeric-only snapshot. Requested IDs include failures so they do not look like scope widening.
    internal sealed class PreviewSnapshot
    {
        private readonly HashSet<long> requested;
        private readonly Dictionary<long,List<double>> plans;
        internal readonly string Scope;
        internal PreviewSnapshot(IEnumerable<long> requestedIds, IDictionary<long,List<double>> verifiedPlans, string scope)
        {
            requested=new HashSet<long>(requestedIds);
            plans=verifiedPlans.ToDictionary(pair=>pair.Key,pair=>new List<double>(pair.Value));Scope=scope;
            if(plans.Keys.Any(id=>!requested.Contains(id))) throw new ArgumentException("Verified row not in requested preview set");
        }
        internal int Count {get{return plans.Count;}}
        internal List<long> VerifiedIds {get{return plans.Keys.OrderBy(id=>id).ToList();}}
        internal bool IncludesNewIds(IEnumerable<long> candidates) {return candidates.Any(id=>!requested.Contains(id));}
        internal List<double> PlanFor(long id) {return new List<double>(plans[id]);}
    }
}
