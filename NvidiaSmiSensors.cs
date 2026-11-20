using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
namespace ThermalPilot
{
    internal sealed class NvidiaSmiSensors
    {
        public readonly string Executable;
        public NvidiaSmiSensors()
        {
            var candidates = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe") };
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                if (!string.IsNullOrWhiteSpace(directory)) { try { candidates.Add(Path.Combine(directory.Trim('"'), "nvidia-smi.exe")); } catch (ArgumentException) { } }
            Executable = candidates.FirstOrDefault(File.Exists);
        }
        public void Capture(SensorResult result)
        {
            if (Executable == null) { result.Diagnostics.Add("NVIDIA: nvidia-smi bulunamadı; bu sağlayıcı kullanılmadı."); return; }
            try
            {
                using (var process = Process.Start(new ProcessStartInfo(Executable, "--query-gpu=uuid,name,temperature.gpu --format=csv,noheader,nounits") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(2500)) { process.Kill(); process.WaitForExit(1000); result.Diagnostics.Add("NVIDIA: nvidia-smi zaman aşımı; eski değer kullanılmadı."); return; }
                    string text = output.GetAwaiter().GetResult(), detail = error.GetAwaiter().GetResult().Trim();
                    if (process.ExitCode != 0) { result.Diagnostics.Add("NVIDIA: sorgu başarısız (" + process.ExitCode + "): " + detail); return; }
                    Parse(text, result);
                }
            }
            catch (Exception e) { result.Diagnostics.Add("NVIDIA: " + e.GetType().Name + ": " + e.Message); }
        }
        internal static void Parse(string text, SensorResult result)
        {
            foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = Csv(line); double value;
                if (fields.Count != 3 || !double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !SensorRoles.Valid(value)) { result.Diagnostics.Add("NVIDIA: sıcaklık N/A, desteklenmiyor veya CSV satırı geçersiz."); continue; }
                result.Rows.Add(new SensorRow { HardwareId = fields[0], HardwareName = fields[1], SensorId = fields[0] + "/temperature.gpu", SensorName = "GPU Core", Role = "GPU Core", Name = fields[1] + " · GPU Core", Kind = "GPU", Value = value, Source = "NVIDIA nvidia-smi", SampledUtc = DateTime.UtcNow.ToString("o") });
                result.Diagnostics.Add("NVIDIA · " + fields[1] + ": temperature.gpu okundu.");
            }
        }
        private static List<string> Csv(string text)
        {
            var result = new List<string>(); var field = new System.Text.StringBuilder(); bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else quoted = !quoted; }
                else if (c == ',' && !quoted) { result.Add(field.ToString().Trim()); field.Clear(); }
                else field.Append(c);
            }
            if (quoted) return new List<string>(); result.Add(field.ToString().Trim()); return result;
        }
    }
}
