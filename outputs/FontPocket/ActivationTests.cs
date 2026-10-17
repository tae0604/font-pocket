using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace FontPocket {
    static class ActivationTests {
        static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        static ManagedFont Font(string path) { return new ManagedFont { Path = path, Name = "Test font", UserEntries = new List<FontRegistration> { new FontRegistration { Name = "font-one", Data = path, Kind = RegistryValueKind.String }, new FontRegistration { Name = "font-two", Data = path, Kind = RegistryValueKind.ExpandString } } }; }
        public static void Run(string root, List<string> report) {
            string original = Path.Combine(root, "activation-original.ttf");
            File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"), original, true);
            var font = Font(original); var store = new MemoryStore(font);
            var repo = new ActivationRepository(Path.Combine(root, "disabled-state"), Path.Combine(root, "fake-user-fonts"));
            var disabled = FontActivation.Disable(font, store, repo);
            Assert(store.Entries.Count == 0 && store.PhysicalDeleteCalls == 0 && File.Exists(original), "Disable deleted original file or left registrations");
            var afterRestart = new ActivationRepository(repo.Root, repo.UserFontDirectory).ReadAll(s => { throw new Exception(s); });
            Assert(afterRestart.Count == 1 && afterRestart[0].Stage == "Disabled" && afterRestart[0].Entries[1].Kind == RegistryValueKind.ExpandString && Engine.Hash(repo.FontFile(afterRestart[0])) == Engine.Hash(original), "Disabled state did not persist across repository reload");
            FontActivation.Enable(afterRestart[0], store, repo);
            Assert(store.Entries.Count == 2 && store.Entries["font-two"].Kind == RegistryValueKind.ExpandString && repo.ReadAll(s => {}).Count == 0 && File.Exists(original), "Enable did not restore original registrations and retained file");
            report.Add("PASS: disable preserves original bytes and durable registration snapshot; enable restores both registry value kinds (isolated backend)");

            store = new MemoryStore(font); disabled = FontActivation.Disable(font, store, repo);
            File.Delete(original);
            string restored = FontActivation.Enable(disabled, store, repo);
            Assert(FontLibrary.InDirectory(restored, repo.UserFontDirectory) && File.Exists(restored) && store.Entries.Values.All(x => x.Data == restored), "Missing original was not restored to the user-font directory");
            File.Copy(restored, original, true);
            report.Add("PASS: activation recovers a missing original from the retained copy into the configured user-font directory");

            store = new MemoryStore(font); disabled = FontActivation.Disable(font, store, repo);
            store.Entries["font-one"] = new FontRegistration { Name = "font-one", Data = "another-font.ttf", Kind = RegistryValueKind.String };
            int loads = store.LoadCalls; bool rejected = false;
            try { FontActivation.Enable(disabled, store, repo); } catch (IOException) { rejected = true; }
            Assert(rejected && store.LoadCalls == loads && store.Entries["font-one"].Data == "another-font.ttf" && repo.ReadAll(s => {}).Count == 1, "Activation overwrote a conflicting registry name");
            store.Entries.Clear(); store.FailRestoreAt = store.RestoreCalls + 2; rejected = false;
            try { FontActivation.Enable(disabled, store, repo); } catch (IOException) { rejected = true; }
            Assert(rejected && store.Entries.Count == 0 && repo.ReadAll(s => {}).Single().Stage == "Disabled" && File.Exists(repo.FontFile(disabled)), "Partial enable did not roll back while retaining the recovery copy");
            store.FailRestoreAt = 0; FontActivation.Enable(disabled, store, repo);
            report.Add("PASS: conflicting activation is blocked; partial enable failure rolls back and keeps the disabled recovery state");

            store = new MemoryStore(font); disabled = FontActivation.Disable(font, store, repo);
            File.WriteAllText(repo.FontFile(disabled), "tampered"); rejected = false;
            try { FontActivation.Enable(disabled, store, repo); } catch (IOException) { rejected = true; }
            Assert(rejected && store.Entries.Count == 0, "Modified disabled font was enabled"); repo.Remove(disabled);
            var protectedFont = Font(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"));
            store = new MemoryStore(protectedFont); rejected = false;
            try { FontActivation.Disable(protectedFont, store, repo); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && store.Entries.Count == 2 && store.UnloadCalls == 0, "System font disable was allowed");
            rejected = false;
            try { repo.FontFile(new DisabledFont { Id = new string('a', 32), FileName = "../../outside.ttf" }); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "Disabled record escaped its retained-file directory");
            report.Add("PASS: modified retained fonts, system-font activation changes and out-of-directory state paths are rejected");
        }
        class MemoryStore : IFontActivationStore {
            public Dictionary<string, FontRegistration> Entries = new Dictionary<string, FontRegistration>();
            public int PhysicalDeleteCalls, LoadCalls, UnloadCalls, RestoreCalls, FailRestoreAt;
            public MemoryStore(ManagedFont font) { foreach (var e in font.UserEntries) Entries[e.Name] = Copy(e); }
            static FontRegistration Copy(FontRegistration e) { return new FontRegistration { Name = e.Name, Data = e.Data, Kind = e.Kind }; }
            public ManagedFont Current(string path) {
                var entries = Entries.Values.Where(e => e.Data == path).Select(Copy).ToList();
                return entries.Count == 0 ? null : new ManagedFont { Path = path, UserEntries = entries };
            }
            public FontRegistration ReadEntry(string name) { return Entries.ContainsKey(name) ? Copy(Entries[name]) : null; }
            public void Unload(string path) { UnloadCalls++; }
            public void Reload(string path) { LoadCalls++; }
            public void DeleteEntry(FontRegistration e) { if (Entries.ContainsKey(e.Name) && Entries[e.Name].Data != e.Data) throw new IOException("changed entry"); Entries.Remove(e.Name); }
            public void RestoreEntry(FontRegistration e) { RestoreCalls++; if (RestoreCalls == FailRestoreAt) throw new IOException("injected enable failure"); Entries[e.Name] = Copy(e); }
            public bool DeleteOwnedFile(string path) { PhysicalDeleteCalls++; throw new Exception("Disabling must not delete files"); }
        }
    }
}
