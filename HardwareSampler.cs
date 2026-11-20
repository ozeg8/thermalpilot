using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
namespace ThermalPilot
{
    // Shared by the .NET Framework reader and the .NET 10 compatibility worker.
    // Uses only public LibreHardwareMonitor APIs; no sensor controls are called.
    internal sealed class HardwareSampler : IDisposable
    {
        private object computer;
        private Type computerType;
        public HardwareSampler(string library)
        {
            try
            {
                var assembly = Assembly.LoadFrom(library);
                computerType = assembly.GetType("LibreHardwareMonitor.Hardware.Computer", true);
                computer = Activator.CreateInstance(computerType);
                computerType.GetProperty("IsCpuEnabled").SetValue(computer, true, null);
                computerType.GetProperty("IsGpuEnabled").SetValue(computer, true, null);
                computerType.GetMethod("Open").Invoke(computer, null);
            }
            catch { Dispose(); throw; }
        }
        public SensorResult Capture()
        {
            var result = new SensorResult();
            foreach (object hardware in (IEnumerable)computerType.GetProperty("Hardware").GetValue(computer, null)) ReadHardware(hardware, result);
            result.Updated = DateTime.Now;
            return result;
        }
        internal static void ReadHardware(object hardware, SensorResult result)
        {
            Type type = hardware.GetType();
            string name = Convert.ToString(type.GetProperty("Name").GetValue(hardware, null));
            try
            {
                type.GetMethod("Update").Invoke(hardware, null);
                string hardwareType = Convert.ToString(type.GetProperty("HardwareType").GetValue(hardware, null));
                string kind = hardwareType.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase) ? "GPU" : hardwareType == "Cpu" ? "CPU" : "Other";
                var identifier = type.GetProperty("Identifier");
                string hardwareId = identifier == null ? name : Convert.ToString(identifier.GetValue(hardware, null));
                int count = 0, missing = 0;
                foreach (object sensor in (IEnumerable)type.GetProperty("Sensors").GetValue(hardware, null))
                {
                    var st = sensor.GetType();
                    if (Convert.ToString(st.GetProperty("SensorType").GetValue(sensor, null)) != "Temperature") continue;
                    object raw = st.GetProperty("Value").GetValue(sensor, null);
                    if (raw == null || !SensorRoles.Valid(Convert.ToDouble(raw, CultureInfo.InvariantCulture))) { missing++; continue; }
                    string sensorName = Convert.ToString(st.GetProperty("Name").GetValue(sensor, null));
                    var sid = st.GetProperty("Identifier");
                    result.Rows.Add(new SensorRow {
                        Name = name + " · " + sensorName, Value = Convert.ToDouble(raw, CultureInfo.InvariantCulture), Source = "LibreHardwareMonitorLib", Kind = kind,
                        HardwareId = hardwareId, HardwareName = name, SensorId = sid == null ? sensorName : Convert.ToString(sid.GetValue(sensor, null)), SensorName = sensorName,
                        Role = SensorRoles.Classify(kind, sensorName), SampledUtc = DateTime.UtcNow.ToString("o")
                    }); count++;
                }
                result.Diagnostics.Add("LHM · " + name + ": " + count + " sıcaklık okundu; " + missing + " boş/0/geçersiz değer kullanılmadı.");
            }
            catch (Exception e) { result.Diagnostics.Add("LHM · " + name + ": " + Detail(e)); }
            var children = type.GetProperty("SubHardware");
            if (children != null) foreach (object child in (IEnumerable)children.GetValue(hardware, null)) ReadHardware(child, result);
        }
        public void Dispose()
        {
            if (computer == null) return;
            try { computerType.GetMethod("Close").Invoke(computer, null); }
            finally { computer = null; }
        }
        internal static string Detail(Exception e)
        {
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
        }
    }
}
