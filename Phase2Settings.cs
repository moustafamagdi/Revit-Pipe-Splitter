using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

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
        [DataMember] public bool AllEligible { get; set; }
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
    public sealed class PipeRule
    {
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
    public sealed class RuleFile
    {
        [DataMember(IsRequired = true)] public int SchemaVersion { get; set; }
        [DataMember(IsRequired = true)] public List<PipeRule> Rules { get; set; }
        public RuleFile() { SchemaVersion = 1; Rules = new List<PipeRule>(); }
        public void Validate()
        {
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
            using (var stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
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
            if (!File.Exists(RulesPath)) Save(RulesPath, new RuleFile());
            RuleFile rules = Read<RuleFile>(RulesPath);
            if (rules == null) throw new ArgumentException("Rules file is null");
            rules.Validate();return rules;
        }
    }
}
