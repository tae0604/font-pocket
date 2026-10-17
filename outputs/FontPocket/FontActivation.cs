using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace FontPocket {
    public class DisabledFont {
        public string Id, FileName, OriginalPath, Hash, Name, Stage;
        public List<FontRegistration> Entries = new List<FontRegistration>();
    }
    public class ActivationRepository {
        public string Root, UserFontDirectory;
        public ActivationRepository(string root, string userFontDirectory) { Root = Path.GetFullPath(root); UserFontDirectory = Path.GetFullPath(userFontDirectory); }
        public static ActivationRepository Default { get { return new ActivationRepository(Path.Combine(DataPaths.Root, "Disabled"), Engine.UserDir); } }
        string Manifest(DisabledFont record) {
            if (record == null || !Regex.IsMatch(record.Id ?? "", "^[a-f0-9]{32}$")) throw new InvalidDataException("비활성 폰트 식별자가 올바르지 않습니다.");
            return Path.Combine(Root, record.Id + ".json");
        }
        public string FontFile(DisabledFont record) {
            Manifest(record);
            if (String.IsNullOrWhiteSpace(record.FileName) || Path.GetFileName(record.FileName) != record.FileName || !record.FileName.StartsWith(record.Id + ".", StringComparison.Ordinal) || !Engine.IsFont(record.FileName))
                throw new InvalidDataException("비활성 폰트 파일 경로가 올바르지 않습니다.");
            return Path.Combine(Root, record.FileName);
        }
        public void Save(DisabledFont record) {
            string path = Manifest(record); FontFile(record); Directory.CreateDirectory(Root);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temp, new JavaScriptSerializer().Serialize(record), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public List<DisabledFont> ReadAll(Action<string> warning) {
            var result = new List<DisabledFont>();
            if (!Directory.Exists(Root)) return result;
            foreach (string path in Directory.GetFiles(Root, "*.json")) {
                try {
                    if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("상태 파일이 너무 큽니다.");
                    var record = new JavaScriptSerializer().Deserialize<DisabledFont>(File.ReadAllText(path));
                    if (!String.Equals(Manifest(record), path, StringComparison.OrdinalIgnoreCase) || record.Entries == null || record.Entries.Count == 0 || !Regex.IsMatch(record.Hash ?? "", "^[a-f0-9]{64}$")) throw new InvalidDataException("비활성 폰트 상태가 올바르지 않습니다.");
                    if (!File.Exists(FontFile(record))) throw new FileNotFoundException("보관된 비활성 폰트 파일이 없습니다.");
                    result.Add(record);
                } catch (Exception ex) { warning(Path.GetFileName(path) + ": " + ex.Message); }
            }
            return result;
        }
        public void Remove(DisabledFont record) {
            string manifest = Manifest(record), font = FontFile(record);
            if (File.Exists(manifest)) File.Delete(manifest);
            try { if (File.Exists(font) && (File.GetAttributes(font) & FileAttributes.ReparsePoint) == 0) File.Delete(font); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    public interface IFontActivationStore : IFontRemovalStore {
        FontRegistration ReadEntry(string name);
    }
    public class WindowsFontActivationStore : WindowsFontRemovalStore, IFontActivationStore {
        public FontRegistration ReadEntry(string name) {
            using (var key = Registry.CurrentUser.OpenSubKey(Engine.RegistryPath)) {
                if (key == null) return null;
                string value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                return value == null ? null : new FontRegistration { Name = name, Data = value, Kind = key.GetValueKind(name) };
            }
        }
    }
    public static class FontActivation {
        class PreserveFiles : IFontRemovalStore {
            IFontActivationStore source;
            public PreserveFiles(IFontActivationStore value) { source = value; }
            public ManagedFont Current(string path) { return source.Current(path); }
            public void Unload(string path) { source.Unload(path); }
            public void Reload(string path) { source.Reload(path); }
            public void DeleteEntry(FontRegistration e) { source.DeleteEntry(e); }
            public void RestoreEntry(FontRegistration e) { source.RestoreEntry(e); }
            public bool DeleteOwnedFile(string path) { return false; }
        }
        public static DisabledFont Disable(ManagedFont font, IFontActivationStore store, ActivationRepository repository) {
            if (font.Disabled || !font.CanRemove) throw new InvalidOperationException("내 계정의 활성 폰트만 비활성화할 수 있습니다.");
            var current = store.Current(font.Path);
            if (current == null || !current.CanRemove) throw new InvalidOperationException("현재 계정에 등록된 폰트가 아닙니다.");
            var record = new DisabledFont { Id = Guid.NewGuid().ToString("N"), OriginalPath = font.Path, Hash = Engine.Hash(font.Path), Name = font.Name, Stage = "PendingDisable",
                Entries = current.UserEntries.Select(e => new FontRegistration { Name = e.Name, Data = e.Data, Kind = e.Kind }).ToList() };
            record.FileName = record.Id + Path.GetExtension(font.Path).ToLowerInvariant();
            Directory.CreateDirectory(repository.Root);
            File.Copy(font.Path, repository.FontFile(record), false);
            if (Engine.Hash(repository.FontFile(record)) != record.Hash) throw new IOException("보관용 폰트의 내용이 원본과 다릅니다.");
            repository.Save(record);
            // The recovery state is durable before any Windows registration is removed.
            FontRemoval.Remove(font, new PreserveFiles(store));
            record.Stage = "Disabled"; repository.Save(record); return record;
        }
        public static string Enable(DisabledFont record, IFontActivationStore store, ActivationRepository repository) {
            string source = repository.FontFile(record);
            if (Engine.Hash(source) != record.Hash) throw new IOException("보관된 폰트의 내용이 변경돼 활성화하지 않았습니다.");
            string target = record.OriginalPath;
            bool originalUsable = !String.IsNullOrWhiteSpace(target) && !FontLibrary.InDirectory(target, repository.Root) && !FontLibrary.InDirectory(target, Environment.GetFolderPath(Environment.SpecialFolder.Fonts)) && File.Exists(target) && Engine.Hash(target) == record.Hash;
            if (!originalUsable) {
                Directory.CreateDirectory(repository.UserFontDirectory);
                target = Path.Combine(repository.UserFontDirectory, "FontPocket-enabled-" + record.Hash + Path.GetExtension(source));
                if (!File.Exists(target)) File.Copy(source, target, false);
                else if (Engine.Hash(target) != record.Hash) throw new IOException("활성화할 위치에 다른 파일이 있습니다.");
            }
            var entries = record.Entries.Select(e => new FontRegistration { Name = e.Name, Data = originalUsable ? e.Data : target, Kind = originalUsable ? e.Kind : RegistryValueKind.String }).ToList();
            var toCreate = new List<FontRegistration>();
            foreach (var e in entries) {
                var old = store.ReadEntry(e.Name);
                if (old != null && (old.Data != e.Data || old.Kind != e.Kind)) throw new IOException("같은 등록 이름이 다른 폰트에 사용 중입니다. 기존 폰트는 변경하지 않았습니다.");
                if (old == null) toCreate.Add(e);
            }
            record.Stage = "PendingEnable"; repository.Save(record);
            var added = new List<FontRegistration>(); bool loaded = false;
            try {
                store.Reload(target); loaded = true;
                foreach (var e in toCreate) { store.RestoreEntry(e); added.Add(e); }
            } catch (Exception ex) {
                var errors = new List<string>();
                foreach (var e in added) { try { store.DeleteEntry(e); } catch (Exception rollback) { errors.Add(rollback.Message); } }
                if (loaded) { try { store.Unload(target); } catch (Exception rollback) { errors.Add(rollback.Message); } }
                record.Stage = "Disabled"; try { repository.Save(record); } catch (Exception rollback) { errors.Add(rollback.Message); }
                if (errors.Count > 0) throw new IOException("활성화 실패. 일부 등록을 되돌리지 못했습니다. 보관 파일은 유지했습니다: " + String.Join("; ", errors.ToArray()), ex);
                throw;
            }
            repository.Remove(record); return target;
        }
    }
}
