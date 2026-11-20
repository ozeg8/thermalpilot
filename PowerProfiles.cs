using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ThermalPilot
{
    internal enum PerformanceMode { Quiet, Balanced, Performance }
    internal sealed class ProfileSetting
    {
        public Guid Id;
        public string Name;
        public uint Ac, Dc;
        public bool Required;
    }
    internal sealed class ProfileSpec
    {
        public PerformanceMode Mode;
        public string Name, Description;
        public List<ProfileSetting> Settings;
        public static ProfileSpec For(PerformanceMode mode)
        {
            bool quiet = mode == PerformanceMode.Quiet, fast = mode == PerformanceMode.Performance;
            return new ProfileSpec {
                Mode = mode,
                Name = quiet ? "Sessiz / Serin" : fast ? "Performans" : "Dengeli",
                Description = quiet ? "Daha düşük güç tüketimi ve ısı hedefler; ağır işlerde hız azalabilir." : fast ? "Daha hızlı tepki ve yüksek performansı tercih eder; ısı, fan sesi ve pil tüketimi artabilir." : "Günlük kullanımda performans ve enerji tüketimini dengeler.",
                Settings = new List<ProfileSetting> {
                    new ProfileSetting { Id = new Guid("893dee8e-2bef-41e0-89c6-b55d0929964c"), Name = "CPU alt performans sınırı", Ac = 5, Dc = 5, Required = true },
                    new ProfileSetting { Id = new Guid("bc5038f7-23e0-4960-96da-33abaf5935ec"), Name = "CPU üst performans sınırı", Ac = quiet ? 70u : 100u, Dc = quiet ? 50u : 100u, Required = true },
                    new ProfileSetting { Id = new Guid("be337238-0d82-4146-a960-4f3749d470c7"), Name = "İşlemci boost politikası", Ac = quiet ? 0u : fast ? 2u : 1u, Dc = quiet ? 0u : 1u },
                    new ProfileSetting { Id = new Guid("36687f9e-e3a5-4dbf-b1dc-15eb381c6863"), Name = "Enerji tasarrufu tercihi (EPP)", Ac = quiet ? 80u : fast ? 0u : 50u, Dc = quiet ? 90u : fast ? 25u : 75u }
                }
            };
        }
        public string Preview
        {
            get { var max = Settings[1]; return Name + "\n\n" + Description + "\n\nCPU üst performans sınırı: prizde %" + max.Ac + ", pilde %" + max.Dc + ".\nBoost: " + (Mode == PerformanceMode.Quiet ? "kapalı" : "donanım destekliyorsa açık") + ".\n\nWindows'ta geçici bir ThermalPilot güç planı oluşturulur. Mevcut planın ayarları korunur. Geri al düğmesiyle veya çıkışta eski plana dönülebilir.\n\nMod uygulansın mı?"; }
        }
    }
    public sealed class OwnedPowerPlan
    {
        public string Scheme { get; set; }
        public string Mode { get; set; }
        public bool Created { get; set; }
    }
    internal interface IPowerBackend
    {
        Guid Active();
        void Activate(Guid scheme);
        void Duplicate(Guid source, Guid destination);
        void Rename(Guid scheme, string name);
        void Delete(Guid scheme);
        bool Exists(Guid scheme);
        uint Read(Guid scheme, Guid setting, bool ac);
        void Write(Guid scheme, Guid setting, bool ac, uint value);
    }
    internal sealed class WindowsPowerBackend : IPowerBackend
    {
        internal static readonly Guid Processor = new Guid("54533251-82be-4824-96c1-47b60b740d00");
        [DllImport("powrprof.dll")] private static extern uint PowerDuplicateScheme(IntPtr root, ref Guid source, ref IntPtr destination);
        [DllImport("powrprof.dll")] private static extern uint PowerDeleteScheme(IntPtr root, ref Guid scheme);
        [DllImport("powrprof.dll")] private static extern uint PowerWriteFriendlyName(IntPtr root, ref Guid scheme, IntPtr subgroup, IntPtr setting, byte[] value, uint size);
        [DllImport("powrprof.dll")] private static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr subgroup, uint flags, uint index, byte[] buffer, ref uint size);
        [DllImport("powrprof.dll")] private static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid subgroup, ref Guid setting, out uint value);
        [DllImport("powrprof.dll")] private static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid scheme, ref Guid subgroup, ref Guid setting, out uint value);
        [DllImport("powrprof.dll")] private static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid subgroup, ref Guid setting, uint value);
        [DllImport("powrprof.dll")] private static extern uint PowerWriteDCValueIndex(IntPtr root, ref Guid scheme, ref Guid subgroup, ref Guid setting, uint value);
        public Guid Active() { return Optimizer.ActivePower(); }
        public void Activate(Guid scheme) { Check(Native.PowerSetActiveScheme(IntPtr.Zero, ref scheme), "Güç planı etkinleştirme"); if (Active() != scheme) throw new InvalidOperationException("Windows seçilen güç planını etkinleştirmedi."); }
        public void Duplicate(Guid source, Guid destination)
        {
            IntPtr owned = Marshal.AllocHGlobal(16), pointer = owned;
            try { Marshal.StructureToPtr(destination, owned, false); Check(PowerDuplicateScheme(IntPtr.Zero, ref source, ref pointer), "Geçici plan oluşturma"); }
            finally { if (pointer != owned && pointer != IntPtr.Zero) Native.LocalFree(pointer); Marshal.FreeHGlobal(owned); }
        }
        public void Rename(Guid scheme, string name) { byte[] text = Encoding.Unicode.GetBytes(name + "\0"); Check(PowerWriteFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, text, (uint)text.Length), "Plan adlandırma"); }
        public void Delete(Guid scheme) { Check(PowerDeleteScheme(IntPtr.Zero, ref scheme), "Geçici plan temizleme"); }
        public bool Exists(Guid scheme)
        {
            for (uint index = 0; ; index++)
            {
                byte[] bytes = new byte[16]; uint length = 16;
                uint error = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16, index, bytes, ref length);
                if (error == 259) return false;
                Check(error, "Güç planı listesi");
                if (length != 16) throw new InvalidDataException("Windows geçersiz güç planı kimliği döndürdü.");
                if (new Guid(bytes) == scheme) return true;
            }
        }
        public uint Read(Guid scheme, Guid setting, bool ac)
        {
            Guid group = Processor; uint value;
            uint code = ac ? PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, out value) : PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, out value);
            Check(code, "İşlemci güç ayarı okuma"); return value;
        }
        public void Write(Guid scheme, Guid setting, bool ac, uint value)
        {
            Guid group = Processor;
            Check(ac ? PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, value) : PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, value), "İşlemci güç ayarı yazma");
        }
        private static void Check(uint error, string operation)
        {
            if (error != 0) throw new InvalidOperationException(operation + " başarısız. Windows hata kodu: " + error + (error == 5 ? ". Erişim reddedildi; istersen uygulamayı kendi seçiminle yönetici olarak aç." : ". Bu özellik cihazda veya Windows ilkesinde desteklenmiyor olabilir."));
        }
    }
    internal sealed partial class Optimizer
    {
        public string ActiveMode
        {
            get { string active = power.Active().ToString(); var plan = journal.OwnedPowerPlans.FirstOrDefault(x => x.Created && x.Scheme == active); return plan == null ? "Windows / üretici ayarları" : plan.Mode; }
        }
        internal static List<string> ConfigureProfile(IPowerBackend backend, Guid scheme, ProfileSpec spec)
        {
            var notes = new List<string>();
            foreach (var setting in spec.Settings)
            {
                try { backend.Read(scheme, setting.Id, true); backend.Read(scheme, setting.Id, false); }
                catch (InvalidOperationException e) { if (setting.Required) throw; notes.Add(setting.Name + " okunamadı; bu ayar değiştirilmedi: " + e.Message); continue; }
                backend.Write(scheme, setting.Id, true, setting.Ac); backend.Write(scheme, setting.Id, false, setting.Dc);
                if (backend.Read(scheme, setting.Id, true) != setting.Ac || backend.Read(scheme, setting.Id, false) != setting.Dc) throw new InvalidOperationException(setting.Name + " yazıldıktan sonra doğrulanamadı.");
            }
            return notes;
        }
        public List<string> ApplyMode(PerformanceMode mode)
        {
            ProfileSpec spec = ProfileSpec.For(mode);
            Guid before = power.Active(), target = Guid.NewGuid();
            string original = journal.OriginalPower, applied = journal.AppliedPower;
            var candidate = new OwnedPowerPlan { Scheme = target.ToString(), Mode = spec.Name };
            if (journal.OriginalPower == null) journal.OriginalPower = before.ToString();
            journal.OwnedPowerPlans.Add(candidate);
            try
            {
                Save();
                power.Duplicate(before, target); candidate.Created = true; Save();
                power.Rename(target, "ThermalPilot · " + spec.Name + " (geçici)");
                List<string> notes = ConfigureProfile(power, target, spec);
                journal.AppliedPower = target.ToString(); Save();
                power.Activate(target);
                Save();
                CleanupPowerPlans(notes);
                Save(); return notes;
            }
            catch (Exception failure)
            {
                var errors = new List<string>();
                try { if (power.Active() == target) power.Activate(before); } catch (Exception e) { errors.Add("Önceki plana dönüş: " + e.Message); }
                try
                {
                    if (candidate.Created && power.Active() != target && power.Exists(target)) power.Delete(target);
                    if (!candidate.Created || !power.Exists(target)) journal.OwnedPowerPlans.Remove(candidate);
                }
                catch (Exception e) { errors.Add("Geçici plan temizliği: " + e.Message); }
                if (!journal.OwnedPowerPlans.Contains(candidate)) { journal.OriginalPower = original; journal.AppliedPower = applied; }
                Save();
                throw new InvalidOperationException(spec.Name + " modu uygulanamadı. " + failure.Message + (errors.Count == 0 ? " Önceki ayarlar korundu." : "\n" + string.Join("\n", errors) + "\nGeri alma kayıtları korundu."), failure);
            }
        }
        private void CleanupPowerPlans(List<string> errors)
        {
            Guid active = power.Active();
            foreach (var plan in journal.OwnedPowerPlans.ToArray())
            {
                try
                {
                    Guid id = Guid.Parse(plan.Scheme);
                    if (!plan.Created) { journal.OwnedPowerPlans.Remove(plan); continue; }
                    if (id == active) continue;
                    if (power.Exists(id)) power.Delete(id);
                    journal.OwnedPowerPlans.Remove(plan);
                }
                catch (Exception e) { errors.Add("Geçici plan: " + e.Message); }
            }
        }
    }
}
