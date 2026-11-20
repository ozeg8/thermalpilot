using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

[assembly: AssemblyTitle("ThermalPilot")]
[assembly: AssemblyDescription("Yerel sıcaklık ve performans izleyicisi; REA tabanlı PE inceleme")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

namespace ThermalPilot
{
    internal static class ProductInfo
    {
        public static string Version { get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); } }
        public static string DisplayName { get { return "ThermalPilot " + Version; } }
    }
    internal sealed class HistoryPoint
    {
        public DateTime Time;
        public double? Cpu, CpuTemp, GpuTemp;
    }
    internal sealed class MainController
    {
        private readonly Window window;
        private readonly Telemetry telemetry = new Telemetry();
        private readonly Sensors sensors = new Sensors();
        private readonly Optimizer optimizer;
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private readonly List<HistoryPoint> history = new List<HistoryPoint>();
        private readonly string directory;
        private Sample sample;
        private SensorResult sensorResult = new SensorResult();
        private DateTime lastSensorAttempt = DateTime.MinValue;
        private bool sampling, sensorSampling, closing;
        private string sensorFailure;
        private DateTime lastNotice = DateTime.MinValue;
        private double? previousCpuTemp;
        private DateTime lastAlert = DateTime.MinValue;
        private DateTime lastPowerRefresh = DateTime.MinValue;
        private readonly CancellationTokenSource sensorSetupCancellation = new CancellationTokenSource();
        private bool settingUpSensors, installingDriver;
        public MainController(Window window, bool enableSensorSetup = true)
        {
            this.window = window;
            window.Title = ProductInfo.DisplayName + " — Isı ve Performans";
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThermalPilot");
            optimizer = new Optimizer(directory);
            Click("RefreshButton", async delegate { await Capture(); });
            Click("ExportButton", Export);
            Click("CoolButton", delegate { ApplyMode(PerformanceMode.Quiet); });
            Click("SaverButton", delegate { ApplyMode(PerformanceMode.Quiet); });
            Click("BalancedButton", delegate { ApplyMode(PerformanceMode.Balanced); });
            Click("PerformanceButton", delegate { ApplyMode(PerformanceMode.Performance); });
            Click("RestoreButton", Restore);
            Click("LowerButton", LowerPriority);
            Click("CloseButton", CloseApplication);
            Click("InspectButton", async delegate { await Inspect(); });
            Click("TaskManagerButton", delegate { Open("taskmgr.exe"); });
            Click("StartupButton", delegate { Open("ms-settings:startupapps"); });
            Click("StorageButton", delegate { Open("ms-settings:storagesense"); });
            Click("SetupSensorsButton", async delegate { await SetupSensors(); });
            Click("DriverInstallButton", async delegate { await InstallDriver(); });
            Click("ElevateButton", Elevate);
            if (SensorPackage.IsAdministrator)
            {
                Find<Button>("ElevateButton").Content = "Yönetici erişimi etkin";
                Find<Button>("ElevateButton").IsEnabled = false;
            }
            Click("AboutButton", About);
            Find<TextBox>("SearchBox").TextChanged += delegate { UpdateProcesses(); };
            Find<Canvas>("Chart").SizeChanged += delegate { DrawChart(); };
            window.Closing += OnClosing;
            window.Loaded += async delegate
            {
                if (optimizer.HasChanges)
                {
                    if (Confirm("Önceki oturumdan kalan geri alınabilir ayarlar var. Şimdi geri alınsın mı?\n\n" + optimizer.Summary)) Restore();
                }
                UpdatePower();
                if (enableSensorSetup) BeginSensorSetup();
                timer.Interval = TimeSpan.FromSeconds(3);
                timer.Tick += async delegate { await Capture(); };
                timer.Start();
                await Capture();
                // Capture the first CPU delta without making the user wait a full timer interval.
                await Task.Delay(1000);
                await Capture();
            };
        }
        private async void BeginSensorSetup() { await SetupSensors(); }
        private async Task SetupSensors()
        {
            if (settingUpSensors || closing) return;
            settingUpSensors = true;
            SensorPackage.Preparing = true;
            Find<Button>("SetupSensorsButton").IsEnabled = false;
            try
            {
                await Task.Run(() => SensorPackage.Ensure(delegate(string status)
                {
                    window.Dispatcher.BeginInvoke(new Action(delegate { if (!closing) Label("RuntimeStatus", status); }));
                }, sensorSetupCancellation.Token));
                if (closing) return;
                await Task.Run(() => sensors.Reset()); lastSensorAttempt = DateTime.MinValue;
                Label("RuntimeStatus", "Yerel sensör paketi hazır (" + SensorPackage.Format + "). GPU, Windows WDDM üzerinden ayrıca okunur. Uygulama açılışta yönetici erişimi ister; otomatik sürücü kurulmaz.");
                if (!closing && !sensorSampling) StartSensors();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (!closing)
                {
                    SensorPackage.Status = "Sensör paketi hazırlanamadı: " + e.Message;
                    Label("RuntimeStatus", SensorPackage.Status + "\nTam dağıtımı kullanıp yerel dosya izinlerini kontrol et. Windows GPU okuması bu paketten bağımsız devam eder.");
                    Notice("CPU/RAM takibi çalışıyor; sensör paketi kurulumu tamamlanamadı.");
                }
            }
            finally
            {
                settingUpSensors = false; SensorPackage.Preparing = false;
                if (!closing)
                {
                    Find<Button>("SetupSensorsButton").IsEnabled = true;
                    lastSensorAttempt = DateTime.MinValue;
                    if (SensorPackage.Ready && !sensorSampling) StartSensors();
                }
            }
        }
        private async Task InstallDriver()
        {
            if (installingDriver || closing) return;
            if (!Confirm("CPU sıcaklıklarını okuyabilmek için Windows'a PawnIO donanım erişim sürücüsü kurulacak. Sürücü uygulama kapandıktan sonra da kurulu kalır. Resmî kurucu açılacak ve Windows yönetici onayı isteyebilir.\n\nSürücü kurucusunu şimdi açmak istiyor musun?")) return;
            installingDriver = true;
            bool preparing = SensorPackage.Preparing;
            SensorPackage.Preparing = true;
            Find<Button>("DriverInstallButton").IsEnabled = false;
            try
            {
                if (!SensorPackage.Ready) throw new InvalidOperationException("Önce sensör motorunun hazırlanmasını bekle.");
                Label("RuntimeStatus", "İsteğin üzerine resmî PawnIO sürücü kurucusu açılıyor. Windows yönetici onayı isteyebilir.");
                string installer = await Task.Run(() => SensorPackage.ExtractDriverInstaller());
                if (closing) return;
                using (var process = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Normal }))
                {
                    await Task.Run(() => process.WaitForExit());
                    if (process.ExitCode == 3010) { Label("RuntimeStatus", "Sensör sürücüsü kuruldu. Windows yeniden başlatılmasını istiyor; uygun olduğunda bilgisayarı yeniden başlat."); return; }
                    if (process.ExitCode != 0) throw new InvalidOperationException("Sensör sürücüsü kurulum kodu: " + process.ExitCode);
                }
                if (SensorPackage.DriverAccess != "Ready") { Label("RuntimeStatus", "Kurucu kapandı. Sürücü erişimi: " + SensorPackage.DriverAccess + ". Sürücü kurulumunu ve gerekiyorsa Windows'u yeniden başlatmayı kontrol et. Sıcaklık başarısı henüz doğrulanmadı."); return; }
                await Task.Run(() => sensors.Reset());
                lastSensorAttempt = DateTime.MinValue;
                if (!sensorSampling) StartSensors();
                Label("RuntimeStatus", "Sürücüye erişim sağlandı. Gerçek sıcaklık ölçümleri yeniden deneniyor; yalnızca geçerli değerler gösterilecek.");
            }
            catch (Exception e)
            {
                if (!closing) Label("RuntimeStatus", "Sensör sürücüsü kurulamadı / izin verilmedi: " + e.Message + "\n'Sensör sürücüsünü kur' düğmesiyle yeniden deneyebilirsin.");
            }
            finally
            {
                installingDriver = false; SensorPackage.Preparing = preparing;
                if (!closing)
                {
                    Find<Button>("DriverInstallButton").IsEnabled = true;
                    lastSensorAttempt = DateTime.MinValue;
                }
            }
        }
        private void Elevate()
        {
            if (SensorPackage.IsAdministrator) { Notice("ThermalPilot zaten yönetici olarak çalışıyor."); return; }
            if (!Confirm("ThermalPilot yönetici olarak yeniden açılacak. Windows UAC onayı gösterecek. Otomatik sürücü kurulmaz.\n\nDevam edilsin mi?")) return;
            Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--elevated-start") { UseShellExecute = true, Verb = "runas" });
            window.Close();
        }
        private T Find<T>(string name) where T : class { return (T)window.FindName(name); }
        private void Click(string name, Action action)
        {
            Find<Button>(name).Click += delegate { try { action(); } catch (Exception e) { Error(e); } };
        }
        private void Label(string name, string value) { Find<TextBlock>(name).Text = value; }
        private bool FreshSensors { get { return sensorResult.Updated != DateTime.MinValue && (DateTime.Now - sensorResult.Updated).TotalSeconds <= 10 && sensorFailure == null; } }
        private async Task Capture()
        {
            if (sampling || closing) return;
            sampling = true;
            try
            {
                // Sensor discovery is a separate worker: WMI latency cannot stop CPU/process updates.
                if (!sensorSampling && (DateTime.Now - lastSensorAttempt).TotalSeconds >= 3) StartSensors();
                Sample next = await Task.Run(() => telemetry.Capture());
                if (closing) return;
                sample = next;
                if ((DateTime.Now - lastPowerRefresh).TotalSeconds >= 15) UpdatePower();
                UpdateDashboard();
                UpdateProcesses();
                history.Add(new HistoryPoint { Time = sample.Time, Cpu = sample.Cpu, CpuTemp = FreshSensors ? sensorResult.Cpu : null, GpuTemp = FreshSensors ? sensorResult.Gpu : null });
                history.RemoveAll(x => (sample.Time - x.Time).TotalSeconds > 180);
                DrawChart();
                Label("LiveLabel", "●  CANLI · " + sample.Time.ToString("HH:mm:ss"));
                if ((DateTime.Now - lastNotice).TotalSeconds > 10)
                    Label("Status", sample.Processes.Count + " işlem · " + Environment.ProcessorCount + " mantıksal çekirdek · " + sample.Inaccessible + " işlemde eksik erişim · 3 sn yenileme");
            }
            catch (Exception e)
            {
                Label("LiveLabel", "●  ÖLÇÜM ALINAMADI");
                Label("Status", "Veri yenilenemedi: " + e.Message + " · Görünen değerler önceki ölçümdür.");
            }
            finally { sampling = false; }
        }
        private async void StartSensors()
        {
            sensorSampling = true; lastSensorAttempt = DateTime.Now;
            try
            {
                SensorResult next = await Task.Run(() => sensors.Capture());
                if (closing) return;
                sensorResult = next; sensorFailure = null;
            }
            catch (Exception e) { sensorFailure = "Sensör sorgusu başarısız: " + e.Message; }
            finally
            {
                sensorSampling = false;
                if (!closing) { UpdateSensors(); if (sample != null) UpdateDashboard(); }
            }
        }
        private static string Percent(double? n) { return n.HasValue ? n.Value.ToString("F0") + " %" : "— %"; }
        private static string Temperature(double? n) { return n.HasValue ? n.Value.ToString("F1") + " °C" : "— °C"; }
        private void UpdateSensors()
        {
            bool fresh = FreshSensors;
            Label("CpuTemp", Temperature(fresh ? sensorResult.Cpu : null));
            Label("GpuTemp", Temperature(fresh ? sensorResult.Gpu : null));
            Label("CpuTempSub", fresh && sensorResult.CpuSensor != null ? sensorResult.CpuSensor.Role + " · " + sensorResult.Updated.ToString("HH:mm:ss") : sensorSampling && sensorResult.Updated == DateTime.MinValue ? "Sensörler aranıyor…" : !fresh && sensorResult.Updated != DateTime.MinValue ? "Sensör verisi güncel değil" : sensorResult.CpuUnavailableReason ?? "CPU sensör verisi yok · Sensörler sekmesi");
            Label("GpuTempSub", fresh && sensorResult.GpuSensor != null ? sensorResult.GpuSensor.Role + " · " + sensorResult.GpuSensor.HardwareName : !fresh && sensorResult.Updated != DateTime.MinValue ? "Sensör verisi güncel değil" : "GPU sensör verisi yok · Sensörler sekmesi");
            Find<DataGrid>("SensorGrid").ItemsSource = fresh ? sensorResult.Rows : new List<SensorRow>();
            Label("SensorStatus", (sensorFailure ?? (fresh ? sensorResult.Rows.Count + " sıcaklık sensörü · " + sensorResult.Updated.ToString("HH:mm:ss") : "Henüz güncel sıcaklık verisi yok.")) + "\n\n" + string.Join("\n", sensorResult.Diagnostics));
        }
        private void UpdateDashboard()
        {
            UpdateSensors();
            Label("CpuLoad", Percent(sample.Cpu));
            Label("CpuSub", Environment.ProcessorCount + " mantıksal çekirdek · tüm sistem");
            Label("RamLoad", Percent(sample.MemoryPercent));
            Label("RamSub", sample.TotalMemory > 0 ? ((sample.TotalMemory - sample.AvailableMemory) / 1073741824.0).ToString("F1") + " / " + (sample.TotalMemory / 1073741824.0).ToString("F1") + " GB kullanılıyor" : "Bellek verisi okunamadı");
            Find<DataGrid>("TopGrid").ItemsSource = sample.Processes.Where(x => x.Cpu.HasValue).Take(5).ToArray();
            double? cpuTemp = FreshSensors ? sensorResult.Cpu : null, gpuTemp = FreshSensors ? sensorResult.Gpu : null;
            bool hot = (cpuTemp.HasValue && cpuTemp >= 85) || (gpuTemp.HasValue && gpuTemp >= 85);
            bool high = sample.Cpu >= 75, memory = sample.MemoryPercent >= 85;
            string title = hot ? "Sıcaklık yüksek" : high ? "İşlemci yoğun çalışıyor" : memory ? "Bellek baskısı var" : "Kaynak yükü düşük / orta";
            Label("HealthTitle", title);
            Find<TextBlock>("HealthTitle").Foreground = Brush(hot ? "#FF9D7B" : high || memory ? "#FFB45E" : "#55DEC4");
            string status = !cpuTemp.HasValue && !gpuTemp.HasValue ? "Sıcaklık sensörü okunamadığı için termal durum doğrulanamıyor. " : hot ? "85 °C genel uyarı eşiği aşıldı. Güvenli sınır donanım modeline göre değişir. " : "Okunabilen sensörler 85 °C genel uyarı eşiğinin altında. ";
            status += high ? "Yüksek CPU kullanımı güç tüketimine ve ısınmaya katkıda bulunabilir." : "CPU kullanımını ve uygulama yükünü zaman içinde izle.";
            Label("HealthText", status);
            var top = sample.Processes.FirstOrDefault(x => x.Cpu >= 5);
            var tips = new List<string>();
            if (top != null) tips.Add("• " + top.Name + " toplam CPU'nun %" + top.Cpu.Value.ToString("F1") + " kadarını kullanıyor. Beklenmeyen bir yükse İşlemler sekmesinde incele.");
            if (memory) tips.Add("• Bellek kullanımı yüksek. Kullanmadığın tarayıcı sekmelerini ve büyük uygulamaları kapatmak yardımcı olabilir.");
            if (hot) tips.Add("• Havalandırmayı kontrol et; ağır iş yükünü azaltmayı veya güç tasarrufu planını denemeyi düşün.");
            if (!cpuTemp.HasValue) tips.Add("• CPU sıcaklığı yok. Sensörler sekmesindeki sağlayıcı durumunu kontrol et.");
            if (tips.Count == 0) tips.Add("• Başlangıç uygulamalarını gözden geçir. Düzenli yüksek CPU kullanımı görürsen ilgili işlemi incele.");
            tips.Add("• Öncelik düşürme, rekabet anında diğer uygulamalara kaynak bırakır; tek başına ısı veya hız garantisi vermez.");
            Label("Advice", string.Join("\n\n", tips));
            // Rate-limit audible alerts; readings remain visible even when no sound is possible.
            if (hot && (!previousCpuTemp.HasValue || previousCpuTemp < 85) && (DateTime.Now - lastAlert).TotalMinutes >= 2)
            {
                try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
                lastAlert = DateTime.Now;
            }
            previousCpuTemp = cpuTemp.HasValue || gpuTemp.HasValue ? (double?)Math.Max(cpuTemp ?? 0, gpuTemp ?? 0) : null;
        }
        private void UpdateProcesses()
        {
            if (sample == null) return;
            var grid = Find<DataGrid>("ProcessGrid");
            var selected = grid.SelectedItem as ProcessRow;
            string query = Find<TextBox>("SearchBox").Text.Trim();
            var rows = sample.Processes.Where(x => query.Length == 0 || x.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || x.Id.ToString().Contains(query)).ToArray();
            var sort = grid.Items.SortDescriptions.ToArray();
            grid.ItemsSource = rows;
            grid.Items.SortDescriptions.Clear();
            foreach (var s in sort) grid.Items.SortDescriptions.Add(s);
            if (selected != null) grid.SelectedItem = rows.FirstOrDefault(x => x.Id == selected.Id && x.StartTicks == selected.StartTicks);
        }
        private void DrawChart()
        {
            Canvas canvas = Find<Canvas>("Chart");
            double width = canvas.ActualWidth, height = canvas.ActualHeight;
            if (width <= 1 || height <= 1) return;
            canvas.Children.Clear();
            for (int i = 0; i <= 4; i++)
            {
                double y = 6 + (height - 12) * i / 4;
                canvas.Children.Add(new Line { X1 = 0, X2 = width, Y1 = y, Y2 = y, Stroke = Brush("#233047"), StrokeThickness = 1 });
            }
            if (history.Count == 0) return;
            DateTime end = history[history.Count - 1].Time;
            Plot(canvas, width, height, end, x => x.Cpu, "#55DEC4");
            Plot(canvas, width, height, end, x => x.CpuTemp, "#FFB45E");
            Plot(canvas, width, height, end, x => x.GpuTemp, "#AC9DFF");
        }
        private void Plot(Canvas canvas, double width, double height, DateTime end, Func<HistoryPoint, double?> value, string color)
        {
            Polyline line = null;
            DateTime? previous = null;
            foreach (HistoryPoint p in history)
            {
                double? n = value(p);
                if (!n.HasValue) { line = null; previous = null; continue; }
                if (line == null || (previous.HasValue && (p.Time - previous.Value).TotalSeconds > 12))
                {
                    line = new Polyline { Stroke = Brush(color), StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
                    canvas.Children.Add(line);
                }
                double x = width * (1 - (end - p.Time).TotalSeconds / 180);
                double y = height - 6 - Math.Min(110, Math.Max(0, n.Value)) / 110 * (height - 12);
                line.Points.Add(new Point(x, y)); previous = p.Time;
            }
        }
        private void ApplyMode(PerformanceMode mode)
        {
            var spec = ProfileSpec.For(mode);
            if (!Confirm(spec.Preview)) return;
            try
            {
                var notes = optimizer.ApplyMode(mode);
                Notice(spec.Name + " modu uygulandı. " + string.Join(" ", notes));
                if (notes.Count > 0) MessageBox.Show(window, string.Join("\n", notes), "Mod uygulama bilgisi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally { UpdatePower(); }
        }
        private void UpdatePower()
        {
            lastPowerRefresh = DateTime.Now;
            try
            {
                string mode = optimizer.ActiveMode;
                Label("PowerLabel", "Etkin güç planı: " + Optimizer.PowerName(Optimizer.ActivePower()));
                Label("ActiveModeLabel", "Etkin mod: " + mode);
                Label("ModeBadge", "MOD: " + mode.ToUpperInvariant());
                foreach (string name in new[] { "SaverButton", "BalancedButton", "PerformanceButton" }) Find<Button>(name).ClearValue(Control.BackgroundProperty);
                string selected = mode == "Sessiz / Serin" ? "SaverButton" : mode == "Dengeli" ? "BalancedButton" : mode == "Performans" ? "PerformanceButton" : null;
                if (selected != null) Find<Button>(selected).Background = Brush("#17473F");
            }
            catch (Exception e) { Label("PowerLabel", e.Message); Label("ActiveModeLabel", "Etkin mod okunamadı"); Label("ModeBadge", "MOD: OKUNAMADI"); }
            Label("ChangesLabel", optimizer.HasChanges ? optimizer.Summary : "Geri alınacak değişiklik yok.");
        }
        private void Restore()
        {
            var errors = optimizer.Restore(); UpdatePower();
            if (errors.Count > 0) MessageBox.Show(window, "Bazı ayarlar geri alınamadı; kayıtları tekrar denemek için korundu:\n\n" + string.Join("\n", errors), "Geri alma", MessageBoxButton.OK, MessageBoxImage.Warning);
            else Notice("Geri alma tamamlandı. Sonradan Windows'tan değiştirdiğin ayarlar korundu.");
        }
        private ProcessRow Selected()
        {
            var row = Find<DataGrid>("ProcessGrid").SelectedItem as ProcessRow;
            if (row == null) throw new InvalidOperationException("Önce listeden bir işlem seç.");
            return row;
        }
        private void LowerPriority()
        {
            var row = Selected();
            if (!Confirm(row.Name + " (PID " + row.Id + ") işlem önceliği 'BelowNormal' yapılsın mı?\n\nBu, CPU'ya sabit bir kullanım sınırı koymaz. Ön plandaki uygulamaya kaynak bırakabilir; seçili uygulama yavaşlayabilir. Değişiklik geri alınabilir.")) return;
            optimizer.Lower(row); UpdatePower(); Notice(row.Name + " önceliği düşürüldü.");
        }
        private void CloseApplication()
        {
            var row = Selected();
            if (!Confirm(row.Name + " uygulamasına normal kapatma isteği gönderilsin mi?\n\nKaydedilmemiş çalışmanı kontrol et. Uygulama isteği reddedebilir veya kaydetme iletişimi açabilir.")) return;
            using (Process p = Optimizer.OpenActionTarget(row))
            {
                if (p.MainWindowHandle == IntPtr.Zero) throw new InvalidOperationException("Bu işlemin kapatılabilir bir ana penceresi yok. Görev Yöneticisi ile inceleyebilirsin.");
                if (!p.CloseMainWindow()) throw new InvalidOperationException("Uygulama kapatma isteğini kabul etmedi.");
            }
            Notice(row.Name + " için kapatma isteği gönderildi; zorla sonlandırma yapılmadı.");
        }
        private async Task Inspect()
        {
            try
            {
                ProcessRow row = Selected();
                string path;
                using (Process p = Process.GetProcessById(row.Id))
                {
                    if (p.StartTime.ToUniversalTime().Ticks != row.StartTicks) throw new InvalidOperationException("İşlem kimliği değişti; listeyi yenile.");
                    path = p.MainModule.FileName;
                }
                Find<Button>("InspectButton").IsEnabled = false;
                Notice("Dosya çalıştırılmadan inceleniyor…");
                string report = await Task.Run(() => ReaPeInspector.Inspect(path));
                if (!closing) ShowText("REA · " + row.Name, report);
            }
            catch (Exception e) { if (!closing) Error(e); }
            finally { if (!closing) Find<Button>("InspectButton").IsEnabled = true; }
        }
        private void Export()
        {
            if (sample == null) throw new InvalidOperationException("Önce ölçüm alınmasını bekle.");
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON raporu (*.json)|*.json", FileName = "ThermalPilot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json", AddExtension = true, DefaultExt = ".json" };
            if (dialog.ShowDialog(window) != true) return;
            var serializer = new JavaScriptSerializer { MaxJsonLength = 4194304 };
            var report = new
            {
                application = ProductInfo.DisplayName, rea_source = "https://github.com/morluto/rea",
                exported_at = DateTime.Now.ToString("o"), sample_at = sample.Time.ToString("o"),
                cpu_percent = sample.Cpu, memory_percent = sample.MemoryPercent,
                total_memory_bytes = sample.TotalMemory, available_memory_bytes = sample.AvailableMemory,
                sensors_fresh = FreshSensors, sensor_sample_at = sensorResult.Updated == DateTime.MinValue ? null : sensorResult.Updated.ToString("o"),
                sensors = FreshSensors ? sensorResult.Rows : new List<SensorRow>(),
                sensor_diagnostics = sensorResult.Diagnostics, sensor_error = sensorFailure,
                inaccessible_processes = sample.Inaccessible, processes = sample.Processes,
                history = history.Select(x => new { time = x.Time.ToString("o"), cpu_percent = x.Cpu, cpu_temperature_c = x.CpuTemp, gpu_temperature_c = x.GpuTemp }).ToArray(),
                changes = optimizer.Summary,
                performance_mode = optimizer.ActiveMode,
                limitations = new[] { "İşlem CPU yükü ısıya olası katkıyı gösterir; işlem başına sıcaklık ölçülmez.", "ACPI termal bölgeleri CPU sıcaklığı değildir.", "G/Ç dosya, ağ ve aygıt aktarımını içerebilir.", "85 °C genel bilgilendirme eşiğidir; güvenli sınır donanım modeline bağlıdır." }
            };
            File.WriteAllText(dialog.FileName, serializer.Serialize(report), new UTF8Encoding(false));
            Notice("Rapor kaydedildi: " + dialog.FileName);
        }
        private void About()
        {
            string license;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ReaLicense"))
            using (var reader = new StreamReader(stream)) license = reader.ReadToEnd();
            foreach (string resource in new[] { "LhmLicense", "PawnIoModulesLicense" })
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                using (var reader = new StreamReader(stream)) license += "\n\n" + reader.ReadToEnd();
            ShowText("ThermalPilot · Hakkında", ProductInfo.DisplayName + "\n\nGPU: yerel Windows WDDM ve varsa NVIDIA nvidia-smi.\nCPU: gömülü LibreHardwareMonitorLib 0.9.6. .NET Framework kütüphanesi doğrudan; .NET 10 kütüphanesi aynı ürünün yerel sensör yardımcısında çalışır. Yardımcı mevcut .NET 10 Windows Desktop Runtime'ı kullanır; ayrı izleme uygulaması değildir.\n\nUygulama yönetici erişimini zorunlu tutar; Windows açılışta gerektiğinde UAC onayı ister. Ağ erişimi ve otomatik sürücü kurulumu yapılmaz. Sürücü kurulumu yalnızca ilgili düğme ve açık onayla başlatılır. Ölçüm/raporlar dışarı gönderilmez.\n\nLibreHardwareMonitorLib: MPL-2.0\nhttps://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6\n\nSensörler: " + SensorPackage.Root + "\nGünlükler: " + SensorPackage.LogDirectory + "\\sensor-latest.json ve sensor-events.jsonl\n\nREA PE/COFF okuyucuları C#'a uyarlanmıştır; tam REA motoru gömülü değildir.\nhttps://github.com/morluto/rea\n\n" + license);
        }
        private void ShowText(string title, string text)
        {
            var popup = new Window { Title = title, Owner = window, Width = 850, Height = 640, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush("#0B0F17") };
            var panel = new DockPanel { Margin = new Thickness(18) };
            var save = new Button { Content = "Metin raporunu kaydet", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(save, Dock.Bottom); panel.Children.Add(save);
            panel.Children.Add(new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), Background = Brush("#131B29"), Foreground = Brush("#E8EEF8"), Padding = new Thickness(14) });
            save.Click += delegate
            {
                try
                {
                    var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Metin raporu (*.txt)|*.txt", FileName = "ThermalPilot-REA.txt" };
                    if (dialog.ShowDialog(popup) == true) File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
                }
                catch (Exception e) { MessageBox.Show(popup, e.Message, "Kaydetme başarısız"); }
            };
            popup.Content = panel; popup.ShowDialog();
        }
        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (installingDriver) { e.Cancel = true; Notice("Sensör sürücüsü kurulumu bitince uygulamayı kapatabilirsin."); return; }
            try
            {
                if (Find<CheckBox>("AutoRestore").IsChecked == true && optimizer.HasChanges)
                {
                    var errors = optimizer.Restore();
                    if (errors.Count > 0 && !Confirm("Bazı ayarlar geri alınamadı:\n\n" + string.Join("\n", errors) + "\n\nKayıtlar korunacak. Yine de çıkılsın mı?")) { e.Cancel = true; return; }
                }
            }
            catch (Exception ex)
            {
                if (!Confirm("Geri alma kaydı güncellenemedi: " + ex.Message + "\n\nYine de çıkılsın mı?")) { e.Cancel = true; return; }
            }
            closing = true; timer.Stop();
            sensorSetupCancellation.Cancel();
            sensors.Dispose();
        }
        private void Open(string target) { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        private bool Confirm(string text) { return MessageBox.Show(window, text, "ThermalPilot", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes; }
        private void Error(Exception e) { MessageBox.Show(window, e.Message, "İşlem tamamlanamadı", MessageBoxButton.OK, MessageBoxImage.Information); }
        private void Notice(string text) { lastNotice = DateTime.Now; Label("Status", text); }
        private static SolidColorBrush Brush(string color) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); }
    }
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--power-profile-report") return PowerProfileTests.RunNativeReport(args[1]);
            if (args.Length == 2 && args[0] == "--sensor-report") return SensorReport.Run(args[1]);
            if (args.Length == 2 && args[0] == "--ui-smoke-test") return UiSmokeTest.Run(args[1]);
            if (args.Length > 0 && args[0] == "--self-test") return SelfTests.Run(args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "ThermalPilot-self-test.json"));
            if (args.Length == 3 && args[0] == "--inspect")
            {
                try { File.WriteAllText(args[2], ReaPeInspector.Inspect(args[1]), new UTF8Encoding(false)); return 0; }
                catch (Exception e) { File.WriteAllText(args[2], e.ToString()); return 1; }
            }
            // Windows requires administrator access through the EXE manifest; drivers are installed only on request.
            bool first;
            using (var mutex = new Mutex(true, "Local\\ThermalPilot-" + WindowsIdentity.GetCurrent().User.Value, out first))
            {
                if (!first)
                {
                    if (!args.Contains("--elevated-start") || !mutex.WaitOne(5000)) { MessageBox.Show("ThermalPilot zaten bu oturumda çalışıyor. Yeni sürümü açmadan önce eski pencereyi kapat.", "ThermalPilot"); return 0; }
                }
                try
                {
                    var app = new Application();
                    app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) { MessageBox.Show(e.Exception.Message, "ThermalPilot"); e.Handled = true; };
                    Window main;
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) main = (Window)XamlReader.Load(stream);
                    var controller = new MainController(main);
                    return app.Run(main);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Uygulama başlatılamadı:\n" + e.Message + "\n\nThermalPilot kaydını ve klasör erişim izinlerini kontrol et.", "ThermalPilot", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}
