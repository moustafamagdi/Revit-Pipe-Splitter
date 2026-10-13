using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Linq;

namespace PipeSplitter2024
{
    [DataContract]
    public sealed class UserSettings
    {
        [DataMember] public double MaximumM { get; set; }
        [DataMember] public double MinimumM { get; set; }
        [DataMember] public string Remainder { get; set; }
        [DataMember] public bool Strict { get; set; }
        [DataMember] public bool SelectedOnly { get; set; }
        [DataMember] public bool ActiveViewOnly { get; set; }
        [DataMember] public int WindowWidth { get; set; }
        [DataMember] public int WindowHeight { get; set; }
        [DataMember] public bool Maximized { get; set; }
        public UserSettings() { Defaults(); }
        [OnDeserializing] private void BeforeRead(StreamingContext context) { Defaults(); }
        private void Defaults()
        {
            MaximumM = 6; MinimumM = .5; Remainder = "End";
            WindowWidth = 1250; WindowHeight = 930;
        }
        public void Validate()
        {
            SettingsStore.ValidateNumber(MaximumM, "MaximumM");
            SettingsStore.ValidateNumber(MinimumM, "MinimumM");
            if (MinimumM > MaximumM) throw new ArgumentException("MinimumM exceeds MaximumM");
            SettingsStore.ParsePlacement(Remainder);
            if (WindowWidth < 950 || WindowHeight < 650 || WindowWidth > 10000 || WindowHeight > 10000) throw new ArgumentException("Invalid saved window size");
        }
    }
    [DataContract]
    public sealed class PipeRule : IExtensibleDataObject
    {
        public ExtensionDataObject ExtensionData { get; set; }
        [DataMember(IsRequired = true)] public string Name { get; set; }
        [DataMember] public bool Enabled { get; set; }
        [DataMember] public string PipeType { get; set; }
        [DataMember] public string SystemType { get; set; }
        [DataMember] public double? DiameterMm { get; set; }
        [DataMember(IsRequired = true)] public double MaximumM { get; set; }
        [DataMember] public double? MinimumM { get; set; }
        public PipeRule() { Enabled = true; }
        [OnDeserializing] private void BeforeRead(StreamingContext context) { Enabled = true; }
    }
    [DataContract]
    public sealed class RuleFile : IExtensibleDataObject
    {
        public ExtensionDataObject ExtensionData { get; set; }
        [DataMember(IsRequired = true)] public int SchemaVersion { get; set; }
        [DataMember(IsRequired = true)] public List<PipeRule> Rules { get; set; }
        public RuleFile() { SchemaVersion = 1; Rules = new List<PipeRule>(); }
        public void Validate()
        {
            SettingsStore.CheckExtensionData(this);
            if (SchemaVersion != 1 || Rules == null) throw new ArgumentException("Rules file requires SchemaVersion 1 and a Rules array");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PipeRule rule in Rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Name) || !names.Add(rule.Name)) throw new ArgumentException("Rules require unique nonempty names");
                if (string.IsNullOrWhiteSpace(rule.PipeType) && string.IsNullOrWhiteSpace(rule.SystemType) && !rule.DiameterMm.HasValue) throw new ArgumentException("Rule " + rule.Name + " requires at least one selector");
                SettingsStore.ValidateNumber(rule.MaximumM, rule.Name + ": MaximumM");
                if (rule.DiameterMm.HasValue) SettingsStore.ValidateNumber(rule.DiameterMm.Value, rule.Name + ": DiameterMm");
                if (rule.MinimumM.HasValue)
                {
                    SettingsStore.ValidateNumber(rule.MinimumM.Value, rule.Name + ": MinimumM");
                    if (rule.MinimumM.Value > rule.MaximumM) throw new ArgumentException("MinimumM exceeds MaximumM in " + rule.Name);
                }
            }
        }
        public List<string> ShadowWarnings()
        {
            var warnings=new List<string>();
            for(int i=0;i<Rules.Count;i++)
            {
                PipeRule later=Rules[i];if(!later.Enabled) continue;
                for(int j=0;j<i;j++)
                {
                    PipeRule earlier=Rules[j];if(!earlier.Enabled) continue;
                    bool type=string.IsNullOrWhiteSpace(earlier.PipeType) || string.Equals(earlier.PipeType,later.PipeType,StringComparison.OrdinalIgnoreCase);
                    bool system=string.IsNullOrWhiteSpace(earlier.SystemType) || string.Equals(earlier.SystemType,later.SystemType,StringComparison.OrdinalIgnoreCase);
                    // Same-size intervals have equal width: only equal centres imply full coverage.
                    bool size=!earlier.DiameterMm.HasValue || (later.DiameterMm.HasValue && earlier.DiameterMm.Value==later.DiameterMm.Value);
                    if(type && system && size) {warnings.Add("Rule '"+later.Name+"' is shadowed by earlier rule '"+earlier.Name+"'.");break;}
                }
            }
            return warnings;
        }
        public PipeRule Match(string pipeType, string systemType, double diameterMm)
        {
            foreach (PipeRule rule in Rules)
            {
                if (!rule.Enabled) continue;
                if (!string.IsNullOrWhiteSpace(rule.PipeType) && !string.Equals(rule.PipeType, pipeType, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(rule.SystemType) && !string.Equals(rule.SystemType, systemType, StringComparison.OrdinalIgnoreCase)) continue;
                if (rule.DiameterMm.HasValue && Math.Abs(rule.DiameterMm.Value - diameterMm) > 0.001) continue;
                return rule;
            }
            return null;
        }
    }
    public static class SettingsStore
    {
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PipeSplitter");
        public static string SettingsPath { get { return Path.Combine(Folder, "settings.json"); } }
        public static string RulesPath { get { return Path.Combine(Folder, "rules.json"); } }
        public static void ValidateNumber(double value, string field)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < .001 || value > 100000) throw new ArgumentException(field + " must be between 0.001 and 100000");
        }
        public static RemainderPlacement ParsePlacement(string value)
        {
            RemainderPlacement placement;
            if (!Enum.TryParse<RemainderPlacement>(value, false, out placement) || !Enum.IsDefined(typeof(RemainderPlacement), placement)) throw new ArgumentException("Remainder must be End, Start or Balanced");
            return placement;
        }
        public static T Read<T>(string path)
        {
            byte[] data=File.ReadAllBytes(path);
            if(typeof(T)==typeof(RuleFile)) CheckRuleJson(data);
            using(var stream=new MemoryStream(data)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        private static string MemberName(XmlElement member) {return member.HasAttribute("item")?member.GetAttribute("item"):member.LocalName;}
        private static void CheckMembers(XmlElement node, string context, string[] allowed)
        {
            var known=new HashSet<string>(allowed,StringComparer.Ordinal);
            foreach(XmlElement member in node.ChildNodes.OfType<XmlElement>())
                if(!known.Contains(MemberName(member))) throw new ArgumentException("Unknown JSON member '"+MemberName(member)+"' in "+context);
        }
        private static void CheckRuleJson(byte[] data)
        {
            var xml=new XmlDocument();
            using(var reader=JsonReaderWriterFactory.CreateJsonReader(data,XmlDictionaryReaderQuotas.Max)) xml.Load(reader);
            XmlElement root=xml.DocumentElement;
            CheckMembers(root,"rules.json root",new[]{"SchemaVersion","Rules"});
            XmlElement list=root.ChildNodes.OfType<XmlElement>().FirstOrDefault(node=>MemberName(node)=="Rules");
            if(list==null) return;
            int index=0;
            foreach(XmlElement rule in list.ChildNodes.OfType<XmlElement>())
            {
                XmlElement name=rule.ChildNodes.OfType<XmlElement>().FirstOrDefault(node=>MemberName(node)=="Name");
                string context="rule "+index+(name==null?" (Name missing)":" ('"+name.InnerText+"')");
                CheckMembers(rule,context,new[]{"Name","Enabled","PipeType","SystemType","DiameterMm","MaximumM","MinimumM"});index++;
            }
        }
        internal static void CheckExtensionData(RuleFile file)
        {
            // Public ExtensionData cannot expose member names. Round-trip it through the documented
            // serializer to inspect preserved unknown members without private reflection.
            if(file.ExtensionData==null && (file.Rules==null || file.Rules.All(rule=>rule==null || rule.ExtensionData==null))) return;
            using(var stream=new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(RuleFile)).WriteObject(stream,file);CheckRuleJson(stream.ToArray());
            }
        }
        public static void Save<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static RuleFile LoadRules()
        {
            string source;return LoadRules(out source);
        }
        public static RuleFile LoadRules(out string source)
        {
            if (!File.Exists(RulesPath)) Save(RulesPath, new RuleFile());
            source=File.ReadAllText(RulesPath);
            byte[] data=System.Text.Encoding.UTF8.GetBytes(source);CheckRuleJson(data);
            RuleFile rules;
            using(var stream=new MemoryStream(data)) rules=(RuleFile)new DataContractJsonSerializer(typeof(RuleFile)).ReadObject(stream);
            if (rules == null) throw new ArgumentException("Rules file is null");
            rules.Validate();return rules;
        }
    }
}
