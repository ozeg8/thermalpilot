using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Markup;

namespace ThermalPilot
{
    internal static class SelfTests
    {
        public static int Run(string output)
        {
            var results = new List<object>();
            int failures = 0;
            Action<string, Action> test = delegate(string name, Action action)
            {
                try { action(); results.Add(new { name = name, passed = true, detail = "OK" }); }
                catch (Exception e) { failures++; results.Add(new { name = name, passed = false, detail = e.ToString() }); }
            };
            test("WPF layout loads with all interactive controls", delegate
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))
                {
                    var window = (Window)XamlReader.Load(stream);
                    string[] names = { "Tabs", "Chart", "ProcessGrid", "TopGrid", "SensorGrid", "CpuTemp", "RefreshButton", "LowerButton", "CloseButton", "InspectButton", "RestoreButton", "ExportButton", "AutoRestore", "SearchBox", "RuntimeStatus", "SetupSensorsButton", "DriverInstallButton", "ElevateButton", "PerformanceButton", "ActiveModeLabel", "ModeBadge" };
                    foreach (string name in names) Assert(window.FindName(name) != null, "Missing control: " + name);
                    window.Measure(new Size(1270, 890)); window.Arrange(new Rect(0, 0, 1270, 890)); window.UpdateLayout(); window.Close();
                }
            });
            test("REA COFF reader preserves fields and rejects truncation", delegate
            {
                byte[] b = new byte[20];
                Array.Copy(BitConverter.GetBytes((ushort)0x8664), 0, b, 0, 2);
                Array.Copy(BitConverter.GetBytes((ushort)7), 0, b, 2, 2);
                Array.Copy(BitConverter.GetBytes((ushort)240), 0, b, 16, 2);
                Array.Copy(BitConverter.GetBytes((ushort)0x22), 0, b, 18, 2);
                var facts = ReaPeInspector.ReadCoff(b);
                Assert(facts.Machine == 0x8664 && facts.SectionCount == 7 && facts.OptionalSize == 240 && facts.Characteristics == 0x22, "COFF fields differ");
                bool refused = false; try { ReaPeInspector.ReadCoff(new byte[19]); } catch (InvalidDataException) { refused = true; }
                Assert(refused, "Truncated header accepted");
            });
            test("REA section reader clips padding and preserves raw size", delegate
            {
                byte[] b = new byte[40];
                Array.Copy(Encoding.ASCII.GetBytes(".text"), b, 5);
                Array.Copy(BitConverter.GetBytes((uint)100), 0, b, 8, 4);
                Array.Copy(BitConverter.GetBytes((uint)4096), 0, b, 12, 4);
                Array.Copy(BitConverter.GetBytes((uint)512), 0, b, 16, 4);
                Array.Copy(BitConverter.GetBytes((uint)1024), 0, b, 20, 4);
                Array.Copy(BitConverter.GetBytes((uint)0x60000020), 0, b, 36, 4);
                var s = ReaPeInspector.ReadSection(b);
                Assert(s.Name == ".text" && s.Size == 100 && s.RawSize == 512 && s.Offset == 1024 && s.VirtualAddress == 4096, "Section fields differ");
                Array.Clear(b, 8, 4); Assert(ReaPeInspector.ReadSection(b).Size == 512, "Zero virtual size rule differs");
                bool refused = false; try { ReaPeInspector.ReadSection(new byte[39]); } catch (InvalidDataException) { refused = true; }
                Assert(refused, "Truncated section accepted");
            });
            test("Static inspection reads built EXE without execution", delegate
            {
                string report = ReaPeInspector.Inspect(Assembly.GetExecutingAssembly().Location);
                Assert(report.Contains("SHA-256:") && report.Contains("x64") && report.Contains(".text"), "EXE report incomplete");
            });
            test("Malformed PE input is rejected", delegate
            {
                string fixture = Path.Combine(Path.GetTempPath(), "ThermalPilot-malformed-" + Guid.NewGuid().ToString("N") + ".bin");
                try
                {
                    File.WriteAllBytes(fixture, new byte[70]);
                    bool refused = false; try { ReaPeInspector.Inspect(fixture); } catch (InvalidDataException) { refused = true; }
                    Assert(refused, "Malformed file accepted");
                }
                finally { File.Delete(fixture); }
            });
            test("CPU and RAM read real Win32 counters; first CPU sample is unknown", delegate
            {
                var telemetry = new Telemetry();
                Sample first = telemetry.Capture(); Thread.Sleep(250); Sample second = telemetry.Capture();
                Assert(first.Cpu == null, "First CPU sample must be unknown");
                Assert(second.Cpu.HasValue && second.Cpu >= 0 && second.Cpu <= 100, "CPU sample out of range");
                Assert(second.TotalMemory > 0 && second.MemoryPercent.HasValue && second.MemoryPercent >= 0 && second.MemoryPercent <= 100, "Memory invalid");
                Assert(second.Processes.Count > 0, "No processes captured");
                var self = second.Processes.FirstOrDefault(x => x.Id == Process.GetCurrentProcess().Id);
                Assert(self != null && self.Cpu.HasValue && self.StartTicks > 0, "Own process delta or identity missing");
            });
            test("Action guard rejects self and an unknown process identity", delegate
            {
                using (var self = Process.GetCurrentProcess())
                {
                    bool refused = false;
                    try { using (Optimizer.OpenActionTarget(new ProcessRow { Id = self.Id, StartTicks = self.StartTime.ToUniversalTime().Ticks })) { } } catch (InvalidOperationException) { refused = true; }
                    Assert(refused, "Self action was allowed");
                    refused = false; try { using (Optimizer.OpenActionTarget(new ProcessRow { Id = self.Id, StartTicks = 0 })) { } } catch (InvalidOperationException) { refused = true; }
                    Assert(refused, "Unknown identity was allowed");
                }
            });
            test("Sensor selection excludes ACPI from CPU; missing values stay null", delegate
            {
                var s = new SensorResult();
                Assert(s.Cpu == null && s.Gpu == null, "Missing sensor is not null");
                s.Rows.Add(new SensorRow { Name = "ACPI", Value = 50, Kind = "ACPI" });
                Assert(s.Cpu == null, "ACPI incorrectly treated as CPU");
                s.Rows.Add(new SensorRow { Name = "CPU package", Value = 61, Kind = "CPU" });
                s.Rows.Add(new SensorRow { Name = "CPU core", Value = 64, Kind = "CPU" });
                Assert(s.Cpu == 61 && s.Gpu == null, "CPU Package must take precedence over a hotter CPU Core");
            });
            test("Integrated hardware reader updates CPU/GPU and skips missing or invalid values", delegate
            {
                var cpu = new HardwareFixture { Name = "CPU fixture", HardwareType = "Cpu", Sensors = new[] { new SensorFixture("Temperature", 61.5f), new SensorFixture("Temperature", null), new SensorFixture("Temperature", Single.NaN), new SensorFixture("Load", 90) } };
                var gpu = new HardwareFixture { Name = "GPU fixture", HardwareType = "GpuNvidia", Sensors = new[] { new SensorFixture("Temperature", 55f, "GPU Core"), new SensorFixture("Temperature", 160f, "GPU Core") } };
                cpu.SubHardware = new[] { gpu };
                var result = new SensorResult();
                Sensors.ReadHardware(cpu, result);
                Assert(cpu.Updated && gpu.Updated, "Hardware update was not invoked");
                Assert(result.Rows.Count == 2 && result.Cpu == 61.5 && result.Gpu == 55, "CPU/GPU mapping or missing-value rejection failed");
                Assert(result.Rows.All(x => x.Source == "LibreHardwareMonitorLib"), "Wrong sensor source attribution");
            });
            test("GPU memory/hotspot never displace GPU Core; CPU Package outranks cores", delegate
            {
                var r = new SensorResult();
                r.Rows.Add(new SensorRow { Kind = "GPU", Role = "GPU Memory", Value = 100 });
                Assert(r.Gpu == null, "GPU memory was incorrectly used as the main GPU temperature");
                r.Rows.Add(new SensorRow { Kind = "GPU", Role = "GPU Hot Spot", Value = 90 });
                r.Rows.Add(new SensorRow { Kind = "GPU", Role = "GPU Core", Value = 65 });
                Assert(r.Gpu == 65 && r.GpuSensor.Role == "GPU Core", "Hotspot replaced GPU Core");
                r.Rows.Add(new SensorRow { Kind = "CPU", Role = "CPU Core", Value = 85 });
                r.Rows.Add(new SensorRow { Kind = "CPU", Role = "CPU Package", Value = 70 });
                Assert(r.Cpu == 70, "CPU package selection is incorrect");
                r.Rows[2].SampledUtc = DateTimeOffset.UtcNow.AddMinutes(-2).ToString("o");
                Assert(r.Gpu == 90 && r.GpuSensor.Role == "GPU Hot Spot", "Expired GPU core remained current or fallback role was hidden");
            });
            test("NVIDIA CSV preserves device identities and rejects N/A, zero and malformed rows", delegate
            {
                var r = new SensorResult();
                NvidiaSmiSensors.Parse("GPU-A, \"NVIDIA GPU, A\", 62\nGPU-B, NVIDIA B, 71\nGPU-C, NVIDIA C, N/A\nGPU-D, NVIDIA D, 0\ninvalid", r);
                Assert(r.Rows.Count == 2 && r.Rows[0].HardwareId == "GPU-A" && r.Rows[0].HardwareName == "NVIDIA GPU, A" && r.Rows[1].Value == 71, "CSV data or per-GPU identity changed");
            });
            test("Bridge refuses stale, invalid and wrong-sequence measurements", delegate
            {
                var serializer = new JavaScriptSerializer();
                string valid = serializer.Serialize(new BridgeReply { Sequence = 3, SampledUtc = DateTimeOffset.UtcNow.ToString("o"), Rows = new List<SensorRow>(), Diagnostics = new List<string>() });
                Assert(BridgeClient.ParseReply(valid, 3).Sequence == 3, "Valid bridge response refused");
                bool refused = false; try { BridgeClient.ParseReply(valid, 4); } catch (InvalidDataException) { refused = true; }
                Assert(refused, "Wrong request sequence was accepted");
                string old = serializer.Serialize(new BridgeReply { Sequence = 3, SampledUtc = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o"), Rows = new List<SensorRow>(), Diagnostics = new List<string>() });
                refused = false; try { BridgeClient.ParseReply(old, 3); } catch (InvalidDataException) { refused = true; }
                Assert(refused, "Stale response accepted");
            });
            test("Windows WDDM native ABI layout matches 64-bit SDK", delegate
            {
                Assert(System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeGpuSensors.PerfData)) == 64 && System.Runtime.InteropServices.Marshal.OffsetOf(typeof(NativeGpuSensors.PerfData), "Temperature").ToInt32() == 56, "WDDM ABI layout mismatch");
            });
            test("Sensor package hash is checked before installation; validated package publishes atomically", delegate
            {
                WithPackageFixture(delegate(string directory)
                {
                    string archive = Path.Combine(directory, "fixture.zip"), target = Path.Combine(directory, "runtime");
                    MakeArchive(archive, "LibreHardwareMonitorLib.dll", "test fixture, not a real assembly");
                    bool refused = false;
                    try { SensorPackage.InstallArchive(archive, target, new string('0', 64), CancellationToken.None); } catch (InvalidDataException) { refused = true; }
                    Assert(refused && !Directory.Exists(target), "Unverified archive was published");
                    string hash; using (var file = File.OpenRead(archive)) hash = SensorPackage.Hash(file);
                    SensorPackage.InstallArchive(archive, target, hash, CancellationToken.None);
                    Assert(File.Exists(Path.Combine(target, "LibreHardwareMonitorLib.dll")) && File.ReadAllText(Path.Combine(target, "verified.sha256")) == hash, "Verified package was not published");
                    Assert(Directory.GetDirectories(directory, "*.stage-*").Length == 0, "Staging directory leaked");
                });
            });
            test("Sensor package refuses ZIP traversal and leaves no ready runtime", delegate
            {
                WithPackageFixture(delegate(string directory)
                {
                    string archive = Path.Combine(directory, "fixture.zip"), target = Path.Combine(directory, "runtime");
                    MakeArchive(archive, "../escaped.txt", "should never be written");
                    string hash; using (var file = File.OpenRead(archive)) hash = SensorPackage.Hash(file);
                    bool refused = false;
                    try { SensorPackage.InstallArchive(archive, target, hash, CancellationToken.None); } catch (InvalidDataException) { refused = true; }
                    Assert(refused && !Directory.Exists(target) && !File.Exists(Path.Combine(directory, "escaped.txt")), "ZIP traversal was not refused");
                    Assert(Directory.GetDirectories(directory, "*.stage-*").Length == 0, "Failed extraction leaked staging");
                });
            });
            test("Cancelled sensor installation never publishes a partially extracted runtime", delegate
            {
                WithPackageFixture(delegate(string directory)
                {
                    string archive = Path.Combine(directory, "fixture.zip"), target = Path.Combine(directory, "runtime");
                    MakeArchive(archive, "LibreHardwareMonitorLib.dll", "test fixture");
                    string hash; using (var file = File.OpenRead(archive)) hash = SensorPackage.Hash(file);
                    bool cancelled = false;
                    using (var token = new CancellationTokenSource())
                    {
                        token.Cancel();
                        try { SensorPackage.InstallArchive(archive, target, hash, token.Token); } catch (OperationCanceledException) { cancelled = true; }
                    }
                    Assert(cancelled && !Directory.Exists(target) && Directory.GetDirectories(directory, "*.stage-*").Length == 0, "Cancelled extraction was published or leaked staging");
                });
            });
            test("Rollback journal survives restart and ignores reused PIDs", delegate
            {
                string directory = Path.Combine(Path.GetTempPath(), "ThermalPilot-journal-test-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                try
                {
                    var j = new ChangeJournal();
                    using (var self = Process.GetCurrentProcess()) j.Priorities.Add(new PriorityChange { Id = self.Id, StartTicks = 1, Original = (int)ProcessPriorityClass.High, Applied = (int)ProcessPriorityClass.Normal, Name = "stale PID fixture" });
                    File.WriteAllText(Path.Combine(directory, "changes.json"), new JavaScriptSerializer().Serialize(j));
                    var optimizer = new Optimizer(directory);
                    Assert(optimizer.HasChanges, "Journal was not loaded");
                    Assert(optimizer.Restore().Count == 0 && !optimizer.HasChanges, "Stale PID was not discarded");
                    Assert(!new Optimizer(directory).HasChanges, "Clean journal was not persisted");
                }
                finally
                {
                    foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                    Directory.Delete(directory);
                }
            });
            test("Action priority lowers and restores only an owned test child", delegate
            {
                string directory = Path.Combine(Path.GetTempPath(), "ThermalPilot-priority-test-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                Process child = null;
                try
                {
                    child = Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe"), "-n 30 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true });
                    var row = new ProcessRow { Id = child.Id, Name = child.ProcessName, StartTicks = child.StartTime.ToUniversalTime().Ticks };
                    var original = child.PriorityClass;
                    var optimizer = new Optimizer(directory);
                    optimizer.Lower(row); child.Refresh();
                    Assert(child.PriorityClass == ProcessPriorityClass.BelowNormal, "Priority not lowered");
                    Assert(optimizer.HasChanges, "Rollback not recorded");
                    var recovered = new Optimizer(directory);
                    var errors = recovered.Restore(); child.Refresh();
                    Assert(errors.Count == 0 && child.PriorityClass == original, "Priority not restored");
                }
                finally
                {
                    if (child != null) { try { if (!child.HasExited) child.Kill(); child.WaitForExit(2000); } catch { } child.Dispose(); }
                    foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                    Directory.Delete(directory);
                }
            });
            PowerProfileTests.Register(test);
            var reportObject = new { executed_at = DateTime.Now.ToString("o"), passed = failures == 0, failures = failures, tests = results };
            File.WriteAllText(output, new JavaScriptSerializer().Serialize(reportObject), new UTF8Encoding(false));
            return failures == 0 ? 0 : 1;
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void MakeArchive(string path, string name, string content)
        {
            using (var file = File.Create(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write(content);
        }
        private static void WithPackageFixture(Action<string> action)
        {
            string directory = Path.Combine(Path.GetTempPath(), "ThermalPilot-package-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try { action(directory); }
            finally
            {
                string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(directory);
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("ThermalPilot-package-test-")) throw new InvalidOperationException("Unexpected test cleanup path");
                Directory.Delete(target, true);
            }
        }
        public sealed class SensorFixture
        {
            public string Name { get; set; }
            public string SensorType { get; set; }
            public float? Value { get; set; }
            public SensorFixture(string type, float? value, string name = "CPU Package") { SensorType = type; Value = value; Name = name; }
        }
        public sealed class HardwareFixture
        {
            public string Name { get; set; }
            public string HardwareType { get; set; }
            public SensorFixture[] Sensors { get; set; }
            public HardwareFixture[] SubHardware { get; set; }
            public bool Updated { get; private set; }
            public HardwareFixture() { SubHardware = new HardwareFixture[0]; Sensors = new SensorFixture[0]; }
            public void Update() { Updated = true; }
        }
    }
}
