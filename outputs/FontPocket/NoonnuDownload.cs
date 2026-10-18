using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FontPocket {
    public enum FontVariant { Unknown, Static, Variable }
    public class DownloadLink {
        public string Url, Title;
        public FontVariant Variant;
        public bool FormatOverride;
        public string FileName { get { return Uri.UnescapeDataString(Path.GetFileName(new Uri(Url).AbsolutePath)); } }
        public string KindLabel { get { return Variant == FontVariant.Variable ? "가변 (Variable)" : Variant == FontVariant.Static ? "정적 (Static)" : "종류 확인 전"; } }
        public override string ToString() {
            int formats = Selection.LinkFormats(this);
            string format = formats == 1 ? "TTF" : formats == 2 ? "OTF" : formats == 4 ? "TTC" : formats == 3 ? "TTF + OTF" : "ZIP / 폰트";
            string name = String.IsNullOrWhiteSpace(Title) || Regex.IsMatch(Title.Trim(), @"^(TTF|OTF|TTC|다운로드|Download)$", RegexOptions.IgnoreCase) ? FileName : Title + " · " + FileName;
            return KindLabel + "  ·  " + format + "  ·  " + name;
        }
    }
    public class DownloadPlan {
        public string Source, Noonnu;
        public string Input;
        public List<DownloadLink> Files = new List<DownloadLink>();
        public List<DownloadLink> Pages = new List<DownloadLink>();
    }
    public static class NoonnuDownload {
        public static FontVariant InferVariant(string url, string title, string context) {
            string explicitText = Uri.UnescapeDataString((url ?? "") + " " + (title ?? ""));
            if (Regex.IsMatch(explicitText, @"variable|가변|(?:^|[\s/_\-.])vf(?:$|[\s/_\-.])", RegexOptions.IgnoreCase)) return FontVariant.Variable;
            if (Regex.IsMatch(explicitText, @"static|정적", RegexOptions.IgnoreCase)) return FontVariant.Static;
            var headings = Regex.Matches(context ?? "", @"<(?:h[1-6]|p|legend|span)\b[^>]*>\s*(?<k>Static|Variable|정적|가변)(?:\s+(?:Fonts?|폰트))?\s*</", RegexOptions.IgnoreCase);
            if (headings.Count == 0) return FontVariant.Unknown;
            string label = headings[headings.Count - 1].Groups["k"].Value;
            return label.Equals("variable", StringComparison.OrdinalIgnoreCase) || label == "가변" ? FontVariant.Variable : FontVariant.Static;
        }
        static readonly Regex Anchors = new Regex(@"<(?<tag>a|button)\b(?<attrs>[^>]*)>(?<text>.*?)</\k<tag>\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
        static string Attribute(string attrs, string name) {
            var m = Regex.Match(attrs, @"(?:^|\s)" + Regex.Escape(name) + @"\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)'|(?<v>[^\s>]+))", RegexOptions.IgnoreCase);
            return m.Success ? WebUtility.HtmlDecode(m.Groups["v"].Value) : "";
        }
        public static string Text(string html) { return Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim(); }
        public static List<DownloadLink> Links(string html, string baseUrl) {
            var result = new List<DownloadLink>(); var seen = new HashSet<string>();
            // Ignore script/style bodies: a download link must be an actual anchor.
            html = Regex.Replace(html, @"<(script|style)\b[^>]*>.*?</\1\s*>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
            foreach (Match m in Anchors.Matches(html)) {
                Uri url; string attrs = m.Groups["attrs"].Value;
                string raw = Attribute(attrs, "href");
                if (String.IsNullOrWhiteSpace(raw) || raw == "#" || raw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) {
                    raw = Attribute(attrs, "data-href");
                    if (String.IsNullOrWhiteSpace(raw)) raw = Attribute(attrs, "data-url");
                    if (String.IsNullOrWhiteSpace(raw)) {
                        // Recognize literal navigation only. Never execute page JavaScript.
                        var nav = Regex.Match(Attribute(attrs, "onclick"), @"(?:location(?:\.href)?\s*=\s*|(?:window\.)?open\s*\(\s*|location\.(?:assign|replace)\s*\(\s*)['""](?<u>[^'""]+)['""]", RegexOptions.IgnoreCase);
                        if (nav.Success) raw = nav.Groups["u"].Value;
                    }
                }
                if (String.IsNullOrWhiteSpace(raw)) continue;
                if (raw.StartsWith("#") || !Uri.TryCreate(new Uri(baseUrl), raw, out url) || url.Scheme != "https") continue;
                string title = Text(m.Groups["text"].Value);
                if (String.IsNullOrWhiteSpace(title)) title = Attribute(attrs, "title") + " " + Attribute(attrs, "aria-label");
                if (String.IsNullOrWhiteSpace(title)) { var image = Regex.Match(m.Groups["text"].Value, @"<img\b(?<a>[^>]*)>", RegexOptions.IgnoreCase); if (image.Success) title = Attribute(image.Groups["a"].Value, "alt"); }
                int start = Math.Max(0, m.Index - 1800);
                if (seen.Add(url.AbsoluteUri)) result.Add(new DownloadLink { Url = url.AbsoluteUri, Title = title.Trim(), Variant = InferVariant(url.AbsoluteUri, title, html.Substring(start, m.Index - start)) });
            }
            return result;
        }
        static void CheckUri(Uri uri) {
            if (uri.Scheme != "https" || !String.IsNullOrEmpty(uri.UserInfo) || uri.IsLoopback || uri.Port != 443) throw new InvalidDataException("공개 HTTPS 주소만 지원합니다.");
            foreach (var address in Dns.GetHostAddresses(uri.DnsSafeHost)) {
                var ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
                byte[] b = ip.GetAddressBytes();
                if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (b.Length == 16 && ((b[0] & 0xfe) == 0xfc)) ||
                    (b.Length == 4 && (b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 || (b[0] == 169 && b[1] == 254) || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))))
                    throw new InvalidDataException("로컬 네트워크 주소는 지원하지 않습니다.");
            }
        }
        static HttpWebResponse Open(string url, Func<bool> cancelled) {
            for (int i = 0; i < 10; i++) {
                if (cancelled()) throw new OperationCanceledException();
                Uri uri = new Uri(url); CheckUri(uri);
                var request = SecureHttp.Create(uri.AbsoluteUri);
                request.AllowAutoRedirect = false; request.Timeout = 20000; request.ReadWriteTimeout = 20000;
                request.UserAgent = "FontPocket/3.1 (+Windows desktop font manager)";
                request.Accept = "*/*"; request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                var response = (HttpWebResponse)request.GetResponse(); int status = (int)response.StatusCode;
                if (status >= 300 && status < 400) {
                    string location = response.Headers["Location"]; response.Dispose();
                    if (String.IsNullOrWhiteSpace(location)) throw new IOException("다운로드 리디렉션 주소가 없습니다.");
                    url = new Uri(uri, location).AbsoluteUri; continue;
                }
                return response;
            }
            throw new IOException("페이지 이동 횟수가 너무 많습니다.");
        }
        public static string Html(string url, Func<bool> cancelled, out string finalUrl) {
            using (var response = Open(url, cancelled)) {
                finalUrl = response.ResponseUri.AbsoluteUri;
                using (var s = response.GetResponseStream()) using (var memory = new MemoryStream()) {
                    Copy(s, memory, 8L * 1024 * 1024, cancelled, null, -1);
                    Encoding encoding = Encoding.UTF8;
                    try { if (!String.IsNullOrEmpty(response.CharacterSet)) encoding = Encoding.GetEncoding(response.CharacterSet); } catch { }
                    string html = encoding.GetString(memory.ToArray());
                    var charset = Regex.Match(html, @"charset\s*=\s*[""']?(?<c>[a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
                    if (charset.Success) { try { html = Encoding.GetEncoding(charset.Groups["c"].Value).GetString(memory.ToArray()); } catch { } }
                    return html;
                }
            }
        }
        static void Copy(Stream from, Stream to, long max, Func<bool> cancelled, Action<long, long> progress, long length) {
            byte[] buffer = new byte[81920]; int n; long total = 0; DateTime deadline = DateTime.UtcNow.AddMinutes(15);
            while ((n = from.Read(buffer, 0, buffer.Length)) > 0) {
                if (cancelled()) throw new OperationCanceledException();
                if (DateTime.UtcNow > deadline) throw new IOException("다운로드 제한 시간을 넘었습니다.");
                total += n; if (total > max) throw new IOException("다운로드 허용 크기를 넘었습니다.");
                to.Write(buffer, 0, n); if (progress != null) progress(total, length);
            }
        }
        static bool FileLink(DownloadLink link) {
            string path = Uri.UnescapeDataString(new Uri(link.Url).PathAndQuery).ToLowerInvariant();
            string label = (link.Title + " " + path).ToLowerInvariant();
            if (Regex.IsMatch(label, @"woff|webfont|웹폰트|\.exe\b|\.dmg\b|/archive/|\.tar\.gz")) return false;
            return Regex.IsMatch(path, @"\.(zip|ttf|otf|ttc)(?:$|[?&#])") ||
                (Regex.IsMatch(label, @"다운로드|download|첨부") && Regex.IsMatch(label, @"\bttf\b|\botf\b|\bttc\b|\bzip\b"));
        }
        public static int Score(DownloadLink link) {
            string s = Uri.UnescapeDataString(link.Title + " " + link.Url).ToLowerInvariant(); int score = 0;
            if (s.Contains("ttf")) score += 20;
            if (s.Contains("windows") || s.Contains("윈도우")) score += 10;
            if (s.Contains("zip")) score += 5;
            if (s.Contains("otf")) score += 2;
            if (Regex.IsMatch(s, @"(?:pretendard|sans|font)(?:jp|jp-)|일본어|japanese|\bmac\b|\bweb\b|variable|가변")) score -= 30;
            return score;
        }
        public delegate string PageFetcher(string url, out string finalUrl);
        public static Uri InputUri(string address) {
            Uri uri; string value = address.Trim();
            if (!value.Contains("://")) value = "https://" + value;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "https" || String.IsNullOrWhiteSpace(uri.Host) || !String.IsNullOrEmpty(uri.UserInfo) || uri.IsLoopback || uri.Port != 443)
                throw new InvalidDataException("눈누, 공식 사이트 또는 폰트 파일의 HTTPS 주소를 넣어 주세요.");
            return new UriBuilder(uri) { Fragment = "" }.Uri;
        }
        public static DownloadPlan Resolve(string address, Func<bool> cancelled, Action<string> report) {
            return Discover(address, cancelled, report, delegate(string url, out string final) { return Html(url, cancelled, out final); });
        }
        public static DownloadPlan Discover(string address, Func<bool> cancelled, Action<string> report, PageFetcher fetch) {
            Uri uri = InputUri(address); string final;
            bool isNoonnu = (uri.Host.Equals("noonnu.cc", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.noonnu.cc", StringComparison.OrdinalIgnoreCase)) && Regex.IsMatch(uri.AbsolutePath, @"^/font_page/\d+/?$");
            var plan = new DownloadPlan { Input = uri.AbsoluteUri, Source = uri.AbsoluteUri, Noonnu = isNoonnu ? uri.AbsoluteUri : null };
            if (isNoonnu) {
                report("눈누에서 제작사 링크를 찾는 중…");
                var source = Links(fetch(uri.AbsoluteUri, out final), final).FirstOrDefault(x => x.Title.Contains("다운로드 페이지"));
                if (source == null) throw new InvalidDataException("눈누의 다운로드 페이지 링크를 찾지 못했습니다.");
                plan.Source = source.Url;
            }
            var pending = new Queue<Tuple<string, int>>(); var seen = new HashSet<string>(); var scheduled = new HashSet<string>();
            pending.Enqueue(Tuple.Create(plan.Source, 0)); scheduled.Add(plan.Source);
            for (int count = 0; pending.Count > 0 && count < 12; count++) {
                if (cancelled()) throw new OperationCanceledException();
                var next = pending.Dequeue(); string current = next.Item1;
                if (!seen.Add(current)) continue;
                try {
                report("다운로드 탐색 " + (count + 1) + "/12 · " + new Uri(current).Host);
                var asFile = new DownloadLink { Url = current, Title = Path.GetFileName(new Uri(current).AbsolutePath), Variant = InferVariant(current, "", "") };
                if (FileLink(asFile)) { plan.Files.Add(asFile); break; }
                Uri sourceUri = new Uri(current);
                if (sourceUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) {
                    string[] parts = sourceUri.AbsolutePath.Trim('/').Split('/');
                    if (parts.Length >= 2) {
                        try {
                            string api = "https://api.github.com/repos/" + parts[0] + "/" + parts[1] + "/releases/latest";
                            string json = fetch(api, out final);
                            var data = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                            if (data != null && data.ContainsKey("assets")) foreach (var o in (object[])data["assets"]) {
                                var asset = (Dictionary<string, object>)o;
                                var link = new DownloadLink { Url = (string)asset["browser_download_url"], Title = (string)asset["name"] };
                                link.Variant = InferVariant(link.Url, link.Title, "");
                                if (FileLink(link)) plan.Files.Add(link);
                            }
                            if (plan.Files.Count > 0) break;
                        } catch (WebException) { report("GitHub API 연결 제한 · 제작사 페이지에서 다시 찾는 중…"); }
                    }
                }
                string html = fetch(current, out final); var links = Links(html, final);
                plan.Files.AddRange(links.Where(FileLink));
                if (plan.Files.Count > 0) break;
                // GitHub release assets are loaded through this public HTML fragment.
                foreach (Match m in Regex.Matches(html, @"[""'](?<u>/[^""']+/releases/expanded_assets/[^""']+)[""']")) {
                    string expandedUrl = new Uri(new Uri(final), WebUtility.HtmlDecode(m.Groups["u"].Value)).AbsoluteUri;
                    string expanded = fetch(expandedUrl, out final);
                    plan.Files.AddRange(Links(expanded, final).Where(FileLink));
                }
                if (plan.Files.Count > 0) break;
                var pages = links.Where(NavigationLink).OrderByDescending(NavigationScore).ToList();
                plan.Pages.AddRange(pages);
                if (next.Item2 < 3) foreach (var link in pages.Take(4)) {
                    string normalized = new UriBuilder(link.Url) { Fragment = "" }.Uri.AbsoluteUri;
                    if (scheduled.Add(normalized)) pending.Enqueue(Tuple.Create(normalized, next.Item2 + 1));
                }
                } catch (OperationCanceledException) { throw; }
                catch (Exception ex) { report("페이지 연결 실패 · " + current + " · " + ex.Message); }
            }
            plan.Pages = plan.Pages.Where(x => !seen.Contains(x.Url)).GroupBy(x => x.Url).Select(x => x.First()).OrderByDescending(NavigationScore).Take(100).ToList();
            plan.Files = plan.Files.GroupBy(x => x.Url).Select(x => x.First()).OrderByDescending(Score).Take(100).ToList();
            foreach (var link in plan.Files) if (String.IsNullOrWhiteSpace(link.Title)) link.Title = Uri.UnescapeDataString(Path.GetFileName(new Uri(link.Url).AbsolutePath));
            return plan;
        }
        static bool NavigationLink(DownloadLink link) {
            string label = link.Title + " " + Uri.UnescapeDataString(new Uri(link.Url).PathAndQuery);
            if (Regex.IsMatch(label, @"로그인|회원가입|구매|장바구니|개인정보|이용약관|login|signup|privacy|checkout|\.(?:pdf|png|jpe?g|svg|mp4|css|js|woff2?|exe|dmg)(?:$|[?&#])", RegexOptions.IgnoreCase)) return false;
            return Regex.IsMatch(label, @"다운로드|download|첨부|자료실|서체|글꼴|폰트|fonts?|typeface|releases|GitHub|자세히|상세보기|details?", RegexOptions.IgnoreCase);
        }
        static int NavigationScore(DownloadLink link) {
            string label = link.Title + " " + link.Url;
            return (Regex.IsMatch(label, @"다운로드|download|첨부", RegexOptions.IgnoreCase) ? 30 : 0) +
                (Regex.IsMatch(label, @"폰트|글꼴|서체|fonts?|typeface", RegexOptions.IgnoreCase) ? 10 : 0) +
                (label.Contains("/releases/latest") ? 20 : 0);
        }
        public static string Download(DownloadLink link, string dir, Func<bool> cancelled, Action<long, long> progress) {
            Directory.CreateDirectory(dir); string part = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".part");
            try {
                using (var response = Open(link.Url, cancelled)) {
                    if (response.ContentLength > 1024L * 1024 * 1024) throw new IOException("다운로드 파일은 1GB 이하만 지원합니다.");
                    using (var input = response.GetResponseStream()) using (var output = File.Create(part))
                        Copy(input, output, 1024L * 1024 * 1024, cancelled, progress, response.ContentLength);
                }
                byte[] magic = new byte[4]; using (var s = File.OpenRead(part)) if (s.Read(magic, 0, 4) != 4) throw new InvalidDataException("다운로드 파일이 비어 있습니다.");
                string tag = Encoding.ASCII.GetString(magic), ext;
                if (magic[0] == 0x50 && magic[1] == 0x4b && magic[2] == 3 && magic[3] == 4) ext = ".zip";
                else if (tag == "OTTO") ext = ".otf";
                else if (tag == "ttcf") ext = ".ttc";
                else if (tag == "true" || (magic[0] == 0 && magic[1] == 1 && magic[2] == 0 && magic[3] == 0)) ext = ".ttf";
                else throw new InvalidDataException("받은 파일이 ZIP / 데스크톱 폰트가 아닙니다. 제작사 페이지에서 확인해 주세요.");
                string target = Path.ChangeExtension(part, ext); File.Move(part, target); return target;
            } finally { if (File.Exists(part)) File.Delete(part); }
        }
    }
}
