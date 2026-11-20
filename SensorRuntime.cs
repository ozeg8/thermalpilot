using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

namespace ThermalPilot
{
    internal static partial class SensorPackage
    {
        public const string Version = "0.9.6";
        public static readonly string Format, ExpectedHash, Root, LibraryPath;
        public static string Status = "Yerel sensör paketi hazırlanıyor…";
        public static volatile bool Preparing;
        public static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThermalPilot");
        static SensorPackage()
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("SensorDescriptor.json"))
            using (var text = new StreamReader(resource))
            {
                var descriptor = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(text.ReadToEnd());
                Format = descriptor["format"]; ExpectedHash = descriptor["sha256"];
            }
            Root = Path.Combine(LogDirectory, "sensors", "lhm-" + Version + "-" + Format + "-" + ExpectedHash.Substring(0,12));
            LibraryPath = Path.Combine(Root, "LibreHardwareMonitorLib.dll");
        }
        public static bool Ready { get { return File.Exists(LibraryPath) && File.Exists(Path.Combine(Root, "verified.sha256")); } }
        public static bool IsAdministrator { get { using (var identity = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); } }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        public static string DriverAccess
        {
            get
            {
                using (var handle = CreateFile(@"\\?\GLOBALROOT\Device\PawnIO", 3, 3, IntPtr.Zero, 3, 0x80, IntPtr.Zero))
                {
                    if (!handle.IsInvalid) return "Ready";
                    int code = Marshal.GetLastWin32Error();
                    return code == 5 ? "AccessDenied (Windows 5)" : code == 2 || code == 3 ? "NotInstalled" : "Unavailable (Windows " + code + ")";
                }
            }
        }
        public static bool DriverInstalled { get { return DriverAccess != "NotInstalled"; } }
        public static string DotnetHost
        {
            get
            {
                string candidate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
                return File.Exists(candidate) ? candidate : null;
            }
        }
        public static string Hash(Stream stream) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        public static void Ensure(Action<string> progress, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(Root));
            string archivePath = Path.Combine(Path.GetDirectoryName(Root), "lhm-" + Format + "-" + ExpectedHash.Substring(0,12) + ".zip");
            if (!File.Exists(archivePath))
            {
                string pending = archivePath + ".tmp";
                try
                {
                    using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("IntegratedSensors.zip"))
                    {
                        if (source == null) throw new InvalidDataException("Sensör paketi bu EXE'ye gömülmemiş. Tam dağıtımı kullan; uygulama internetten indirme yapmaz.");
                        using (var target = File.Create(pending)) source.CopyTo(target);
                    }
                    using (var source = File.OpenRead(pending)) if (Hash(source) != ExpectedHash) throw new InvalidDataException("Gömülü sensör ZIP özeti yanlış.");
                    cancellation.ThrowIfCancellationRequested(); File.Move(pending, archivePath);
                }
                finally { if (File.Exists(pending)) File.Delete(pending); }
            }
            using (var source = File.OpenRead(archivePath)) if (Hash(source) != ExpectedHash) throw new InvalidDataException("Yerel sensör ZIP'i bozulmuş. Sensör klasörünü yedekleyip uygulamayı tekrar aç.");
            if (!Ready) { Status = "Gömülü sensör dosyaları yerleştiriliyor…"; progress(Status); InstallArchive(archivePath, Root, ExpectedHash, cancellation); }
            ValidateInstalled();
            if (Format == "net10") WriteBridge();
            Status = "Sensör paketi yerelden hazırlandı (" + Format + "). Ağ erişimi yapılmadı."; progress(Status);
        }
        public static void ValidateInstalled()
        {
            if (!Ready) throw new InvalidDataException("Yerel sensör kütüphanesi eksik.");
            string archivePath = Path.Combine(Path.GetDirectoryName(Root), "lhm-" + Format + "-" + ExpectedHash.Substring(0,12) + ".zip");
            using (var source = File.OpenRead(archivePath)) if (Hash(source) != ExpectedHash) throw new InvalidDataException("Sensör ZIP özeti yanlış.");
            // Compare against the pinned archive, not a mutable local manifest.
            using (var archive = ZipFile.OpenRead(archivePath))
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.Length == 0) continue;
                    string local = Path.GetFullPath(Path.Combine(Root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!local.StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Geçersiz sensör yolu.");
                    using (var expected = entry.Open()) using (var actual = File.OpenRead(local))
                        if (Hash(expected) != Hash(actual)) throw new InvalidDataException("Sensör dosyası değişmiş: " + entry.FullName);
                }
        }
        private static void WriteBridge()
        {
            string path = Path.Combine(Root, "ThermalPilot.SensorBridge.dll");
            byte[] expected;
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("SensorBridge.dll"))
            using (var memory = new MemoryStream()) { source.CopyTo(memory); expected = memory.ToArray(); }
            bool matches = false;
            if (File.Exists(path)) using (var file = File.OpenRead(path)) using (var input = new MemoryStream(expected)) matches = Hash(file) == Hash(input);
            if (!matches) File.WriteAllBytes(path, expected);
        }
        public static ProcessStartInfo BridgeStart(string arguments)
        {
            if (DotnetHost == null) throw new InvalidOperationException("Bu dağıtımdaki .NET 10 sensör kütüphanesi için .NET 10 Windows Desktop Runtime gerekli. Windows WDDM GPU okuması bağımsız çalışır.");
            string config = Path.Combine(Root, "LibreHardwareMonitor.runtimeconfig.json"), deps = Path.Combine(Root, "LibreHardwareMonitor.deps.json"), worker = Path.Combine(Root, "ThermalPilot.SensorBridge.dll");
            return new ProcessStartInfo(DotnetHost, "exec --runtimeconfig \"" + config + "\" --depsfile \"" + deps + "\" \"" + worker + "\" " + arguments) {
                WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
        }
        public static string ExtractDriverInstaller()
        {
            ValidateInstalled();
            string directory = Path.Combine(LogDirectory, "driver-setup"); Directory.CreateDirectory(directory);
            string installer = Path.Combine(directory, "PawnIO-0.9.6-setup.exe");
            if (Format == "net10")
            {
                using (var process = Process.Start(BridgeStart("--extract-driver \"" + installer + "\"")))
                {
                    var error = process.StandardError.ReadToEndAsync(); var output = process.StandardOutput.ReadToEndAsync();
                    if (!process.WaitForExit(15000)) { process.Kill(); process.WaitForExit(); throw new TimeoutException("Sürücü kurucu kaynağı zamanında okunamadı."); }
                    string detail = error.GetAwaiter().GetResult(); output.GetAwaiter().GetResult();
                    if (process.ExitCode != 0 || !File.Exists(installer)) throw new InvalidOperationException("Sürücü kaynağı çıkarılamadı: " + detail);
                }
            }
            else
            {
                var metadata = Assembly.ReflectionOnlyLoadFrom(Path.Combine(Root, "LibreHardwareMonitor.exe"));
                string resource = metadata.GetManifestResourceNames().Single(x => x.EndsWith(".PawnIO_setup.exe", StringComparison.OrdinalIgnoreCase));
                using (var input = metadata.GetManifestResourceStream(resource)) using (var target = File.Create(installer)) input.CopyTo(target);
            }
            return installer;
        }
    }
    public sealed class BridgeReply
    {
        public int Sequence { get; set; }
        public string SampledUtc { get; set; }
        public List<SensorRow> Rows { get; set; }
        public List<string> Diagnostics { get; set; }
    }
    internal sealed class BridgeClient : IDisposable
    {
        private Process child;
        private int sequence;
        private readonly StringBuilder errors = new StringBuilder();
        public void Capture(SensorResult result)
        {
            if (child == null)
            {
                child = Process.Start(SensorPackage.BridgeStart(""));
                child.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (errors) { if (errors.Length < 8192) errors.AppendLine(e.Data); } };
                child.BeginErrorReadLine();
            }
            if (child.HasExited) throw new InvalidOperationException("Sensör yardımcısı kapandı: " + ErrorText());
            int request = ++sequence;
            child.StandardInput.WriteLine(request); child.StandardInput.Flush();
            for (int skip = 0; skip < 16; skip++)
            {
                var read = child.StandardOutput.ReadLineAsync();
                if (!read.Wait(8000)) { Dispose(); throw new TimeoutException("Sensör yanıtı 8 saniyede alınamadı. Eski değer güncel gösterilmedi."); }
                string line = read.GetAwaiter().GetResult();
                if (line == null) throw new InvalidOperationException("Sensör yardımcısı yanıt vermedi: " + ErrorText());
                if (!line.StartsWith("TP_SENSOR1:", StringComparison.Ordinal)) continue;
                var reply = ParseReply(line.Substring(11), request);
                result.Rows.AddRange(reply.Rows); result.Diagnostics.AddRange(reply.Diagnostics); return;
            }
            throw new InvalidDataException("Sensör yardımcısı beklenen protokol yanıtını vermedi.");
        }
        internal static BridgeReply ParseReply(string json, int sequence)
        {
            var reply = new JavaScriptSerializer { MaxJsonLength = 1048576 }.Deserialize<BridgeReply>(json);
            DateTimeOffset time;
            if (reply == null || reply.Sequence != sequence || reply.Rows == null || reply.Diagnostics == null || !DateTimeOffset.TryParse(reply.SampledUtc, out time) || Math.Abs((DateTimeOffset.UtcNow - time).TotalSeconds) > 10) throw new InvalidDataException("Sensör yanıtı eksik, eski veya yanlış isteğe ait.");
            foreach (var row in reply.Rows)
                if (!SensorRoles.Valid(row.Value) || (row.Kind != "CPU" && row.Kind != "GPU" && row.Kind != "Other")) throw new InvalidDataException("Sensör yardımcısı geçersiz ölçüm döndürdü.");
            return reply;
        }
        private string ErrorText() { lock (errors) return errors.ToString().Trim(); }
        public void Dispose()
        {
            if (child == null) return;
            try
            {
                if (!child.HasExited) { try { child.StandardInput.WriteLine("quit"); child.StandardInput.Flush(); } catch { } if (!child.WaitForExit(1500)) { child.Kill(); child.WaitForExit(1500); } }
            }
            finally { child.Dispose(); child = null; }
        }
    }
    internal sealed class Sensors : IDisposable
    {
        private readonly object gate = new object();
        private readonly NvidiaSmiSensors nvidia = new NvidiaSmiSensors();
        private HardwareSampler direct;
        private BridgeClient bridge;
        private bool disposed;
        private DateTime retryAfter;
        public SensorResult Capture()
        {
            lock (gate)
            {
                var result = new SensorResult(); if (disposed) return result;
                // GPU providers are independent of CPU libraries, preparation and administrator rights.
                NativeGpuSensors.Capture(result);
                nvidia.Capture(result);
                if (SensorPackage.Preparing || !SensorPackage.Ready) result.Diagnostics.Add("LHM: " + SensorPackage.Status);
                else if (DateTime.UtcNow < retryAfter) result.Diagnostics.Add("LHM: önceki hata sonrası 30 saniyelik yeniden deneme bekleniyor.");
                else
                {
                    try
                    {
                        if (SensorPackage.Format == "net10") { if (bridge == null) bridge = new BridgeClient(); bridge.Capture(result); }
                        else { if (direct == null) { SensorPackage.ValidateInstalled(); direct = new HardwareSampler(SensorPackage.LibraryPath); } var sample = direct.Capture(); result.Rows.AddRange(sample.Rows); result.Diagnostics.AddRange(sample.Diagnostics); }
                    }
                    catch (Exception e) { result.Diagnostics.Add("LHM: " + HardwareSampler.Detail(e)); ResetReader(); retryAfter = DateTime.UtcNow.AddSeconds(30); }
                }
                string driver = SensorPackage.DriverAccess;
                result.Diagnostics.Add("PawnIO erişimi: " + driver + "; yönetici: " + SensorPackage.IsAdministrator + ".");
                if (!result.Cpu.HasValue) result.CpuUnavailableReason = driver == "NotInstalled" ? "PawnIO sürücüsü eksik · Sensörler sekmesi" : driver.StartsWith("AccessDenied") ? "Sensör erişimi reddedildi · Sensörler sekmesi" : "Geçerli CPU sensör ölçümü yok · Sensörler";
                if (!result.Cpu.HasValue) result.Diagnostics.Add(driver.StartsWith("AccessDenied") ? "CPU sıcaklığı için sensör sürücüsüne erişim reddedildi. Uygulama yönetici erişimi ister; PawnIO sürücüsünün kurulumunu ve Windows güvenlik izinlerini kontrol et." : driver == "NotInstalled" ? "CPU için PawnIO sürücüsü bulunamadı. İstersen açıklamayı okuyup 'Sensör sürücüsünü kur' düğmesini kullan." : "CPU Package / Tctl-Tdie sensöründen geçerli sıcaklık alınamadı. Donanım desteği, sürücü ve izinleri kontrol et.");
                if (!result.Gpu.HasValue) result.Diagnostics.Add("GPU ana sıcaklığı alınamadı. Ekran kartının üretici sürücüsünü ve desteklenen sensörleri kontrol et; bellek/hotspot ana çekirdek olarak etiketlenmedi.");
                result.Updated = DateTime.Now;
                SensorLog.Record(result);
                return result;
            }
        }
        public void Reset() { lock (gate) { ResetReader(); retryAfter = DateTime.MinValue; } }
        private void ResetReader() { if (direct != null) { direct.Dispose(); direct = null; } if (bridge != null) { bridge.Dispose(); bridge = null; } }
        internal static void ReadHardware(object hardware, SensorResult result) { HardwareSampler.ReadHardware(hardware, result); }
        public void Dispose() { lock (gate) { disposed = true; ResetReader(); } }
    }
    internal static class SensorLog
    {
        private static string lastState;
        public static void Record(SensorResult result)
        {
            try
            {
                Directory.CreateDirectory(SensorPackage.LogDirectory);
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(new { time = DateTimeOffset.Now.ToString("o"), cpu = result.Cpu, gpu = result.Gpu, cpu_sensor = result.CpuSensor, gpu_sensor = result.GpuSensor, rows = result.Rows, diagnostics = result.Diagnostics, administrator = SensorPackage.IsAdministrator, pawnio = SensorPackage.DriverAccess });
                string latest = Path.Combine(SensorPackage.LogDirectory, "sensor-latest.json");
                File.WriteAllText(latest + ".tmp", json, new UTF8Encoding(false));
                if (File.Exists(latest)) File.Replace(latest + ".tmp", latest, null); else File.Move(latest + ".tmp", latest);
                string state = string.Join("\n", result.Diagnostics);
                if (state != lastState)
                {
                    string log = Path.Combine(SensorPackage.LogDirectory, "sensor-events.jsonl");
                    if (File.Exists(log) && new FileInfo(log).Length > 1048576) { string backup = log + ".previous"; if (File.Exists(backup)) File.Delete(backup); File.Move(log, backup); }
                    File.AppendAllText(log, json + Environment.NewLine, new UTF8Encoding(false)); lastState = state;
                }
            }
            catch (Exception e) { result.Diagnostics.Add("Yerel sensör günlüğü yazılamadı: " + e.Message); }
        }
    }
}
