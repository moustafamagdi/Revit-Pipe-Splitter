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
    private static void PlacementAndRules()
    {
        List<double> start=Planner.Split(12.05,6,.5,RemainderPlacement.Start);
        Near(start[0],.5,"Start remainder");Near(start[2],6,"Start full piece");
        List<double> balanced=Planner.Split(12.05,6,.5,RemainderPlacement.Balanced);
        Near(balanced[0],3.025,"Balanced first");Near(balanced[1],6,"Balanced middle");Near(balanced[2],3.025,"Balanced last");
        Reject(()=>Planner.Split(12,6,.5,(RemainderPlacement)99),"Unknown placement");
        Random random=new Random(271828);
        for(int i=0;i<10000;i++)
        {
            int count=2+random.Next(30);double max=1+random.NextDouble()*10,min=.01+random.NextDouble()*(max-.01);
            double net=count*(min+random.NextDouble()*(max-min));
            foreach(RemainderPlacement placement in Enum.GetValues(typeof(RemainderPlacement)))
            {
                List<double> targets=Planner.FinishedTargets(net,count,max,min,placement);
                Near(targets.Sum(),net,"Placement conservation");
                Assert(targets.All(v=>v>=min-1e-8 && v<=max+1e-8),"Placement limits");
                if(placement==RemainderPlacement.Balanced) Near(targets[0],targets[count-1],"Equal ends");
                if(placement==RemainderPlacement.Start) Assert(targets[0]<=targets[count-1]+1e-8,"Start ordering");
                if(placement==RemainderPlacement.End) Assert(targets[0]+1e-8>=targets[count-1],"End ordering");
                double left=.01,right=.025;
                List<double> cuts=Enumerable.Repeat((net+(count-1)*(left+right))/count,count).ToList();
                List<double> actual=cuts.Select((v,j)=>v-(j==0?left:j==count-1?right:left+right)).ToList();
                List<double> shifted=Planner.CompensateCuts(cuts,actual,targets);
                for(int j=0;j<count;j++) Near(shifted[j]-(j==0?left:j==count-1?right:left+right),targets[j],"Placement compensation");
            }
        }
        var ruleFile=new RuleFile();
        ruleFile.Rules.Add(new PipeRule{Name="Specific",PipeType="Steel",SystemType="Supply",DiameterMm=100,MaximumM=4});
        ruleFile.Rules.Add(new PipeRule{Name="Steel fallback",PipeType="Steel",MaximumM=6});
        ruleFile.Rules.Add(new PipeRule{Name="PVC",PipeType="PVC",MaximumM=3,MinimumM=.3});
        ruleFile.Validate();
        Assert(ruleFile.Match("steel","supply",100).Name=="Specific","Rule AND and case-insensitive matching");
        Assert(ruleFile.Match("Steel","Return",100).Name=="Steel fallback","System mismatch falls through");
        Assert(ruleFile.Match("Steel","Supply",80).Name=="Steel fallback","Size mismatch falls through");
        Assert(ruleFile.Match("Copper","Supply",100)==null,"Global default fallback");
        ruleFile.Rules[0].Enabled=false;
        Assert(ruleFile.Match("Steel","Supply",100).Name=="Steel fallback","Disabled rules");
        ruleFile.Rules.Add(new PipeRule{Name="Invalid",MaximumM=6});
        Reject(()=>ruleFile.Validate(),"Selector required");ruleFile.Rules.RemoveAt(3);
        string folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-checks-"+Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        string file=System.IO.Path.Combine(folder,"data.json");
        try
        {
            SettingsStore.Save(file,ruleFile);
            RuleFile restored=SettingsStore.Read<RuleFile>(file);restored.Validate();
            Assert(restored.Rules.Count==3 && restored.Match("PVC","",50).MinimumM==.3,"Rules JSON round trip");
            var settings=new UserSettings {MaximumM=3,MinimumM=.3,Remainder="Balanced",WindowWidth=1100,WindowHeight=900};
            SettingsStore.Save(file,settings);
            UserSettings read=SettingsStore.Read<UserSettings>(file);read.Validate();
            Assert(read.Remainder=="Balanced" && read.WindowWidth==1100,"Preferences round trip");
            settings.ActiveViewOnly = true;
            settings.SelectedOnly = false;
            SettingsStore.Save(file, settings);
            read = SettingsStore.Read<UserSettings>(file);
            Assert(read.ActiveViewOnly && !read.SelectedOnly, "Active-view scan scope persists independently of execution scope");
            System.IO.File.WriteAllText(file,"{}");
            read=SettingsStore.Read<UserSettings>(file);read.Validate();Assert(read.MaximumM==6,"Missing saved fields use safe defaults");
            Assert(!read.ActiveViewOnly, "Legacy settings do not silently enable active-view scope");
            System.IO.File.WriteAllText(file,"{\"SchemaVersion\":1,\"Rules\":[{\"Name\":\"Missing max\",\"PipeType\":\"Steel\"}]}");
            bool rejected=false;try{SettingsStore.Read<RuleFile>(file);}catch(System.Runtime.Serialization.SerializationException){rejected=true;}Assert(rejected,"Missing required rule maximum rejected");
        }
        finally {if(System.IO.File.Exists(file))System.IO.File.Delete(file);System.IO.Directory.Delete(folder);}
        Console.WriteLine("Phase 2: 30,000 placement/compensation cases and rule/settings JSON checks passed; production APPDATA untouched.");
    }
    private static void ReviewFixChecks()
    {
        var session=new SessionInfo {RevitVersion="2024 / standalone",WindowsUser="TestUser",DocumentPath="test.rvt"};
        Assert(session.Header().Contains(typeof(SessionInfo).Assembly.GetName().Version.ToString()) && session.Header().Contains("TestUser") && session.Header().Contains("test.rvt"),"Session metadata header");
        var cuts=new List<double>{6.01,6.04,1.03};var actual=new List<double>{6,6,1};
        var measurement=TakeoutMeasurement.Measure(cuts,actual,123,1e-8);
        Assert(measurement!=null,"Uniform takeout measurement");Near(measurement.FirstPieceLoss,.01,"First piece loss");Near(measurement.LastPieceLoss,.03,"Last piece loss");
        var reverseCuts=new List<double>(cuts);reverseCuts.Reverse();var reverseActual=new List<double>(actual);reverseActual.Reverse();
        var reversed=TakeoutMeasurement.Measure(reverseCuts,reverseActual,123,1e-8);
        Assert(reversed!=null,"Reversed orientation measurement");Near(reversed.FirstPieceLoss,.03,"Reversed first loss");Near(reversed.LastPieceLoss,.01,"Reversed last loss");
        var two=TakeoutMeasurement.Measure(new List<double>{6.01,2.03},new List<double>{6,2},123,1e-8);
        Assert(two!=null,"Two-piece measurement");Near(two.FirstPieceLoss,.01,"Two-piece first");Near(two.LastPieceLoss,.03,"Two-piece last");
        Assert(TakeoutMeasurement.Measure(new List<double>{6.01,6.05,1.03},actual,123,1e-8)==null,"Non-uniform middle joints rejected");
        Assert(TakeoutMeasurement.Measure(null,actual,123,1e-8)==null,"Null takeout input rejected");
        foreach(var item in new[]{measurement,reversed,two})
        foreach(RemainderPlacement placement in Enum.GetValues(typeof(RemainderPlacement)))
        foreach(int count in new[]{2,3,5})
        {
            double length=(count-1)*6.0+1;
            List<double> seed=item.Seed(length,count,6,.5,placement);
            Near(seed.Sum(),length,"Cached seed preserves route");
            var expected=Planner.FinishedTargets(length-(count-1)*(item.FirstPieceLoss+item.LastPieceLoss),count,6,.5,placement);
            for(int i=0;i<count;i++) Near(seed[i]-(i==0?item.FirstPieceLoss:i==count-1?item.LastPieceLoss:item.FirstPieceLoss+item.LastPieceLoss),expected[i],"Seed honors every placement");
        }
        Reject(()=>measurement.Seed(6,1,6,.5,RemainderPlacement.End),"No single-piece cache seed");
        var supplied=new Dictionary<long,List<double>>{{10,new List<double>{6.01,1.03}}};
        var snapshot=new PreviewSnapshot(new long[]{10,20},supplied,"Visible rows");supplied[10][0]=999;
        Near(snapshot.PlanFor(10)[0],6.01,"Preview deep copy");var returned=snapshot.PlanFor(10);returned[0]=888;
        Near(snapshot.PlanFor(10)[0],6.01,"Apply plan copy");
        Assert(snapshot.Count==1 && snapshot.VerifiedIds.Single()==10,"Only verified ids retained");
        Assert(!snapshot.IncludesNewIds(new long[0]) && !snapshot.IncludesNewIds(new long[]{10,20}),"Narrowing and previous failed rows not widening");
        Assert(snapshot.IncludesNewIds(new long[]{30}),"Widening requires new preview");
        var rules=new RuleFile();rules.Rules.Add(new PipeRule{Name="Broad",PipeType="Steel",MaximumM=6});rules.Rules.Add(new PipeRule{Name="Narrow",PipeType="steel",SystemType="Supply",DiameterMm=100,MaximumM=4});
        rules.Validate();Assert(rules.ShadowWarnings().Single().Contains("Narrow"),"Broad selector shadow detected");
        rules.Rules[0].Enabled=false;Assert(rules.ShadowWarnings().Count==0,"Disabled rule does not shadow");
        rules.Rules[0].Enabled=true;rules.Rules[0].DiameterMm=100;rules.Rules[1].DiameterMm=100.0005;
        Assert(rules.ShadowWarnings().Count==0,"Overlapping size intervals are not full shadow");
        string folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"review-checks-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(folder);
        string path=System.IO.Path.Combine(folder,"rules.json");
        try
        {
            string unknown="{\"SchemaVersion\":1,\"Rules\":[{\"Name\":\"Typo rule\",\"PipeType\":\"Steel\",\"MaximumM\":6,\"DiamterMm\":100}]}";
            System.IO.File.WriteAllText(path,unknown);bool rejected=false;
            try {SettingsStore.Read<RuleFile>(path);}catch(ArgumentException ex){rejected=ex.Message.Contains("DiamterMm") && ex.Message.Contains("Typo rule");}Assert(rejected,"Unknown rule member names field and rule");
            using(var stream=new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(unknown)))
            {
                var direct=(RuleFile)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(RuleFile)).ReadObject(stream);
                Assert(direct.Rules[0].ExtensionData!=null,"Unknown field retained in ExtensionData");Reject(()=>direct.Validate(),"ExtensionData validation rejects unknown field");
            }
            System.IO.File.WriteAllText(path,"{\"SchemaVersion\":1,\"Rules\":[],\"Ruels\":[]}");
            rejected=false;try{SettingsStore.Read<RuleFile>(path);}catch(ArgumentException ex){rejected=ex.Message.Contains("Ruels");}Assert(rejected,"Unknown root member named");
            System.IO.File.WriteAllText(path,"{\"AllEligible\":true,\"MaximumM\":6,\"MinimumM\":0.5}");
            var saved=SettingsStore.Read<UserSettings>(path);SettingsStore.Save(path,saved);
            Assert(!System.IO.File.ReadAllText(path).Contains("AllEligible"),"Legacy execution scope discarded and not persisted");
            var started=new DateTime(2026,10,13,12,0,0);var first=new AuditSessionFile(folder,started);var second=new AuditSessionFile(folder,started);
            first.Append("Header",new[]{"one"});first.Append("Header",new[]{"two"});second.Append("Header",new[]{"different-session"});
            Assert(first.PathName!=second.PathName,"Timestamp collision uses unique path");
            Assert(System.IO.File.ReadAllLines(first.PathName).SequenceEqual(new[]{"Header","one","two"}),"Session append never rewrites previous rows");
            first.Append("Header", new[]{"مواسير معزولة"});
            byte[] auditBytes = System.IO.File.ReadAllBytes(first.PathName);
            Assert(auditBytes.Take(3).SequenceEqual(new byte[]{239,187,191}), "Session audit begins with UTF-8 BOM");
            string auditText = System.Text.Encoding.UTF8.GetString(auditBytes);
            Assert(auditText.Count(c => c == '\uFEFF') == 1, "Session audit appends never add a second BOM");
            Assert(auditText.Contains("\u0645\u0648\u0627\u0633\u064a\u0631 \u0645\u0639\u0632\u0648\u0644\u0629"), "Audit contains the expected Arabic Unicode code points");
            Assert(System.IO.File.ReadAllLines(first.PathName).Last() == "مواسير معزولة", "Arabic audit text survives append and UTF-8 read");
            string log=System.IO.Path.Combine(folder,"log.txt");System.IO.File.WriteAllText(log,"old-log");
            Assert(SessionFiles.RotateLog(log,3) && System.IO.File.ReadAllText(log+".bak")=="old-log","Log rotated to backup");
            System.IO.File.WriteAllText(log,"new-log");SessionFiles.RotateLog(log,3);
            Assert(System.IO.File.ReadAllText(log+".bak")=="new-log","Only latest backup retained");
        }
        finally {foreach(string file in System.IO.Directory.GetFiles(folder))System.IO.File.Delete(file);System.IO.Directory.Delete(folder);}
        Console.WriteLine("Review fixes: takeout orientation/count/placement, immutable snapshot, strict JSON, shadowing, settings, audit append and log rotation checks passed.");
    }
    private static void Phase3Checks()
    {
        Assert(Phase3Policy.ValidateLimits(6m, .5m, .001) == "", "Valid finished limits");
        Assert(Phase3Policy.ValidateLimits(6m, 7m, .001).Contains("Max finished length"), "Minimum greater than max blocked");
        Assert(Phase3Policy.ValidateLimits(6m, .001m, .001).Length > 0, "Minimum at Revit safety limit blocked");
        Assert(Phase3Policy.ValidateLimits(0m, .5m, .001).Length > 0, "Zero max blocked");
        Assert(Phase3Policy.ValidateLimits(100001m, .5m, .001).Length > 0, "Out-of-range max blocked");
        Assert(Phase3Policy.MatchesStatus("Actionable", "Failed: retryable", true, false), "Retryable failures stay actionable");
        Assert(!Phase3Policy.MatchesStatus("Actionable", "Applied", false, false), "Applied rows not actionable");
        Assert(Phase3Policy.MatchesStatus("Will split", "Estimated: needs preview", true, false), "Estimated row will split");
        Assert(!Phase3Policy.MatchesStatus("Will split", "Preview OK", true, true), "Verified row excluded from Will split");
        Assert(Phase3Policy.MatchesStatus("Verified", "Preview OK (2 warnings)", true, true), "Verified warning row filter");
        Assert(!Phase3Policy.MatchesStatus("Verified", "Preview OK", true, false), "Stale preview text cannot count as verified");
        foreach (string state in new[]{"Skipped", "Blocked", "Failed", "Applied"})
            Assert(Phase3Policy.MatchesStatus(state, state + ": detail (2 warnings)", false, false), "Status prefix " + state);
        Assert(Phase3Policy.MatchesStatus("Unchanged", "Not processed (cancelled)", true, false), "Unprocessed cancellation is unchanged");
        Assert(Phase3Policy.MatchesStatus("Actionable", "Not processed (cancelled)", true, false), "Unprocessed cancellation is retryable");
        Assert(Phase3Policy.MatchesStatus("Unchanged", "Within limits: unchanged", false, false), "Within limits filter");
        Assert(Phase3Policy.MatchesStatus("All", null, false, false), "All tolerates missing status");
        var plans = new Dictionary<long,List<double>> { { 1, new List<double>{6.01,1.03} } };
        var snapshot = new PreviewSnapshot(new long[]{1}, plans, "Visible rows");
        var hidden = new long[]{1}.Where(id => Phase3Policy.MatchesStatus("Will split", "Preview OK", true, true));
        Assert(!snapshot.IncludesNewIds(hidden) && snapshot.Count == 1, "Quick filter hiding verified success preserves snapshot");
        Assert(snapshot.IncludesNewIds(new long[]{1,2}), "Quick filter widening scope invalidates snapshot");
        Console.WriteLine("Phase 3: live limit policy, all quick status choices and frozen-set interactions passed (no WinForms or Revit execution).");
    }
    private static int Main()
    {
        try
        {
            EdgeCases(); RandomAllocations(); SyntheticTakeout(); PlacementAndRules(); ReviewFixChecks();
            Phase3Checks();
            Console.WriteLine("PASS: " + assertions + " assertions. No Revit assemblies loaded; no Revit API behavior tested.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }
}
