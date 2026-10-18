using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FontPocket {
    public class FontRegistration {
        public string Name, Data;
        public RegistryValueKind Kind;
    }
    public class ManagedFont {
        public string Path, Name, Format, Version = "", Hash, HashError, DuplicateLabel = "";
        public FontMetadata Metadata;
        public bool Machine, Checked, ExactDuplicate, NameConflict;
        public bool Disabled;
        public DisabledFont DisabledRecord;
        public long Bytes;
        public DateTime AddedUtc;
        public List<FontRegistration> UserEntries = new List<FontRegistration>();
        public override string ToString() { return Name + (Disabled ? " · 비활성" : "") + (String.IsNullOrWhiteSpace(Version)?"":" · "+Version); }
        public bool CanRemove { get { return (Disabled || UserEntries.Count > 0) && !Machine && !FontLibrary.InDirectory(Path, Environment.GetFolderPath(Environment.SpecialFolder.Fonts)); } }
        public string Scope { get { return CanRemove ? "내 계정" : "시스템 · 보호"; } }
        public bool Matches(string query) { return String.IsNullOrWhiteSpace(query) || (Name + " " + Format + " " + Version + " " + System.IO.Path.GetFileName(Path) + (Disabled ? " 비활성" : " 활성")).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0; }
        public string Identity {
            get {
                if (Metadata == null) return null;
                return String.Join(";", Metadata.Faces.Select(x => Regex.Replace(x.FamilyKey, @"(otf|ttf)$", "", RegexOptions.IgnoreCase) + "|" + x.Style.ToLowerInvariant().Replace(" ", "") + (x.Variable ? "|variable" : "|static")).OrderBy(x => x, StringComparer.Ordinal).ToArray());
            }
        }
    }
    public static class FontLibrary {
        public static bool InDirectory(string path, string directory) {
            try { return !String.IsNullOrWhiteSpace(directory) && System.IO.Path.GetFullPath(path).StartsWith(System.IO.Path.GetFullPath(directory).TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }
        public static List<ManagedFont> Read() {
            var result = new Dictionary<string, ManagedFont>(StringComparer.OrdinalIgnoreCase);
            ReadKey(Registry.CurrentUser, false, Engine.UserDir, result);
            ReadKey(Registry.LocalMachine, true, Environment.GetFolderPath(Environment.SpecialFolder.Fonts), result);
            return result.Values.ToList();
        }
        static void ReadKey(RegistryKey hive, bool machine, string baseDir, Dictionary<string, ManagedFont> result) {
            using (var key = hive.OpenSubKey(Engine.RegistryPath)) {
                if (key == null) return;
                foreach (string name in key.GetValueNames()) {
                    try {
                        string raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                        if (String.IsNullOrWhiteSpace(raw)) continue;
                        string value = Environment.ExpandEnvironmentVariables(raw);
                        string path = System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(value) ? value : System.IO.Path.Combine(baseDir, value));
                        if (!File.Exists(path) || !Engine.IsFont(path)) continue;
                        ManagedFont font;
                        if (!result.TryGetValue(path, out font)) { font = new ManagedFont { Path = path, Name = System.IO.Path.GetFileNameWithoutExtension(path), Format = System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant() }; result[path] = font; }
                        if (machine) font.Machine = true;
                        else font.UserEntries.Add(new FontRegistration { Name = name, Data = raw, Kind = key.GetValueKind(name) });
                    } catch (ArgumentException) { } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }
        public static void ReadMetadata(ManagedFont font) {
            try { font.Metadata = FontMetadata.Read(font.Path); font.Name = font.Metadata.Display; font.Version = String.Join(", ", font.Metadata.Faces.Select(x => x.Version).Where(x => !String.IsNullOrWhiteSpace(x)).Distinct().ToArray()); if (font.Metadata.Variable) font.Format += " · 가변"; } catch { }
            try {var file=new FileInfo(font.Path);font.Bytes = file.Length; font.AddedUtc=file.CreationTimeUtc; } catch { }
        }
        public static void FindDuplicates(List<ManagedFont> fonts, Func<string, string> hash, Func<bool> cancelled, Action<int, int> progress) {
            for (int i = 0; i < fonts.Count; i++) {
                if (cancelled()) throw new OperationCanceledException();
                var font = fonts[i]; font.ExactDuplicate = false; font.NameConflict = false; font.DuplicateLabel = ""; font.Hash = null; font.HashError = null;
                try { font.Hash = hash(font.Path); } catch (Exception ex) { font.HashError = ex.Message; font.DuplicateLabel = "파일 확인 실패"; }
                progress(i + 1, fonts.Count);
            }
            foreach (var group in fonts.Where(x => !String.IsNullOrEmpty(x.Hash)).GroupBy(x => x.Hash).Where(x => x.Count() > 1))
                foreach (var f in group) { f.ExactDuplicate = true; f.DuplicateLabel = "완전 동일 · " + group.Count() + "개"; }
            foreach (var group in fonts.Where(x => x.Identity != null).GroupBy(x => x.Identity).Where(x => x.Count() > 1))
                foreach (var f in group) { f.NameConflict = true; if (!f.ExactDuplicate) f.DuplicateLabel = "이름·스타일 겹침 · " + group.Count() + "개"; }
        }
        public static List<ManagedFont> ExactRemovalCandidates(List<ManagedFont> fonts) {
            var result = new List<ManagedFont>();
            foreach (var group in fonts.Where(x => !String.IsNullOrEmpty(x.Hash)).GroupBy(x => x.Hash).Where(x => x.Count() > 1)) {
                var ordered = group.OrderBy(x => x.CanRemove ? 1 : 0).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
                result.AddRange(ordered.Skip(1).Where(x => x.CanRemove));
            }
            return result;
        }
    }
    public interface IFontRemovalStore {
        ManagedFont Current(string path);
        void Unload(string path);
        void Reload(string path);
        void DeleteEntry(FontRegistration entry);
        void RestoreEntry(FontRegistration entry);
        bool DeleteOwnedFile(string path);
    }
    public static class FontRemoval {
        public static string Remove(ManagedFont font, IFontRemovalStore store) {
            var current = store.Current(font.Path);
            if (current == null || !current.CanRemove) throw new InvalidOperationException("시스템 폰트이거나 현재 계정에 등록되지 않은 폰트입니다.");
            if (font.UserEntries.Count != current.UserEntries.Count || font.UserEntries.Any(x => !current.UserEntries.Any(y => y.Name == x.Name && y.Data == x.Data && y.Kind == x.Kind)))
                throw new IOException("폰트 등록 정보가 변경됐습니다. 새로고침 후 다시 선택하세요.");
            var removed = new List<FontRegistration>();
            store.Unload(current.Path);
            try {
                foreach (var entry in current.UserEntries) { store.DeleteEntry(entry); removed.Add(entry); }
            } catch (Exception ex) {
                var rollbackErrors = new List<string>();
                foreach (var entry in removed) { try { store.RestoreEntry(entry); } catch (Exception rollback) { rollbackErrors.Add(rollback.Message); } }
                try { store.Reload(current.Path); } catch (Exception rollback) { rollbackErrors.Add(rollback.Message); }
                if (rollbackErrors.Count > 0) throw new IOException("삭제 실패. 일부 등록 복원도 실패했습니다. 복구 ZIP을 사용하세요: " + String.Join("; ", rollbackErrors.ToArray()), ex);
                throw;
            }
            bool deleted = false;
            try { deleted = store.DeleteOwnedFile(current.Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return deleted ? "삭제 완료" : "등록 해제 완료 · 원본 보존 / 사용 중인 파일은 남을 수 있음";
        }
    }
    public class WindowsFontRemovalStore : IFontRemovalStore {
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern bool RemoveFontResourceEx(string path, uint flags, IntPtr reserved);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern int AddFontResourceEx(string path, uint flags, IntPtr reserved);
        public ManagedFont Current(string path) { return FontLibrary.Read().FirstOrDefault(x => String.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)); }
        public void Unload(string path) { for (int i = 0; i < 64; i++) if (!RemoveFontResourceEx(path, 0, IntPtr.Zero)) break; }
        public void Reload(string path) { if (AddFontResourceEx(path, 0, IntPtr.Zero) == 0) throw new IOException("폰트 리소스를 다시 불러오지 못했습니다."); }
        public void DeleteEntry(FontRegistration entry) {
            using (var key = Registry.CurrentUser.OpenSubKey(Engine.RegistryPath, true)) {
                if (key == null || !String.Equals(key.GetValue(entry.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string, entry.Data, StringComparison.Ordinal) || key.GetValueKind(entry.Name) != entry.Kind)
                    throw new IOException("등록 정보가 다른 프로그램에서 변경됐습니다.");
                key.DeleteValue(entry.Name, true);
            }
        }
        public void RestoreEntry(FontRegistration entry) {
            using (var key = Registry.CurrentUser.CreateSubKey(Engine.RegistryPath)) {
                object present = key.GetValue(entry.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (present != null && !String.Equals(present as string, entry.Data, StringComparison.Ordinal)) throw new IOException("같은 등록 이름이 이미 사용 중입니다.");
                key.SetValue(entry.Name, entry.Data, entry.Kind);
            }
        }
        public bool DeleteOwnedFile(string path) {
            if (!FontLibrary.InDirectory(path, Engine.UserDir) || FontLibrary.InDirectory(path, Environment.GetFolderPath(Environment.SpecialFolder.Fonts))) return false;
            if (FontLibrary.Read().Any(x => String.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase))) return false;
            string current = System.IO.Path.GetFullPath(path);
            if (current.Substring(System.IO.Path.GetPathRoot(current).Length).Contains(":")) return false;
            while (!String.IsNullOrEmpty(current)) {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
                current = System.IO.Path.GetDirectoryName(current);
            }
            File.Delete(path); return true;
        }
    }
}
