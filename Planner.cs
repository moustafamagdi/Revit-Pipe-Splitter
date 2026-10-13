using System;
using System.Collections.Generic;
namespace PipeSplitter2024 {
 public static class Planner {
  // Preserve the number of pieces already split while allocating the measured net pipe length.
  public static List<double> FinishedTargets(double netLength, int count, double maximum, double minimum) {
   if(count<1 || double.IsNaN(netLength) || double.IsInfinity(netLength) || netLength<count*minimum-1e-8 || netLength>count*maximum+1e-8)
    throw new ArgumentException("Union allowance leaves insufficient net pipe length for these limits");
   var targets=new List<double>();double remaining=netLength;
   for(int i=0;i<count;i++) {
    double target=Math.Min(maximum,remaining-(count-i-1)*minimum);
    targets.Add(target);remaining-=target;
   }
   return targets;
  }
  public static List<double> CompensateCuts(List<double> cuts, List<double> actual, List<double> targets) {
   if(cuts.Count!=actual.Count || cuts.Count!=targets.Count) throw new ArgumentException("Piece count mismatch");
   var result=new List<double>();
   for(int i=0;i<cuts.Count;i++) {
    double length=cuts[i]+targets[i]-actual[i];
    if(double.IsNaN(length) || double.IsInfinity(length) || length<=0) throw new ArgumentException("Cannot compensate union allowance without invalid cut positions");
    result.Add(length);
   }
   return result;
  }
  public static List<double> Split(double length, double maximum, double minimum) {
   if (double.IsNaN(length) || double.IsInfinity(length) || double.IsNaN(maximum) || double.IsInfinity(maximum) || double.IsNaN(minimum) || double.IsInfinity(minimum) || length <= 0 || maximum <= 0 || minimum <= 0 || minimum > maximum) throw new ArgumentException("Invalid lengths");
   if (length <= maximum + 1e-9) return new List<double> { length };
   int count = (int)Math.Ceiling(length / maximum - 1e-10);
   if (count > 10000) throw new ArgumentException("More than 10,000 pieces in one pipe");
   if (length < count * minimum - 1e-9) throw new ArgumentException("Cannot satisfy both maximum and minimum lengths");
   var result = new List<double>(); double remaining = length;
   for (int i = 0; i < count; i++) { double value = Math.Min(maximum, remaining - (count - i - 1) * minimum); result.Add(value); remaining -= value; }
   return result;
  }
 }
}
