using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace FontPocket {
    static class Verification {
        static void Assert(bool condition, string text) { if (!condition) throw new Exception(text); }
        static FontItem Item(string name, params Face[] faces) { return new FontItem(name) { Metadata = new FontMetadata { Faces = faces.ToList() } }; }
        static Face F(string style, bool variable) { return new Face { Family = "Test Sans", Style = style, Variable = variable }; }
        public static void Local(string root, List<string> report) {
            FontDiscoveryTests.Run(report);
            WebFontExportTests.Run(root,report);
            ManagerFilterTests.Run(root,report);
            FontImportTests.Run(root,report);
            FileAssociationTests.Run(report);
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string regular = Path.Combine(dir, "arial.ttf"), bold = Path.Combine(dir, "arialbd.ttf");
            var a = FontMetadata.Read(regular); var b = FontMetadata.Read(bold);
            Assert(a.Faces[0].Key != b.Faces[0].Key, "Real Regular and Bold were collapsed");
            report.Add("PASS: real font metadata keeps Regular and Bold distinct");
            var backupChoices = new List<BackupFont> { new BackupFont { Path = regular, Name = a.Display, Format = "TTF", Selected = true }, new BackupFont { Path = bold, Name = b.Display, Format = "TTF", Selected = false } };
            Assert(backupChoices[1].Matches("bold") && backupChoices[0].Matches("ARIAL.TTF") && backupChoices[0].Matches("ttf"), "Backup search does not match names, filenames or formats");
            string selectedZip = Path.Combine(root, "selected-fonts.zip");
            Engine.Backup(backupChoices.Where(x => x.Selected).Select(x => x.Path).ToList(), selectedZip, (n, s) => {}, () => false);
            var restoredSelection = Engine.Extract(selectedZip, Path.Combine(root, "selected-restored"));
            Assert(restoredSelection.Count == 1 && Engine.Hash(restoredSelection[0]) == Engine.Hash(regular) && Engine.Hash(restoredSelection[0]) != Engine.Hash(bold), "Unselected font included in backup");
            report.Add("PASS: selective backup contains only the checked font; backup search matches name, filename and format");
            var ttf = Item("regular.ttf", F("Regular", false)); var otf = Item("regular.otf", F("Regular", false));
            var boldItem = Item("bold.ttf", F("Bold", false)); var boldOtf = Item("bold.otf", F("Bold", false)); var variable = Item("variable.ttf", F("Regular", true));
            var ttc = Item("collection.ttc", F("Regular", false), F("Bold", false), F("Light", false));
            var items = new List<FontItem> { otf, boldItem, boldOtf, variable, ttc, ttf };
            Selection.Recommend(items, FontFormat.TtfOnly, true);
            Assert(ttf.Recommended && !otf.Recommended && boldItem.Recommended && !variable.Recommended && !ttc.Recommended, "Default selection failed");
            Assert(ttc.Reason.Contains("일부"), "Partial TTC overlap not explained");
            Assert(!boldOtf.Recommended, "TTF-only selected OTF");
            report.Add("PASS: TTF-only excludes all OTF, retains different weights, flags partial TTC and variable alternative");
            Selection.Recommend(items, FontFormat.OtfOnly, false); Assert(otf.Recommended && boldOtf.Recommended && !ttf.Recommended && !boldItem.Recommended && !ttc.Recommended, "OTF-only selection failed");
            Selection.Recommend(items, FontFormat.Both, false); Assert(ttf.Recommended && otf.Recommended && boldItem.Recommended && boldOtf.Recommended && !ttc.Recommended, "Both formats were collapsed");
            var duplicate = Item("duplicate.ttf", F("Regular", false)); items.Add(duplicate);
            Selection.Recommend(items, FontFormat.Both, false);
            Assert(items.Count(x => x.Recommended && Selection.Extension(x.Path) == ".ttf" && x.Metadata.Faces[0].Style == "Regular") == 1, "Same-format duplicates selected");
            Assert(Selection.InstallKey(F("Regular", false), "a.ttf") != Selection.InstallKey(F("Regular", false), "a.otf"), "Installed OTF would block TTF");
            Selection.Recommend(new List<FontItem> { ttc }, FontFormat.TtfOnly, true); Assert(ttc.Recommended, "Unique TTC not selected");
            Selection.Recommend(new List<FontItem> { ttc }, FontFormat.Both, false); Assert(!ttc.Recommended, "TTC opt-out ignored");
            Selection.Recommend(new List<FontItem> { variable }, FontFormat.TtfOnly, false); Assert(variable.Recommended, "Variable-only font lost");
            report.Add("PASS: OTF-only, both formats, same-format deduplication, installed-format separation and TTC opt-in");
            Selection.Recommend(new List<FontItem> { otf }, FontFormat.TtfOnly, false); Assert(!otf.Recommended, "TTF-only fell back to OTF");
            var downloadLinks = new List<DownloadLink> { new DownloadLink { Url = "https://example.com/Fonts-TTF.zip", Title = "TTF ZIP" }, new DownloadLink { Url = "https://example.com/Fonts-OTF.zip", Title = "OTF ZIP" } };
            Assert(Selection.DefaultDownloads(downloadLinks, FontFormat.TtfOnly, false).SetEquals(new string[] { downloadLinks[0].Url }), "TTF download package selection failed");
            Assert(Selection.DefaultDownloads(downloadLinks, FontFormat.OtfOnly, false).SetEquals(new string[] { downloadLinks[1].Url }), "OTF download package selection failed");
            Assert(Selection.DefaultDownloads(downloadLinks, FontFormat.Both, false).Count == 2, "Both download packages not selected");
            report.Add("PASS: missing format does not fall back, link ZIP defaults match TTF / OTF / both");
            var exceptionItem = Item("only.otf", F("Regular", false));
            bool declined = Selection.AcceptOverride(exceptionItem, FontFormat.TtfOnly, false, s => false);
            Assert(!declined && !exceptionItem.FormatOverride && !Selection.CanInstall(exceptionItem, FontFormat.TtfOnly, false), "Declined override was allowed");
            string question = "";
            bool accepted = Selection.AcceptOverride(exceptionItem, FontFormat.TtfOnly, false, s => { question = s; return true; });
            exceptionItem.ForceSelected = true;
            Selection.Recommend(new List<FontItem> { exceptionItem }, FontFormat.TtfOnly, false);
            Assert(accepted && question.Contains("OTF") && exceptionItem.Recommended && Selection.CanInstall(exceptionItem, FontFormat.TtfOnly, false), "Confirmed OTF selection was blocked at installation");
            variable.ForceSelected = true;
            Selection.Recommend(new List<FontItem> { variable, ttf }, FontFormat.TtfOnly, false);
            Assert(variable.Recommended && ttf.Recommended, "Explicitly chosen variable font was deselected");
            variable.ForceSelected = false;
            report.Add("PASS: declined override stays blocked; confirmed OTF survives selection and installation checks; explicit variable choice retained");
            var kinds = NoonnuDownload.Links("<section><h4>Static</h4><a href='/a/1-ttf.zip'>TTF</a></section><section><h4>Variable</h4><a href='/b/2-ttf.zip'>TTF</a></section>", "https://example.com/");
            Assert(kinds.Count == 2 && kinds[0].Variant == FontVariant.Static && kinds[1].Variant == FontVariant.Variable && kinds[0].ToString().Contains("정적") && kinds[1].ToString().Contains("가변"), "Sibling Static / Variable headings were lost");
            Assert(Selection.DefaultDownloads(kinds, FontFormat.TtfOnly, false).SetEquals(new string[] { kinds[0].Url }), "Default selected variable and static together");
            Assert(Selection.DefaultDownloads(new List<DownloadLink> { kinds[1] }, FontFormat.TtfOnly, false).Contains(kinds[1].Url), "Variable-only download not selected");
            report.Add("PASS: equal TTF labels identified as static / variable using headings; static default and variable-only preserved");
            variable.Metadata.Faces[0].Family = "Test Sans Variable";
            Selection.Recommend(new List<FontItem> { variable, ttf }, FontFormat.TtfOnly, false);
            Assert(!variable.Recommended && ttf.Recommended, "Variable-suffix family was not matched");
            report.Add("PASS: variable-family naming suffix normalized against static family");
            string malformed = Path.Combine(root, "bad-tables.ttf"); File.WriteAllBytes(malformed, new byte[] { 0,1,0,0,0,1,0,0,0,0,0,0 });
            bool rejected = false; try { FontMetadata.Read(malformed); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "Malformed metadata was accepted"); report.Add("PASS: invalid table bounds rejected");
            var parsed = NoonnuDownload.Links("<script>var s='<a href=\"https://fake.test/font.zip\">font</a>';</script><a href='/download/a.zip?a=1&amp;b=2'><span>글꼴 받기</span></a>", "https://example.com/font");
            Assert(parsed.Count == 1 && parsed[0].Url == "https://example.com/download/a.zip?a=1&b=2" && parsed[0].Title == "글꼴 받기", "Download HTML parser failed");
            report.Add("PASS: HTML links decoded, relative links resolved, script anchors excluded");
            var routes = new Dictionary<string, string> {
                { "https://official.example/", "<a href='/broken-download'>다운로드</a><a href=/about/font>글꼴 소개</a>" },
                { "https://official.example/about/font", "<button onclick=\"location.href='/brand/typeface'\">서체 자세히</button>" },
                { "https://official.example/brand/typeface", "<a href='#' data-href='/downloads/typeface' title='다운로드'>다음 다운로드 페이지</a>" },
                { "https://official.example/downloads/typeface", "<a href='/files/download?name=Typeface.ttf'>TTF 다운로드</a><a href='/web.woff2'>웹폰트</a>" }
            };
            var visits = new List<string>();
            var plan = NoonnuDownload.Discover("official.example", () => false, s => {}, delegate(string url, out string final) {
                final = url; visits.Add(url);
                if (!routes.ContainsKey(url)) throw new IOException("fixture unavailable");
                return routes[url];
            });
            Assert(plan.Noonnu == null && plan.Files.Count == 1 && plan.Files[0].Url.EndsWith("name=Typeface.ttf") && visits.Count == 5, "Official multi-step traversal failed");
            report.Add("PASS: official homepage -> introduction -> detail -> download, with failed sibling recovery and query filename");
            int fetches = 0;
            var direct = NoonnuDownload.Discover("https://official.example/Font.zip?token=abc", () => false, s => {}, delegate(string url, out string final) { fetches++; final = url; return ""; });
            Assert(direct.Files.Count == 1 && fetches == 0, "Direct download URL was fetched as HTML");
            report.Add("PASS: direct ZIP address accepted without an HTML request");
            var manual = NoonnuDownload.Discover("https://official.example/", () => false, s => {}, delegate(string url, out string final) {
                final = url; if (url != "https://official.example/") return "<p>No direct download</p>";
                return String.Join("", Enumerable.Range(1, 6).Select(i => "<a href='/font/" + i + "'>글꼴 " + i + "</a>"));
            });
            Assert(manual.Files.Count == 0 && manual.Pages.Count == 2, "Unvisited pages missing for manual navigation");
            report.Add("PASS: unvisited relevant pages retained for the next-page chooser");
            string zip = Path.Combine(root, "desktop-only.zip");
            using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) {
                z.CreateEntryFromFile(regular, "desktop/font.ttf"); z.CreateEntry("web/font.woff2"); z.CreateEntry("__MACOSX/._font.ttf"); z.CreateEntry("._font.otf");
            }
            Assert(Engine.Extract(zip, Path.Combine(root, "desktop-only")).Count == 1, "Web font or Mac resource was extracted");
            report.Add("PASS: WOFF2 and macOS resource files excluded");
        }
        public static void Network(string root) {
            Directory.CreateDirectory(root); string reportFile = Path.Combine(root, "network-results.txt");
            Action<string> report = text => File.AppendAllText(reportFile, DateTime.Now.ToString("HH:mm:ss") + " " + text + Environment.NewLine, Encoding.UTF8);
            try {
                foreach (string id in new string[] { "694", "1269", "https://cactus.tistory.com/306", "https://github.com/wanteddev/wanted-sans", "https://sun.fo/suit/" }) {
                    string address = id.StartsWith("https:") ? id : "https://noonnu.cc/font_page/" + id;
                    var plan = NoonnuDownload.Resolve(address, () => false, report);
                    Assert(plan.Files.Count > 0, "No release download found for " + id);
                    report("PASS: resolved " + id + " -> " + plan.Files[0].Url);
                    if (id == "https://sun.fo/suit/") {
                        Assert(plan.Files.Count == 3 && plan.Files.Count(x => x.Variant == FontVariant.Variable) == 1 && plan.Files.Count(x => x.Variant == FontVariant.Static) == 2, "Live SUIT static / variable links incorrect");
                        var defaults = Selection.DefaultDownloads(plan.Files, FontFormat.TtfOnly, false);
                        Assert(defaults.Count == 1 && defaults.Contains(plan.Files.Single(x => x.Variant == FontVariant.Static && Selection.LinkFormats(x) == 1).Url), "Live SUIT TTF default incorrect");
                        foreach (var link in plan.Files) report("SUIT: " + link.ToString());
                        var vf = plan.Files.Single(x => x.Variant == FontVariant.Variable);
                        string vfZip = NoonnuDownload.Download(vf, Path.Combine(root, "suit-variable"), () => false, (d, t) => {});
                        var vfPaths = Engine.Extract(vfZip, Path.Combine(root, "suit-variable-fonts"));
                        Assert(vfPaths.Count > 0 && vfPaths.All(x => FontMetadata.Read(x).Variable), "SUIT variable package was not variable");
                        report("PASS: SUIT variable ZIP downloaded and fvar metadata confirmed");
                    }
                    if (id != "694") continue;
                    DateTime last = DateTime.MinValue;
                    string file = NoonnuDownload.Download(plan.Files[0], Path.Combine(root, "download"), () => false, (done, total) => {
                        if ((DateTime.UtcNow - last).TotalSeconds >= 3) { report("Downloaded " + done + "/" + total); last = DateTime.UtcNow; }
                    });
                    var paths = Engine.Extract(file, Path.Combine(root, "extracted"));
                    var items = paths.Select(x => new FontItem(x) { Hash = Engine.Hash(x), Metadata = FontMetadata.Read(x) }).ToList();
                    Selection.Recommend(items, FontFormat.TtfOnly, false);
                    int selected = items.Count(x => x.Recommended);
                    Assert(selected > 0 && selected < items.Count, "Real TTF/OTF package was not filtered");
                    var picked = items.Where(x => x.Recommended).SelectMany(x => x.Metadata.Faces).ToList();
                    Assert(picked.Count == picked.Select(x => x.Key).Distinct().Count(), "Duplicate faces selected");
                    Assert(picked.Any(x => x.Style.Contains("Bold")) && picked.Any(x => x.Style.Contains("Regular")), "Real weights were lost");
                    Assert(items.Where(x => x.Recommended).All(x => Path.GetExtension(x.Path).Equals(".ttf", StringComparison.OrdinalIgnoreCase)), "TTF preference not applied to real package");
                    string family = Engine.Validate(items.First(x => x.Recommended).Path);
                    report("PASS: live download -> ZIP -> metadata -> TTF selection -> Windows validation: " + items.Count + " files / " + selected + " selected / " + family);
                    foreach (var item in items.Where(x => x.Recommended)) report("SELECTED: " + item.Metadata.Display);
                }
                report("All network tests passed. No permanent font installation was performed.");
            } catch (Exception ex) { report("FAIL: " + ex); throw; }
        }
    }
}
