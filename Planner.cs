using System;
using System.Collections.Generic;
namespace PipeSplitter2024 {
 public static class Planner {
  public const int MaximumPieceCount = 10000;
  // Pure arithmetic roundoff allowances; geometric acceptance uses Revit-derived tolerances in Engine.
  const double ArithmeticTolerance = 1e-9;
  static bool Finite(double value) {return !double.IsNaN(value) && !double.IsInfinity(value);}
  static void ValidateLimits(double maximum,double minimum) {
   if(!Finite(maximum) || !Finite(minimum) || maximum<=0 || minimum<=0 || minimum>maximum) throw new ArgumentException("Invalid length limits");
  }
  // Preserve the number of pieces already split while allocating the measured net pipe length.
  public static List<double> FinishedTargets(double netLength, int count, double maximum, double minimum) {
   ValidateLimits(maximum,minimum);
   if(count<1 || count>MaximumPieceCount || !Finite(netLength) || netLength<=0 || netLength<count*minimum-ArithmeticTolerance || netLength>count*maximum+ArithmeticTolerance)
    throw new ArgumentException("Union allowance leaves insufficient net pipe length for these limits");
   var targets=new List<double>();double remaining=netLength;
   for(int i=0;i<count;i++) {
    double target=Math.Min(maximum,remaining-(count-i-1)*minimum);
    targets.Add(target);remaining-=target;
   }
   return targets;
  }
  public static List<double> CompensateCuts(List<double> cuts, List<double> actual, List<double> targets) {
   if(cuts==null || actual==null || targets==null || cuts.Count==0 || cuts.Count>MaximumPieceCount || cuts.Count!=actual.Count || cuts.Count!=targets.Count) throw new ArgumentException("Piece count mismatch");
   double actualSum=0,targetSum=0;
   for(int i=0;i<cuts.Count;i++) {
    if(!Finite(cuts[i]) || !Finite(actual[i]) || !Finite(targets[i]) || cuts[i]<=0 || actual[i]<=0 || targets[i]<=0) throw new ArgumentException("Invalid piece length");
    actualSum+=actual[i];targetSum+=targets[i];
   }
   if(Math.Abs(actualSum-targetSum)>ArithmeticTolerance*Math.Max(1,actualSum)) throw new ArgumentException("Target allocation changes net pipe length");
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
   double required = Math.Ceiling(length / maximum - 1e-10);
   if (!Finite(required) || required > MaximumPieceCount) throw new ArgumentException("More than 10,000 pieces in one pipe");
   int count = (int)required;
   if (length < count * minimum - 1e-9) throw new ArgumentException("Cannot satisfy both maximum and minimum lengths");
   var result = new List<double>(); double remaining = length;
   for (int i = 0; i < count; i++) { double value = Math.Min(maximum, remaining - (count - i - 1) * minimum); result.Add(value); remaining -= value; }
   return result;
  }
 }
}
