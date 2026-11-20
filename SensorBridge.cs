using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ThermalPilot;
internal static class SensorBridge
{
    // Compiled with the existing C# compiler, executed by the installed .NET 10 host.
    // No elevation, downloads, installation or network access exists in this worker.
    static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            if (args.Length == 2 && args[0] == "--extract-driver")
            {
                var metadata = Assembly.LoadFrom(Path.Combine(root, "LibreHardwareMonitor.dll"));
                string resource = metadata.GetManifestResourceNames().Single(x => x.EndsWith(".PawnIO_setup.exe", StringComparison.OrdinalIgnoreCase));
                using (var input = metadata.GetManifestResourceStream(resource))
                using (var output = File.Create(args[1])) input.CopyTo(output);
                return 0;
            }
            using (var sampler = new HardwareSampler(Path.Combine(root, "LibreHardwareMonitorLib.dll")))
            {
                if (args.Length == 1 && args[0] == "--once") { Write(1, sampler.Capture()); return 0; }
                string command;
                while ((command = Console.ReadLine()) != null && command != "quit")
                {
                    int sequence;
                    if (!int.TryParse(command, out sequence)) return 2;
                    Write(sequence, sampler.Capture());
                }
            }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(HardwareSampler.Detail(e)); return 1; }
    }
    static string Quote(string value)
    {
        var b = new StringBuilder("\"");
        foreach (char c in value ?? "")
        {
            if (c == '\\' || c == '"') b.Append('\\').Append(c);
            else if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4"));
            else b.Append(c);
        }
        return b.Append('"').ToString();
    }
    static void Write(int sequence, SensorResult result)
    {
        var b = new StringBuilder("TP_SENSOR1:{\"Sequence\":").Append(sequence).Append(",\"SampledUtc\":").Append(Quote(DateTime.UtcNow.ToString("o"))).Append(",\"Rows\":[");
        bool first = true;
        foreach (var s in result.Rows)
        {
            if (!first) b.Append(','); first = false;
            b.Append("{\"Name\":").Append(Quote(s.Name)).Append(",\"Value\":").Append(s.Value.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"Source\":").Append(Quote(s.Source)).Append(",\"Kind\":").Append(Quote(s.Kind))
                .Append(",\"HardwareId\":").Append(Quote(s.HardwareId)).Append(",\"HardwareName\":").Append(Quote(s.HardwareName))
                .Append(",\"SensorId\":").Append(Quote(s.SensorId)).Append(",\"SensorName\":").Append(Quote(s.SensorName))
                .Append(",\"Role\":").Append(Quote(s.Role)).Append(",\"SampledUtc\":").Append(Quote(s.SampledUtc)).Append('}');
        }
        b.Append("],\"Diagnostics\":["); first = true;
        foreach (string d in result.Diagnostics) { if (!first) b.Append(','); first = false; b.Append(Quote(d)); }
        Console.WriteLine(b.Append("]}").ToString()); Console.Out.Flush();
    }
}
