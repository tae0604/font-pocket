using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace FontPocket {
    static class ManagementTests {
        static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
        static ManagedFont Font(string path, FontMetadata metadata, bool machine) {
            var font = new ManagedFont { Path = path, Name = metadata.Display, Metadata = metadata, Machine = machine };
            if (!machine) font.UserEntries.Add(new FontRegistration { Name = "entry-" + Path.GetFileName(path), Data = path, Kind = RegistryValueKind.String });
            return font;
        }
        public static void Run(string root, List<string> report) {
            string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            string a = Path.Combine(root, "dup-a.ttf"), b = Path.Combine(root, "dup-b.ttf"), c = Path.Combine(root, "other.otf");
            File.Copy(source, a, true); File.Copy(source, b, true);
            var metadata = FontMetadata.Read(source);
            Check(metadata.Faces.All(x => !String.IsNullOrWhiteSpace(x.Version)), "Font version metadata missing");
            var first = Font(a, metadata, false); var second = Font(b, metadata, false); var system = Font(source, metadata, true);
            var fonts = new List<ManagedFont> { first, second, system };
            FontLibrary.FindDuplicates(fonts, Engine.Hash, () => false, (n, t) => {});
            var cleanup = FontLibrary.ExactRemovalCandidates(fonts);
            Check(cleanup.Count == 2 && cleanup.Contains(first) && cleanup.Contains(second) && !cleanup.Contains(system), "Duplicate cleanup did not preserve protected copy");
            Check(FontLibrary.ExactRemovalCandidates(new List<ManagedFont> { first, second }).Count == 1, "User duplicates did not retain exactly one copy");
            var exactReview = DuplicateReview.Groups(fonts, false);
            Check(exactReview.Count == 1 && exactReview[0].Exact && exactReview[0].Selected && exactReview[0].Keeper == system && exactReview[0].Removals.Count == 2, "Review plan did not retain protected duplicate");
            var otherFormat = Font(c, metadata, false);
            FontLibrary.FindDuplicates(new List<ManagedFont> { first, otherFormat }, p => p == a ? "different-a" : "different-c", () => false, (n, t) => {});
            Check(first.NameConflict && otherFormat.NameConflict && !first.ExactDuplicate && FontLibrary.ExactRemovalCandidates(new List<ManagedFont> { first, otherFormat }).Count == 0, "Different font data selected for automatic deletion");
            var nameReview = DuplicateReview.Groups(new List<ManagedFont> { first, otherFormat }, false);
            Check(nameReview.Count == 1 && !nameReview[0].Exact && !nameReview[0].Selected && nameReview[0].Keeper == first, "Different formats were preselected for removal");
            nameReview = DuplicateReview.Groups(new List<ManagedFont> { first, otherFormat }, true);
            Check(nameReview[0].Keeper == otherFormat, "OTF keeper preference ignored");
            nameReview[0].Selected = true;
            Check(nameReview[0].Removals.Count == 1 && nameReview[0].Removals[0] == first && !nameReview[0].Removals.Contains(nameReview[0].Keeper), "Keeper included in cleanup removal plan");
            var vfMetadata = new FontMetadata { Faces = new List<Face> { new Face { Family = metadata.Faces[0].Family, Style = metadata.Faces[0].Style, Variable = true, Version = "Version 1.0" } } };
            var variable = Font(c, vfMetadata, false);
            FontLibrary.FindDuplicates(new List<ManagedFont> { first, variable }, p => p, () => false, (n, t) => {});
            Check(!first.NameConflict && !variable.NameConflict, "Static and variable incorrectly marked as same face");
            FontLibrary.FindDuplicates(new List<ManagedFont> { first, otherFormat }, p => { if (p == c) throw new IOException("unreadable"); return "a"; }, () => false, (n, t) => {});
            Check(otherFormat.HashError != null && FontLibrary.ExactRemovalCandidates(new List<ManagedFont> { first, otherFormat }).Count == 0, "Unreadable file included in automatic cleanup");
            Check(!FontLibrary.InDirectory(Path.Combine(root, "fonts-other", "a.ttf"), Path.Combine(root, "fonts")), "Directory prefix collision allowed");
            report.Add("PASS: exact duplicate cleanup and review preserve the chosen keeper and protected fonts; different formats require explicit group selection; static and variable stay separate");

            var success = new FakeStore(first); string result = FontRemoval.Remove(first, success);
            Check(result == "삭제 완료" && success.Entries.Count == 0 && success.FileDeleteCalls == 1 && success.UnloadCalls == 1, "Removal did not complete in expected order");
            var protectedStore = new FakeStore(system); bool blocked = false;
            try { FontRemoval.Remove(system, protectedStore); } catch (InvalidOperationException) { blocked = true; }
            Check(blocked && protectedStore.UnloadCalls == 0 && protectedStore.FileDeleteCalls == 0, "Protected font removal mutated state");
            var windowsUser = Font(source, metadata, false); var windowsStore = new FakeStore(windowsUser); blocked = false;
            try { FontRemoval.Remove(windowsUser, windowsStore); } catch (InvalidOperationException) { blocked = true; }
            Check(blocked && windowsStore.UnloadCalls == 0, "HKCU entry pointing at Windows font was not protected");
            var changed = new FakeStore(first); changed.Entries[first.UserEntries[0].Name].Data = "changed-path.ttf"; blocked = false;
            try { FontRemoval.Remove(first, changed); } catch (IOException) { blocked = true; }
            Check(blocked && changed.UnloadCalls == 0, "Stale font registration was deleted");
            var twoEntries = Font(a, metadata, false); twoEntries.UserEntries.Add(new FontRegistration { Name = "second-name", Data = a, Kind = RegistryValueKind.String });
            var rollback = new FakeStore(twoEntries) { FailAtDelete = 2 }; blocked = false;
            try { FontRemoval.Remove(twoEntries, rollback); } catch (IOException) { blocked = true; }
            Check(blocked && rollback.Entries.Count == 2 && rollback.ReloadCalls == 1 && rollback.FileDeleteCalls == 0, "Partial failure did not restore registrations");
            var locked = new FakeStore(first) { FailFileDelete = true }; result = FontRemoval.Remove(first, locked);
            Check(locked.Entries.Count == 0 && result.Contains("등록 해제"), "Locked file did not preserve successful unregistration result");
            report.Add("PASS: removal engine protects system fonts and changed registrations, rolls back partial failures, and reports locked files (isolated backend; no installed font removed)");
            using (var preview = new PreviewFont(source, 24)) using (var bitmap = new Bitmap(320, 70)) using (var g = Graphics.FromImage(bitmap)) {
                g.Clear(Color.White); g.DrawString("Font Pocket 012345", preview.Font, Brushes.Black, PointF.Empty);
                bitmap.Save(Path.Combine(root, "font-preview-test.png"));
                Check(preview.Font != null, "Memory font preview unavailable");
            }
            report.Add("PASS: actual font preview loads and renders from memory without changing installed fonts");
        }
        class FakeStore : IFontRemovalStore {
            ManagedFont font;
            public Dictionary<string, FontRegistration> Entries = new Dictionary<string, FontRegistration>();
            public int UnloadCalls, ReloadCalls, FileDeleteCalls, FailAtDelete, DeleteCalls;
            public bool FailFileDelete;
            public FakeStore(ManagedFont value) { font = value; foreach (var e in value.UserEntries) Entries[e.Name] = Copy(e); }
            static FontRegistration Copy(FontRegistration e) { return new FontRegistration { Name = e.Name, Data = e.Data, Kind = e.Kind }; }
            public ManagedFont Current(string path) { return new ManagedFont { Path = font.Path, Machine = font.Machine, UserEntries = Entries.Values.Select(Copy).ToList() }; }
            public void Unload(string path) { UnloadCalls++; }
            public void Reload(string path) { ReloadCalls++; }
            public void DeleteEntry(FontRegistration entry) { DeleteCalls++; if (DeleteCalls == FailAtDelete) throw new IOException("injected registration failure"); Entries.Remove(entry.Name); }
            public void RestoreEntry(FontRegistration entry) { Entries[entry.Name] = Copy(entry); }
            public bool DeleteOwnedFile(string path) { Check(Entries.Count == 0, "File deletion occurred before registration removal"); FileDeleteCalls++; if (FailFileDelete) throw new IOException("file locked"); return true; }
        }
    }
}
