using System;
using System.Runtime.InteropServices;
namespace ThermalPilot
{
    internal static class NativeGpuSensors
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Luid { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] internal struct Adapter { public uint Handle; public Luid Luid; public uint Sources; public int Preferred; }
        [StructLayout(LayoutKind.Sequential)] internal struct Enumeration { public uint Count; public IntPtr Adapters; }
        [StructLayout(LayoutKind.Sequential)] internal struct Query { public uint Handle, Type; public IntPtr Data; public uint Bytes; }
        [StructLayout(LayoutKind.Sequential)] internal struct PerfData
        {
            public uint PhysicalIndex;
            public ulong MemoryFrequency, MaxMemoryFrequency, MaxMemoryFrequencyOC, MemoryBandwidth, PcieBandwidth;
            public uint FanRpm, Power, Temperature;
            public byte PowerState;
        }
        [DllImport("gdi32.dll")] private static extern int D3DKMTEnumAdapters2(ref Enumeration e);
        [DllImport("gdi32.dll")] private static extern int D3DKMTQueryAdapterInfo(ref Query q);
        [DllImport("gdi32.dll")] private static extern int D3DKMTCloseAdapter(ref uint handle);
        public static void Capture(SensorResult result)
        {
            IntPtr buffer = IntPtr.Zero;
            Adapter[] adapters = new Adapter[0];
            try
            {
                var e = new Enumeration();
                int status = D3DKMTEnumAdapters2(ref e);
                if (status < 0 || e.Count == 0 || e.Count > 128) { result.Diagnostics.Add("WDDM: bağdaştırıcı listesi alınamadı (NTSTATUS 0x" + status.ToString("X8") + ")."); return; }
                uint capacity = e.Count;
                buffer = Marshal.AllocHGlobal(checked((int)capacity * Marshal.SizeOf(typeof(Adapter))));
                e.Adapters = buffer;
                status = D3DKMTEnumAdapters2(ref e);
                if (status < 0 || e.Count > capacity) { result.Diagnostics.Add("WDDM: bağdaştırıcı açma başarısız (0x" + status.ToString("X8") + ")."); return; }
                adapters = new Adapter[e.Count];
                for (int i = 0; i < adapters.Length; i++) adapters[i] = (Adapter)Marshal.PtrToStructure(IntPtr.Add(buffer, i * Marshal.SizeOf(typeof(Adapter))), typeof(Adapter));
                foreach (Adapter a in adapters) Read(a, result);
            }
            catch (Exception ex) { result.Diagnostics.Add("WDDM: " + ex.GetType().Name + ": " + ex.Message); }
            finally
            {
                foreach (Adapter a in adapters) { uint handle = a.Handle; if (handle != 0) D3DKMTCloseAdapter(ref handle); }
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
        }
        private static int QueryValue(uint handle, uint type, IntPtr data, int bytes)
        {
            var q = new Query { Handle = handle, Type = type, Data = data, Bytes = (uint)bytes };
            return D3DKMTQueryAdapterInfo(ref q);
        }
        private static void Read(Adapter adapter, SensorResult result)
        {
            string name = "GPU " + adapter.Luid.High.ToString("X8") + ":" + adapter.Luid.Low.ToString("X8");
            IntPtr text = Marshal.AllocHGlobal(2080);
            try { if (QueryValue(adapter.Handle, 8, text, 2080) >= 0) name = Marshal.PtrToStringUni(text) ?? name; }
            finally { Marshal.FreeHGlobal(text); }
            IntPtr data = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PerfData)));
            try
            {
                var perf = new PerfData();
                Marshal.StructureToPtr(perf, data, false);
                int status = QueryValue(adapter.Handle, 62, data, Marshal.SizeOf(typeof(PerfData)));
                if (status < 0) { result.Diagnostics.Add("WDDM · " + name + ": sıcaklık sorgusu desteklenmiyor / erişilemiyor (0x" + status.ToString("X8") + ")."); return; }
                perf = (PerfData)Marshal.PtrToStructure(data, typeof(PerfData));
                double temperature = perf.Temperature / 10.0;
                if (temperature <= 0 || temperature > 150) { result.Diagnostics.Add("WDDM · " + name + ": geçerli sıcaklık verisi yok (ham değer " + perf.Temperature + ")."); return; }
                string id = "wddm/" + adapter.Luid.High.ToString("X8") + ":" + adapter.Luid.Low.ToString("X8") + "/0";
                result.Rows.Add(new SensorRow { Name = name + " · GPU Main (WDDM)", Kind = "GPU", Value = temperature, Source = "Windows WDDM", HardwareName = name, HardwareId = id, SensorName = "GPU Main (WDDM)", SensorId = id + "/temperature", Role = "GPU Main (WDDM)", SampledUtc = DateTime.UtcNow.ToString("o") });
                result.Diagnostics.Add("WDDM · " + name + ": ana sıcaklık sensörü okundu.");
            }
            finally { Marshal.FreeHGlobal(data); }
        }
    }
}
