using System;
using System.Collections.Generic;
using System.Linq;
namespace ThermalPilot
{
    public sealed class SensorRow
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public string Source { get; set; }
        public string Kind { get; set; }
        public string HardwareId { get; set; }
        public string HardwareName { get; set; }
        public string SensorId { get; set; }
        public string SensorName { get; set; }
        public string Role { get; set; }
        public string SampledUtc { get; set; }
        public string ValueText { get { return Value.ToString("F1") + " °C"; } }
    }
    public sealed class SensorResult
    {
        public List<SensorRow> Rows = new List<SensorRow>();
        public List<string> Diagnostics = new List<string>();
        public DateTime Updated;
        public string CpuUnavailableReason;
        public SensorRow CpuSensor { get { return Select("CPU"); } }
        public SensorRow GpuSensor { get { return Select("GPU"); } }
        public double? Cpu { get { var s = CpuSensor; return s == null ? (double?)null : s.Value; } }
        public double? Gpu { get { var s = GpuSensor; return s == null ? (double?)null : s.Value; } }
        private SensorRow Select(string kind)
        {
            return Rows.Where(x => x.Kind == kind && SensorRoles.Valid(x.Value) && SensorRoles.Fresh(x.SampledUtc) && SensorRoles.Rank(kind, x.Role ?? SensorRoles.Classify(kind, x.SensorName ?? x.Name)) < 99)
                .OrderBy(x => SensorRoles.Rank(kind, x.Role ?? SensorRoles.Classify(kind, x.SensorName ?? x.Name)))
                .ThenByDescending(x => x.Value).ThenBy(x => x.HardwareId, StringComparer.Ordinal).FirstOrDefault();
        }
    }
    internal static class SensorRoles
    {
        public static bool Valid(double n) { return !Double.IsNaN(n) && !Double.IsInfinity(n) && n > 0 && n <= 150; }
        public static bool Fresh(string sampledUtc)
        {
            if (string.IsNullOrEmpty(sampledUtc)) return true; // Test/legacy rows; production providers always supply a timestamp.
            DateTimeOffset time;
            return DateTimeOffset.TryParse(sampledUtc, out time) && Math.Abs((DateTimeOffset.UtcNow - time).TotalSeconds) <= 10;
        }
        public static string Classify(string kind, string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (kind == "CPU")
            {
                if (n.Contains("package")) return "CPU Package";
                if (n.Contains("tdie")) return n.Contains("tctl") ? "CPU Tctl/Tdie" : "CPU Tdie";
                if (n.Contains("tctl")) return "CPU Tctl";
                if (n.Contains("core")) return "CPU Core";
                return "CPU Other";
            }
            if (kind == "GPU")
            {
                if (n.Contains("memory") || n.Contains("vram")) return "GPU Memory";
                if (n.Contains("hot") && n.Contains("spot")) return "GPU Hot Spot";
                if (n.Contains("core")) return "GPU Core";
                return "GPU Other";
            }
            return "Other";
        }
        public static int Rank(string kind, string role)
        {
            if (kind == "CPU") return role == "CPU Package" ? 0 : role == "CPU Tdie" || role == "CPU Tctl/Tdie" ? 1 : role == "CPU Tctl" ? 2 : role == "CPU Core" ? 3 : 99;
            return role == "GPU Core" ? 0 : role == "GPU Main (WDDM)" ? 1 : role == "GPU Hot Spot" ? 2 : 99;
        }
    }
}
