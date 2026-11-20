using System;
using ThermalPilot;
class ProbeGpu
{
    static int Main()
    {
        var sample = new SensorResult(); NativeGpuSensors.Capture(sample);
        foreach (string d in sample.Diagnostics) Console.WriteLine(d);
        foreach (var s in sample.Rows) Console.WriteLine(s.Name + " = " + s.ValueText);
        Console.WriteLine("PerfData bytes=" + System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeGpuSensors.PerfData)));
        return 0;
    }
}
