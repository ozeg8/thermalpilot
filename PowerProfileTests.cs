using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace ThermalPilot
{
    internal static class PowerProfileTests
    {
        public static void Register(Action<string, Action> test)
        {
            test("Performance modes create separate plans, persist undo and remove old temporary plans", delegate
            {
                WithDirectory(delegate(string directory)
                {
                    var power = new FakePower(); Guid original = power.Active();
                    var optimizer = new Optimizer(directory, power);
                    optimizer.ApplyMode(PerformanceMode.Quiet);
                    Assert(power.Active() != original && optimizer.ActiveMode == "Sessiz / Serin", "Quiet mode not activated");
                    Guid max = ProfileSpec.For(PerformanceMode.Quiet).Settings[1].Id;
                    Assert(power.Read(power.Active(), max, true) == 70 && power.Read(power.Active(), max, false) == 50, "Quiet limits incorrect");
                    Assert(power.Read(original, max, true) == 100, "Original Windows plan was modified");
                    optimizer.ApplyMode(PerformanceMode.Balanced);
                    optimizer.ApplyMode(PerformanceMode.Performance);
                    Assert(power.PlanCount == 2 && optimizer.ActiveMode == "Performans", "Temporary plans accumulated or active mode incorrect");
                    var reopened = new Optimizer(directory, power);
                    Assert(reopened.HasChanges && reopened.Restore().Count == 0, "Persisted profile could not be restored");
                    Assert(power.Active() == original && power.PlanCount == 1 && !reopened.HasChanges, "Original state not restored");
                });
            });
            test("Failed profile write leaves the previous plan and settings intact", delegate
            {
                WithDirectory(delegate(string directory)
                {
                    var power = new FakePower { FailWriteAt = 3 }; Guid original = power.Active();
                    var optimizer = new Optimizer(directory, power); bool failed = false;
                    try { optimizer.ApplyMode(PerformanceMode.Quiet); } catch (InvalidOperationException) { failed = true; }
                    Assert(failed && power.Active() == original && power.PlanCount == 1 && !optimizer.HasChanges, "Failed configuration changed active plan or leaked undo state");
                });
            });
            test("Failed profile activation cleans the candidate and preserves earlier undo state", delegate
            {
                WithDirectory(delegate(string directory)
                {
                    var power = new FakePower(); var optimizer = new Optimizer(directory, power);
                    optimizer.ApplyMode(PerformanceMode.Balanced); Guid before = power.Active();
                    power.FailActivation = true; bool failed = false;
                    try { optimizer.ApplyMode(PerformanceMode.Performance); } catch (InvalidOperationException) { failed = true; }
                    Assert(failed && power.Active() == before && power.PlanCount == 2 && optimizer.ActiveMode == "Dengeli", "Activation failure damaged prior mode");
                    power.FailActivation = false; Assert(optimizer.Restore().Count == 0 && power.PlanCount == 1, "Prior undo state lost");
                });
            });
            test("User-selected Windows plan survives profile rollback", delegate
            {
                WithDirectory(delegate(string directory)
                {
                    var power = new FakePower(); var optimizer = new Optimizer(directory, power);
                    optimizer.ApplyMode(PerformanceMode.Quiet);
                    Guid chosen = power.AddUserPlan(); power.Activate(chosen);
                    Assert(optimizer.ActiveMode == "Windows / üretici ayarları", "External Windows plan not reflected");
                    Assert(optimizer.Restore().Count == 0 && power.Active() == chosen && power.PlanCount == 2, "External user choice was overwritten");
                });
            });
            test("Optional unsupported profile settings are reported without pretending they were applied", delegate
            {
                WithDirectory(delegate(string directory)
                {
                    var power = new FakePower { NoEpp = true }; var optimizer = new Optimizer(directory, power);
                    var notes = optimizer.ApplyMode(PerformanceMode.Quiet);
                    Assert(notes.Count == 1 && notes[0].Contains("EPP"), "Unsupported EPP was hidden");
                    Assert(optimizer.Restore().Count == 0, "Partial capability mode did not restore");
                });
            });
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void WithDirectory(Action<string> action)
        {
            string directory = Path.Combine(Path.GetTempPath(), "ThermalPilot-mode-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            try { action(directory); }
            finally
            {
                string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(directory);
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("ThermalPilot-mode-test-")) throw new InvalidOperationException("Unexpected test cleanup path");
                Directory.Delete(target, true);
            }
        }
        private sealed class FakePower : IPowerBackend
        {
            private readonly Dictionary<Guid, Dictionary<string, uint>> plans = new Dictionary<Guid, Dictionary<string, uint>>();
            private Guid active;
            private int writes;
            public bool FailActivation, NoEpp;
            public int FailWriteAt;
            public int PlanCount { get { return plans.Count; } }
            public FakePower() { active = AddUserPlan(); }
            public Guid AddUserPlan()
            {
                Guid id = Guid.NewGuid(); var settings = new Dictionary<string, uint>();
                foreach (var s in ProfileSpec.For(PerformanceMode.Balanced).Settings) { settings[s.Id + "/ac"] = s.Ac; settings[s.Id + "/dc"] = s.Dc; }
                plans[id] = settings; return id;
            }
            public Guid Active() { return active; }
            public void Activate(Guid id) { if (FailActivation) throw new InvalidOperationException("Injected activation failure"); if (!plans.ContainsKey(id)) throw new InvalidOperationException("Unknown plan"); active = id; }
            public void Duplicate(Guid source, Guid target) { plans.Add(target, new Dictionary<string, uint>(plans[source])); }
            public void Rename(Guid id, string name) { }
            public void Delete(Guid id) { if (id == active) throw new InvalidOperationException("Cannot delete active plan"); plans.Remove(id); }
            public bool Exists(Guid id) { return plans.ContainsKey(id); }
            public uint Read(Guid scheme, Guid setting, bool ac)
            {
                if (NoEpp && setting == new Guid("36687f9e-e3a5-4dbf-b1dc-15eb381c6863")) throw new InvalidOperationException("Unsupported EPP");
                return plans[scheme][setting + (ac ? "/ac" : "/dc")];
            }
            public void Write(Guid scheme, Guid setting, bool ac, uint value)
            {
                if (++writes == FailWriteAt) throw new InvalidOperationException("Injected write failure");
                plans[scheme][setting + (ac ? "/ac" : "/dc")] = value;
            }
        }
        public static int RunNativeReport(string path)
        {
            var power = new WindowsPowerBackend(); Guid original = power.Active(), temporary = Guid.NewGuid(); bool created = false;
            var results = new List<object>(); var errors = new List<string>();
            try
            {
                power.Duplicate(original, temporary); created = true;
                power.Rename(temporary, "ThermalPilot inactive profile verification");
                foreach (PerformanceMode mode in Enum.GetValues(typeof(PerformanceMode)))
                {
                    var spec = ProfileSpec.For(mode); var notes = Optimizer.ConfigureProfile(power, temporary, spec);
                    results.Add(new { mode = spec.Name, notes = notes, settings = spec.Settings.Select(s => new { name = s.Name, ac = TryRead(power, temporary, s.Id, true), dc = TryRead(power, temporary, s.Id, false) }).ToArray() });
                }
            }
            catch (Exception e) { errors.Add(e.Message); }
            finally
            {
                if (created)
                    try { if (power.Active() == temporary) errors.Add("Temporary test plan became active externally; it was preserved."); else power.Delete(temporary); }
                    catch (Exception e) { errors.Add("Cleanup: " + e.Message); }
            }
            bool unchanged = power.Active() == original;
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(new { passed = errors.Count == 0 && unchanged, active_plan_unchanged = unchanged, original_scheme = original, temporary_scheme = temporary, activation_test = "NOT_PERFORMED: inactive copy only", results = results, errors = errors }), new UTF8Encoding(false));
            return errors.Count == 0 && unchanged ? 0 : 2;
        }
        private static uint? TryRead(IPowerBackend p, Guid scheme, Guid setting, bool ac) { try { return p.Read(scheme, setting, ac); } catch { return null; } }
    }
}
