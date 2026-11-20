using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace ThermalPilot
{
    internal static class SensorReport
    {
        public static int Run(string path)
        {
            var setup = new List<string>();
            try { SensorPackage.Ensure(setup.Add, CancellationToken.None); }
            catch (Exception e) { setup.Add(HardwareSampler.Detail(e)); }
            var samples = new List<object>(); bool cpu = false, gpu = false, intel = false, nvidiaMeasured = false;
            using (var sensors = new Sensors())
                for (int i = 0; i < 5; i++)
                {
                    var sample = sensors.Capture(); cpu |= sample.Cpu.HasValue; gpu |= sample.Gpu.HasValue;
                    intel |= sample.Rows.Any(x => x.Kind == "GPU" && (x.HardwareName ?? "").IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0);
                    nvidiaMeasured |= sample.Rows.Any(x => x.Source == "NVIDIA nvidia-smi");
                    samples.Add(new { sampled_at = sample.Updated.ToString("o"), cpu_c = sample.Cpu, gpu_c = sample.Gpu, cpu_sensor = sample.CpuSensor, gpu_sensor = sample.GpuSensor, sensors = sample.Rows, diagnostics = sample.Diagnostics });
                    if (i < 4) Thread.Sleep(2000);
                }
            var nvidia = new NvidiaSmiSensors();
            var report = new {
                application = ProductInfo.DisplayName, run_as_administrator = SensorPackage.IsAdministrator, driver_access = SensorPackage.DriverAccess,
                embedded_format = SensorPackage.Format, setup = setup,
                cpu_real_reading = cpu, gpu_real_reading = gpu,
                nvidia_smi_path = nvidia.Executable, nvidia_hardware_test = nvidia.Executable == null ? "NOT_TESTED: nvidia-smi unavailable" : nvidiaMeasured ? "REAL_READING_OBTAINED" : "ATTEMPTED_NO_VALID_READING",
                intel_hardware_test = intel ? "REAL_READING_OBTAINED" : "NOT_TESTED: no valid Intel GPU sample obtained", samples = samples,
                driver_installation_performed = false, elevation_performed = false, network_requests = 0
            };
            File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = 4194304 }.Serialize(report), new UTF8Encoding(false));
            return cpu && gpu ? 0 : 2; // 2 means partial hardware coverage, never a false full success.
        }
    }
}
