using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;

namespace ThermalPilot
{
    public sealed class PriorityChange
    {
        public int Id { get; set; }
        public long StartTicks { get; set; }
        public string Name { get; set; }
        public int Original { get; set; }
        public int Applied { get; set; }
    }
    public sealed class ChangeJournal
    {
        public string OriginalPower { get; set; }
        public string AppliedPower { get; set; }
        public List<PriorityChange> Priorities { get; set; }
        public List<OwnedPowerPlan> OwnedPowerPlans { get; set; }
        public ChangeJournal() { Priorities = new List<PriorityChange>(); OwnedPowerPlans = new List<OwnedPowerPlan>(); }
    }
    internal sealed partial class Optimizer
    {
        public static readonly Guid Saver = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
        public static readonly Guid Balanced = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        private readonly string path;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private ChangeJournal journal;
        private readonly IPowerBackend power;
        public bool HasChanges { get { return journal.OriginalPower != null || journal.Priorities.Count > 0 || journal.OwnedPowerPlans.Count > 0; } }
        public string Summary { get { return (journal.OriginalPower != null ? "Önceki güç planı geri alma için saklandı. " : "") + (journal.OwnedPowerPlans.Count > 0 ? journal.OwnedPowerPlans.Count + " geçici ThermalPilot planı. " : "") + (journal.Priorities.Count > 0 ? journal.Priorities.Count + " işlem önceliği değiştirildi." : ""); } }
        public Optimizer(string directory, IPowerBackend backend = null)
        {
            power = backend ?? new WindowsPowerBackend();
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "changes.json");
            if (File.Exists(path))
            {
                // Preserve an unreadable journal. Never silently discard rollback information.
                journal = serializer.Deserialize<ChangeJournal>(File.ReadAllText(path, Encoding.UTF8));
                if (journal == null || journal.Priorities == null) throw new InvalidDataException("Değişiklik kaydı okunamadı: " + path);
            }
            else journal = new ChangeJournal();
            if (journal.OwnedPowerPlans == null) journal.OwnedPowerPlans = new List<OwnedPowerPlan>();
        }
        private void Save()
        {
            string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(pending, serializer.Serialize(journal), new UTF8Encoding(false));
                for (int attempt = 0; ; attempt++)
                {
                    try { if (File.Exists(path)) File.Replace(pending, path, null); else File.Move(pending, path); return; }
                    catch (IOException e)
                    {
                        // Windows can briefly hold the replaced file during scanning (observed error 1175).
                        int code = e.HResult & 0xffff;
                        if (attempt >= 4 || (code != 32 && code != 33 && code != 1175)) throw;
                        System.Threading.Thread.Sleep(25 * (attempt + 1));
                    }
                }
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
        public static Guid ActivePower()
        {
            IntPtr p;
            uint code = Native.PowerGetActiveScheme(IntPtr.Zero, out p);
            if (code != 0) throw new InvalidOperationException("Güç planı okunamadı. Windows hata kodu: " + code);
            try { return (Guid)Marshal.PtrToStructure(p, typeof(Guid)); }
            finally { Native.LocalFree(p); }
        }
        public static string PowerName(Guid id)
        {
            if (id == Saver) return "Güç tasarrufu";
            if (id == Balanced) return "Dengeli";
            if (id == new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")) return "Yüksek performans";
            return "Üretici / özel plan (" + id.ToString().Substring(0, 8) + ")";
        }
        public void SetPower(Guid target)
        {
            Guid current = power.Active();
            if (current == target) return;
            string oldOriginal = journal.OriginalPower, oldApplied = journal.AppliedPower;
            if (journal.OriginalPower == null) journal.OriginalPower = current.ToString();
            journal.AppliedPower = target.ToString();
            Save();
            try { power.Activate(target); }
            catch
            {
                journal.OriginalPower = oldOriginal; journal.AppliedPower = oldApplied; Save();
                throw;
            }
        }
        public static Process OpenActionTarget(ProcessRow row)
        {
            if (row == null || row.StartTicks == 0) throw new InvalidOperationException("Kimliği doğrulanabilen bir işlem seç.");
            Process p = Process.GetProcessById(row.Id);
            try
            {
                if (p.Id == Process.GetCurrentProcess().Id || p.Id <= 4) throw new InvalidOperationException("Uygulamanın kendisi ve sistem işlemleri değiştirilemez.");
                if (p.StartTime.ToUniversalTime().Ticks != row.StartTicks) throw new InvalidOperationException("İşlem kapanmış veya kimliği değişmiş. Listeyi yenile.");
                if (p.SessionId != Process.GetCurrentProcess().SessionId) throw new InvalidOperationException("Yalnızca kendi oturumundaki uygulamaları değiştirebilirsin.");
                string[] system = { "csrss", "wininit", "winlogon", "services", "lsass", "smss", "svchost", "dwm", "fontdrvhost", "sihost", "system", "registry", "memory compression", "msmpeng", "securityhealthservice" };
                if (system.Contains(p.ProcessName.ToLowerInvariant())) throw new InvalidOperationException("Bu Windows işlemi korunuyor.");
                IntPtr h = Native.OpenProcess(0x1000, false, row.Id);
                if (h == IntPtr.Zero) throw new InvalidOperationException("İşlemin sahibi doğrulanamadı.");
                try
                {
                    IntPtr token;
                    if (!Native.OpenProcessToken(h, 8, out token)) throw new InvalidOperationException("İşlem sahibi okunamadı.");
                    try
                    {
                        using (var owner = new WindowsIdentity(token))
                        using (var me = WindowsIdentity.GetCurrent())
                            if (!owner.User.Equals(me.User)) throw new InvalidOperationException("Başka bir kullanıcıya ait işlem değiştirilemez.");
                    }
                    finally { Native.CloseHandle(token); }
                }
                finally { Native.CloseHandle(h); }
                int critical, returned;
                if (Native.NtQueryInformationProcess(p.Handle, 29, out critical, 4, out returned) != 0 || critical != 0)
                    throw new InvalidOperationException("Kritik işlem durumu güvenle doğrulanamadı; işlem değiştirilmeyecek.");
                // p.Handle pins the process object; action uses this handle even if the PID is reused.
                if (p.HasExited || p.StartTime.ToUniversalTime().Ticks != row.StartTicks) throw new InvalidOperationException("İşlem artık çalışmıyor.");
                return p;
            }
            catch { p.Dispose(); throw; }
        }
        public void Lower(ProcessRow row)
        {
            using (Process p = OpenActionTarget(row))
            {
                var priority = p.PriorityClass;
                if (priority == ProcessPriorityClass.BelowNormal || priority == ProcessPriorityClass.Idle) throw new InvalidOperationException("İşlemin önceliği zaten düşük.");
                if (journal.Priorities.Any(x => x.Id == row.Id && x.StartTicks == row.StartTicks)) throw new InvalidOperationException("Bu işlem için zaten geri alınabilir bir değişiklik var.");
                var change = new PriorityChange { Id = row.Id, StartTicks = row.StartTicks, Name = row.Name, Original = (int)priority, Applied = (int)ProcessPriorityClass.BelowNormal };
                journal.Priorities.Add(change); Save();
                try { p.PriorityClass = ProcessPriorityClass.BelowNormal; }
                catch { journal.Priorities.Remove(change); Save(); throw; }
            }
        }
        public List<string> Restore()
        {
            var errors = new List<string>();
            if (journal.OriginalPower != null)
            {
                try
                {
                    Guid original = Guid.Parse(journal.OriginalPower);
                    // Do not overwrite a later choice the user made in Windows.
                    if (power.Active().ToString() == journal.AppliedPower) power.Activate(original);
                    journal.OriginalPower = null; journal.AppliedPower = null;
                }
                catch (Exception e) { errors.Add("Güç planı: " + e.Message); }
            }
            try { CleanupPowerPlans(errors); } catch (Exception e) { errors.Add("Geçici plan temizliği: " + e.Message); }
            foreach (var change in journal.Priorities.ToArray())
            {
                try
                {
                    Process p;
                    try { p = Process.GetProcessById(change.Id); }
                    catch (ArgumentException) { journal.Priorities.Remove(change); continue; }
                    using (p)
                    {
                        if (p.HasExited || p.StartTime.ToUniversalTime().Ticks != change.StartTicks) { journal.Priorities.Remove(change); continue; }
                        if ((int)p.PriorityClass == change.Applied) p.PriorityClass = (ProcessPriorityClass)change.Original;
                    }
                    journal.Priorities.Remove(change);
                }
                catch (Exception e) { errors.Add(change.Name + ": " + e.Message); }
            }
            Save();
            return errors;
        }
    }
}
