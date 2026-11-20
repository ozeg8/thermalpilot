using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ThermalPilot
{
    internal static class UiSmokeTest
    {
        public static int Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var results = new List<string>();
            var errors = new List<string>();
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) { errors.Add(e.Exception.ToString()); e.Handled = true; };
            Window window;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) window = (Window)XamlReader.Load(stream);
            // Exercise the real dispatcher and asynchronous telemetry without showing a test window.
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000; window.Top = -10000;
            window.ShowInTaskbar = false; window.ShowActivated = false;
            var controller = new MainController(window, false);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            timer.Tick += async delegate
            {
                timer.Stop();
                try
                {
                    var cpu = (TextBlock)window.FindName("CpuLoad");
                    var ram = (TextBlock)window.FindName("RamLoad");
                    if (cpu.Text.Contains("—") || ram.Text.Contains("—")) throw new InvalidOperationException("Live CPU/RAM values were not populated: " + cpu.Text + " / " + ram.Text);
                    results.Add("Real live values populated: CPU " + cpu.Text + ", RAM " + ram.Text);
                    var grid = (DataGrid)window.FindName("ProcessGrid");
                    if (grid.Items.Count == 0) throw new InvalidOperationException("Process grid is empty");
                    int count = grid.Items.Count;
                    var search = (TextBox)window.FindName("SearchBox");
                    search.Text = "thermalpilot";
                    if (grid.Items.Count == 0 || grid.Items.Cast<ProcessRow>().Any(x => x.Name.IndexOf("thermalpilot", StringComparison.OrdinalIgnoreCase) < 0)) throw new InvalidOperationException("Process filter failed");
                    search.Text = "";
                    if (grid.Items.Count != count) throw new InvalidOperationException("Process filter reset failed");
                    results.Add("Process filter and reset passed: " + count + " real processes");
                    var tabs = (TabControl)window.FindName("Tabs");
                    foreach (var size in new[] { new Size(1270, 890), new Size(1050, 740) })
                    {
                        window.Width = size.Width; window.Height = size.Height;
                        for (int i = 0; i < tabs.Items.Count; i++)
                        {
                            tabs.SelectedIndex = i;
                            await Task.Delay(120);
                            window.UpdateLayout();
                            var root = (FrameworkElement)window.Content;
                            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                            var visual = new DrawingVisual();
                            using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
                            bitmap.Render(visual);
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            string name = "tab-" + i + "-" + size.Width + "x" + size.Height + ".png";
                            using (var file = File.Create(Path.Combine(directory, name))) encoder.Save(file);
                            results.Add("Rendered " + name);
                        }
                    }
                    results.Add("Sensor state: " + ((TextBlock)window.FindName("SensorStatus")).Text);
                    results.Add("Status: " + ((TextBlock)window.FindName("Status")).Text);
                    // Exercise selection-preserving refresh and ensure sort descriptions do not accumulate.
                    grid.Items.SortDescriptions.Add(new SortDescription("Memory", ListSortDirection.Descending));
                    var update = typeof(MainController).GetMethod("UpdateProcesses", BindingFlags.NonPublic | BindingFlags.Instance);
                    for (int i = 0; i < 5; i++) update.Invoke(controller, null);
                    if (grid.Items.SortDescriptions.Count != 1) throw new InvalidOperationException("Sort descriptions accumulate");
                    results.Add("Sorting survives repeated refresh without duplication");
                }
                catch (Exception e) { errors.Add(e.ToString()); }
                finally
                {
                    File.WriteAllText(Path.Combine(directory, "ui-smoke-results.json"), new JavaScriptSerializer().Serialize(new { passed = errors.Count == 0, results = results, errors = errors }), new UTF8Encoding(false));
                    window.Close(); app.Shutdown(errors.Count == 0 ? 0 : 1);
                }
            };
            timer.Start(); window.Show();
            return app.Run();
        }
    }
}
