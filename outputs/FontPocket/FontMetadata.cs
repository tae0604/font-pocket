using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FontPocket {
    public class Face {
        public string Family, Style, Version;
        public int Weight = 400;
        public bool Variable;
        public string FamilyKey { get { return Normalize(Variable ? Regex.Replace(Family, @"[\s_-]*(variable|vf)$", "", RegexOptions.IgnoreCase) : Family); } }
        public string Key { get { return FamilyKey + "|" + Normalize(Style) + (Variable ? "|variable" : "|static"); } }
        static string Normalize(string s) { return Regex.Replace(s.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\s_-]+", ""); }
    }
    public class FontMetadata {
        public List<Face> Faces = new List<Face>();
        public string Display { get { return String.Join(", ", Faces.Select(f => f.Family + " " + f.Style + (f.Variable ? " (가변)" : "")).ToArray()); } }
        public bool Variable { get { return Faces.Any(x => x.Variable); } }
        public static FontMetadata Read(string path) {
            using (var s = File.OpenRead(path)) {
                var reader = new Sfnt(s); var result = new FontMetadata();
                if (reader.Tag(0) == "ttcf") {
                    uint count = reader.U32(8);
                    if (count == 0 || count > 1024) throw new InvalidDataException("TTC 글꼴 개수가 올바르지 않습니다.");
                    for (int i = 0; i < count; i++) result.Faces.Add(reader.Face(reader.U32(12 + i * 4)));
                } else result.Faces.Add(reader.Face(0));
                return result;
            }
        }
        class Sfnt {
            Stream s; public Sfnt(Stream stream) { s = stream; }
            byte[] Bytes(long p, int count) {
                if (p < 0 || count < 0 || p > s.Length - count) throw new InvalidDataException("손상된 폰트 테이블입니다.");
                s.Position = p; byte[] b = new byte[count]; int n = 0, r;
                while (n < count && (r = s.Read(b, n, count - n)) > 0) n += r;
                if (n != count) throw new EndOfStreamException(); return b;
            }
            public ushort U16(long p) { byte[] b = Bytes(p, 2); return (ushort)(b[0] * 256 + b[1]); }
            public uint U32(long p) { byte[] b = Bytes(p, 4); return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3]; }
            public string Tag(long p) { return Encoding.ASCII.GetString(Bytes(p, 4)); }
            public Face Face(long offset) {
                uint magic = U32(offset);
                if (magic != 0x00010000 && magic != 0x4f54544f && magic != 0x74727565) throw new InvalidDataException("지원하지 않는 폰트 헤더입니다.");
                int tables = U16(offset + 4); if (tables == 0 || tables > 4096) throw new InvalidDataException("손상된 폰트입니다.");
                long name = -1, length = 0; bool variable = false; int weight=400;
                for (int i = 0; i < tables; i++) {
                    long record = offset + 12 + i * 16; string tag = Tag(record);
                    long p = U32(record + 8), size = U32(record + 12);
                    if (p > s.Length - size) throw new InvalidDataException("폰트 테이블이 파일 범위를 벗어났습니다.");
                    if (tag == "name") { name = p; length = size; }
                    if (tag == "fvar") variable = true;
                    if (tag == "OS/2" && size >= 6) {int value=U16(p+4);if(value>=1 && value<=1000) weight=value;}
                }
                if (name < 0 || length < 6) throw new InvalidDataException("글꼴 이름 정보가 없습니다.");
                int count = U16(name + 2), storage = U16(name + 4);
                if (6L + count * 12L > length || storage > length) throw new InvalidDataException("손상된 이름 테이블입니다.");
                var values = new Dictionary<int, string>(); var scores = new Dictionary<int, int>();
                for (int i = 0; i < count; i++) {
                    long p = name + 6 + i * 12; int platform = U16(p), encoding = U16(p + 2), lang = U16(p + 4), id = U16(p + 6), size = U16(p + 8), start = U16(p + 10);
                    if (id != 1 && id != 2 && id != 5 && id != 16 && id != 17) continue;
                    if (platform != 0 && !(platform == 3 && (encoding == 0 || encoding == 1 || encoding == 10))) continue;
                    if (storage + (long)start + size > length || size % 2 != 0) continue;
                    int score = (lang == 0x409 ? 100 : lang == 0 ? 60 : 10) + (platform == 3 ? 3 : 0);
                    if (scores.ContainsKey(id) && scores[id] >= score) continue;
                    string value = Encoding.BigEndianUnicode.GetString(Bytes(name + storage + start, size)).Trim('\0', ' ');
                    if (String.IsNullOrWhiteSpace(value)) continue;
                    values[id] = value; scores[id] = score;
                }
                string family = values.ContainsKey(16) ? values[16] : values.ContainsKey(1) ? values[1] : null;
                string style = values.ContainsKey(17) ? values[17] : values.ContainsKey(2) ? values[2] : null;
                if (family == null || style == null) throw new InvalidDataException("글꼴 이름 / 스타일을 읽을 수 없습니다. 자동 설치에서 제외합니다.");
                return new Face { Family = family, Style = style, Weight=weight, Variable = variable, Version = values.ContainsKey(5) ? values[5] : "" };
            }
        }
    }
    public enum FontFormat { TtfOnly, OtfOnly, Both }
    public static class Selection {
        public static string Extension(string path) { return Path.GetExtension(path).ToLowerInvariant(); }
        public static bool Allowed(string path, FontFormat mode, bool includeTtc) {
            string ext = Extension(path);
            return ext == ".ttc" ? includeTtc : ext == ".ttf" ? mode != FontFormat.OtfOnly : ext == ".otf" && mode != FontFormat.TtfOnly;
        }
        public static string InstallKey(Face face, string path) { return face.Key + "|" + Extension(path); }
        public static bool CanInstall(FontItem item, FontFormat mode, bool ttc) { return Allowed(item.Path, mode, ttc) || item.FormatOverride; }
        public static bool AcceptOverride(FontItem item, FontFormat mode, bool ttc, Func<string, bool> confirm) {
            if (CanInstall(item, mode, ttc)) return true;
            string format = Extension(item.Path).TrimStart('.').ToUpperInvariant();
            if (!confirm("이 폰트는 " + format + " 형식입니다.\r\n현재 형식 설정과 관계없이 이 폰트도 선택할까요?\r\n\r\n" + item.Name)) return false;
            item.FormatOverride = true; return true;
        }
        public static int LinkFormats(DownloadLink link) {
            string text = Uri.UnescapeDataString(link.Title + " " + new Uri(link.Url).PathAndQuery).ToLowerInvariant();
            return (text.Contains("ttf") ? 1 : 0) | (text.Contains("otf") ? 2 : 0) | (text.Contains("ttc") ? 4 : 0);
        }
        public static bool LinkAllowed(DownloadLink link, FontFormat mode, bool ttc) {
            int formats = LinkFormats(link), mask = (mode == FontFormat.TtfOnly ? 1 : mode == FontFormat.OtfOnly ? 2 : 3) | (ttc ? 4 : 0);
            return formats == 0 || (formats & mask) != 0;
        }
        public static HashSet<string> DefaultDownloads(List<DownloadLink> links, FontFormat mode, bool ttc) {
            var eligible = links.Where(x => LinkAllowed(x, mode, ttc)).ToList();
            eligible = eligible.Where(x => x.Variant != FontVariant.Variable || !eligible.Any(other => other.Variant != FontVariant.Variable && (LinkFormats(other) & LinkFormats(x)) != 0)).ToList();
            var explicitMatches = eligible.Where(x => LinkFormats(x) != 0).Select(x => x.Url).ToList();
            if (explicitMatches.Count > 0) return new HashSet<string>(explicitMatches);
            var general = eligible.FirstOrDefault(x => LinkFormats(x) == 0);
            return new HashSet<string>(general == null ? new string[0] : new string[] { general.Url });
        }
        public static void Recommend(List<FontItem> items, FontFormat mode, bool includeTtc) {
            var covered = new HashSet<string>();
            var coveredFaces = new HashSet<string>();
            // Variable files can expose additional axes: retain them as an explicit alternative.
            foreach (var item in items) { item.Recommended = false; item.Reason = "정보 확인 필요"; }
            var staticFamilies = new HashSet<string>(items.Where(x => (Allowed(x.Path, mode, includeTtc) || x.FormatOverride) && x.Metadata != null && !x.Metadata.Variable).SelectMany(x => x.Metadata.Faces.Select(f => f.FamilyKey + "|" + Extension(x.Path))));
            foreach (var item in items.OrderBy(x => Rank(x)).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) {
                if (item.Metadata == null) continue;
                if (!Allowed(item.Path, mode, includeTtc) && !item.FormatOverride) { item.Reason = Extension(item.Path) == ".ttc" ? "TTC 제외 · 체크하면 개별 선택 가능" : "다른 형식 · 체크하면 개별 선택 가능"; continue; }
                var faces = item.Metadata.Faces;
                if (item.Metadata.Variable && faces.Any(x => staticFamilies.Contains(x.FamilyKey + "|" + Extension(item.Path)))) {
                    item.Reason = "가변 대안 · 정적 글꼴 우선 (필요하면 체크)"; continue;
                }
                int overlaps = faces.Count(x => Extension(item.Path) == ".ttc" ? coveredFaces.Contains(x.Key) : covered.Contains(InstallKey(x, item.Path)));
                if (overlaps > 0) {
                    item.Reason = overlaps == faces.Count ? "같은 글꼴·스타일 중복 제외" : "TTC 일부 중복 · 포함 글꼴 확인 후 체크"; continue;
                }
                item.Recommended = true; item.Reason = "설치 대기";
                foreach (var f in faces) { covered.Add(InstallKey(f, item.Path)); coveredFaces.Add(f.Key); }
            }
            foreach (var item in items.Where(x => x.ForceSelected && x.Metadata != null && (Allowed(x.Path, mode, includeTtc) || x.FormatOverride))) { item.Recommended = true; item.Reason = item.FormatOverride ? "확인 후 개별 선택" : "직접 선택"; }
        }
        static int Rank(FontItem item) {
            if (item.Metadata == null) return 100;
            string ext = Path.GetExtension(item.Path).ToLowerInvariant();
            return (item.Metadata.Variable ? 10 : 0) + (ext == ".ttf" ? 0 : ext == ".ttc" ? 2 : 1);
        }
    }
}
