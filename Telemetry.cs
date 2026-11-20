using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace ThermalPilot
{
    public sealed class ProcessRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public long StartTicks { get; set; }
        public double? Cpu { get; set; }
        public long Memory { get; set; }
        public double? IoRate { get; set; }
        public string Priority { get; set; }
        public string CpuText { get { return Cpu.HasValue ? Cpu.Value.ToString("F1") : "—"; } }
        public string MemoryText { get { return (Memory / 1048576.0).ToString("F0") + " MB"; } }
        public string IoText { get { return IoRate.HasValue ? IoRate.Value.ToString("F2") : "—"; } }
        public string Impact { get { return !Cpu.HasValue ? "Ölçüm yok / ilk örnek" : Cpu >= 20 ? "Yüksek CPU yükü" : Cpu >= 5 ? "Belirgin CPU yükü" : Memory > 1073741824L ? "Yüksek bellek kullanımı" : "Düşük CPU yükü"; } }
    }
    public sealed class Sample
    {
        public DateTime Time;
        public double? Cpu;
        public double? MemoryPercent;
        public ulong TotalMemory;
        public ulong AvailableMemory;
        public List<ProcessRow> Processes = new List<ProcessRow>();
        public int Inaccessible;
    }
    internal sealed class Baseline
    {
        public long Start, Cpu;
        public double Time;
        public ulong? Io;
    }
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct FileTime { public uint Low, High; public ulong Value { get { return ((ulong)High << 32) | Low; } } }
        [StructLayout(LayoutKind.Sequential)] public struct MemoryStatus
        {
            public uint Length, Load;
            public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, Extended;
        }
        [StructLayout(LayoutKind.Sequential)] public struct IoCounters
        {
            public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        }
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetProcessIoCounters(IntPtr handle, out IoCounters io);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int id);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("advapi32.dll", SetLastError = true)] public static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("ntdll.dll")] public static extern int NtQueryInformationProcess(IntPtr process, int informationClass, out int info, int length, out int returned);
        [DllImport("powrprof.dll")] public static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
        [DllImport("powrprof.dll")] public static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
        [DllImport("kernel32.dll")] public static extern IntPtr LocalFree(IntPtr memory);
    }
    internal sealed class Telemetry
    {
        private Dictionary<int, Baseline> previous = new Dictionary<int, Baseline>();
        private ulong lastIdle, lastTotal;
        private bool cpuReady;
        public Sample Capture()
        {
            Sample s = new Sample { Time = DateTime.Now };
            Native.FileTime idle, kernel, user;
            if (Native.GetSystemTimes(out idle, out kernel, out user))
            {
                ulong total = kernel.Value + user.Value;
                if (cpuReady && total > lastTotal && idle.Value >= lastIdle)
                    s.Cpu = Clamp(100.0 * (1.0 - (double)(idle.Value - lastIdle) / (total - lastTotal)));
                lastIdle = idle.Value; lastTotal = total; cpuReady = true;
            }
            Native.MemoryStatus memory = new Native.MemoryStatus();
            memory.Length = (uint)Marshal.SizeOf(memory);
            if (Native.GlobalMemoryStatusEx(ref memory))
            {
                s.TotalMemory = memory.TotalPhysical; s.AvailableMemory = memory.AvailablePhysical;
                if (memory.TotalPhysical > 0) s.MemoryPercent = 100.0 * (1.0 - (double)memory.AvailablePhysical / memory.TotalPhysical);
            }
            var next = new Dictionary<int, Baseline>();
            foreach (Process p in Process.GetProcesses())
            {
                using (p)
                {
                    ProcessRow row = new ProcessRow();
                    try { row.Id = p.Id; row.Name = p.ProcessName; row.Memory = p.WorkingSet64; }
                    catch { s.Inaccessible++; continue; }
                    if (row.Id == 0) continue;
                    try
                    {
                        row.StartTicks = p.StartTime.ToUniversalTime().Ticks;
                        long ticks = p.TotalProcessorTime.Ticks;
                        double now = (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
                        ulong? io = ReadIo(row.Id);
                        Baseline old;
                        if (previous.TryGetValue(row.Id, out old) && old.Start == row.StartTicks && now > old.Time)
                        {
                            double elapsed = now - old.Time;
                            if (ticks >= old.Cpu) row.Cpu = Clamp((ticks - old.Cpu) / 10000000.0 / elapsed / Environment.ProcessorCount * 100);
                            if (io.HasValue && old.Io.HasValue && io >= old.Io)
                                row.IoRate = (io.Value - old.Io.Value) / elapsed / 1048576.0;
                        }
                        next[row.Id] = new Baseline { Start = row.StartTicks, Cpu = ticks, Time = now, Io = io };
                        try { row.Priority = p.PriorityClass.ToString(); } catch { row.Priority = "Erişim yok"; }
                    }
                    catch { row.Priority = "Erişim yok"; s.Inaccessible++; }
                    s.Processes.Add(row);
                }
            }
            previous = next;
            s.Processes = s.Processes.OrderByDescending(x => x.Cpu ?? -1).ThenByDescending(x => x.Memory).ToList();
            return s;
        }
        private static ulong? ReadIo(int pid)
        {
            IntPtr h = Native.OpenProcess(0x1000, false, pid);
            if (h == IntPtr.Zero) return null;
            try { Native.IoCounters io; return Native.GetProcessIoCounters(h, out io) ? (ulong?)(io.ReadBytes + io.WriteBytes) : null; }
            finally { Native.CloseHandle(h); }
        }
        private static double Clamp(double n) { return Math.Max(0, Math.Min(100, n)); }
    }
}
