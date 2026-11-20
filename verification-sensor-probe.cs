using System;
using System.Collections;
using System.Reflection;
using System.Threading;
class SensorProbe
{
    static int Main(string[] args)
    {
        object computer = null;
        try
        {
            var assembly = Assembly.LoadFrom(args[0]);
            Console.WriteLine("Library: " + assembly.FullName);
            Type type = assembly.GetType("LibreHardwareMonitor.Hardware.Computer", true);
            computer = Activator.CreateInstance(type);
            type.GetProperty("IsCpuEnabled").SetValue(computer, true, null);
            type.GetProperty("IsGpuEnabled").SetValue(computer, true, null);
            type.GetMethod("Open").Invoke(computer, null);
            for (int i = 0; i < 3; i++)
            {
                foreach (object h in (IEnumerable)type.GetProperty("Hardware").GetValue(computer, null)) Read(h);
                Thread.Sleep(1000);
            }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }
        finally { if (computer != null) computer.GetType().GetMethod("Close").Invoke(computer, null); }
    }
    static void Read(object h)
    {
        var t = h.GetType(); t.GetMethod("Update").Invoke(h, null);
        Console.WriteLine("Hardware: " + t.GetProperty("Name").GetValue(h, null) + " / " + t.GetProperty("HardwareType").GetValue(h, null));
        foreach (object s in (IEnumerable)t.GetProperty("Sensors").GetValue(h, null))
        {
            var st = s.GetType();
            if (Convert.ToString(st.GetProperty("SensorType").GetValue(s, null)) == "Temperature")
                Console.WriteLine("Temperature: " + st.GetProperty("Name").GetValue(s, null) + " = " + (st.GetProperty("Value").GetValue(s, null) ?? "UNAVAILABLE"));
        }
        foreach (object child in (IEnumerable)t.GetProperty("SubHardware").GetValue(h, null)) Read(child);
    }
}
