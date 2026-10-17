using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FontPocket {
    public class FontItem {
        public string Path, Name, Hash;
        public FontMetadata Metadata;
        public bool Recommended;
        public bool FormatOverride, ForceSelected;
        public string Reason;
        public FontItem(string path) { Path = path; Name = System.IO.Path.GetFileName(path); }
    }
    public static class Engine {
        public const string RegistryPath = @"Software\Microsoft\Windows NT\CurrentVersion\Fonts";
        public static readonly string UserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern int AddFontResourceEx(string file, uint flags, IntPtr reserved);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern bool RemoveFontResourceEx(string file, uint flags, IntPtr reserved);
        [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(byte[] data, uint size, IntPtr reserved, out uint count);
        [DllImport("gdi32.dll")] static extern bool RemoveFontMemResourceEx(IntPtr handle);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, UIntPtr wp, IntPtr lp, uint flags, uint timeout, out UIntPtr result);
        public static bool IsFont(string path) {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".ttf" || ext == ".otf" || ext == ".ttc";
        }
        public static string Hash(string path) {
            using (var s = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
        }
        public static string Validate(string path) {
            using (var s = File.OpenRead(path)) {
                if (s.Length < 12 || s.Length > 512L * 1024 * 1024) throw new InvalidDataException("폰트 파일 크기가 올바르지 않습니다.");
                byte[] b = new byte[4]; s.Read(b, 0, 4);
                string magic = Encoding.ASCII.GetString(b);
                if (!(b[0] == 0 && b[1] == 1 && b[2] == 0 && b[3] == 0) && magic != "OTTO" && magic != "ttcf" && magic != "true")
                    throw new InvalidDataException("올바른 TTF / OTF / TTC 파일이 아닙니다.");
            }
            // Validate in process memory: no global registration or filename visibility is needed.
            byte[] bytes = File.ReadAllBytes(path); uint count;
            IntPtr handle = AddFontMemResourceEx(bytes, (uint)bytes.Length, IntPtr.Zero, out count);
            if (handle == IntPtr.Zero) throw new InvalidDataException("Windows가 이 폰트를 읽을 수 없습니다.");
            RemoveFontMemResourceEx(handle);
            return FontMetadata.Read(path).Display;
        }
        public static List<string> InstalledFiles(bool includeMachine) {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ReadRegistry(Registry.CurrentUser, UserDir, files);
            if (includeMachine) ReadRegistry(Registry.LocalMachine, Environment.GetFolderPath(Environment.SpecialFolder.Fonts), files);
            return files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }
        static void ReadRegistry(RegistryKey hive, string baseDir, HashSet<string> files) {
            using (var key = hive.OpenSubKey(RegistryPath)) {
                if (key == null) return;
                foreach (string name in key.GetValueNames()) {
                    string value = key.GetValue(name) as string;
                    if (String.IsNullOrWhiteSpace(value)) continue;
                    value = Environment.ExpandEnvironmentVariables(value);
                    try {
                        string full = Path.IsPathRooted(value) ? value : Path.Combine(baseDir, value);
                        if (File.Exists(full) && IsFont(full)) files.Add(Path.GetFullPath(full));
                    } catch (ArgumentException) { }
                }
            }
        }
        public static HashSet<string> InstalledHashes(Action<string> warning) {
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in InstalledFiles(true)) {
                try { hashes.Add(Hash(file)); }
                catch (Exception ex) { warning(Path.GetFileName(file) + ": 중복 확인 제외 · " + ex.Message); }
            }
            return hashes;
        }
        public static void Install(FontItem item, HashSet<string> hashes) {
            FontInstaller.Install(item,hashes,new WindowsFontInstallStore(),Validate);
        }
        public static void Notify() {
            UIntPtr result;
            SendMessageTimeout(new IntPtr(0xffff), 0x001d, UIntPtr.Zero, IntPtr.Zero, 2, 1000, out result);
        }
        public static List<string> Extract(string zip, string temp) {
            var paths = new List<string>(); long total = 0;
            Directory.CreateDirectory(temp);
            using (var archive = ZipFile.OpenRead(zip)) {
                foreach (var entry in archive.Entries) {
                    if (!ArchiveFont(entry)) continue;
                    total += entry.Length;
                    if (entry.Length > 512L * 1024 * 1024 || total > 2L * 1024 * 1024 * 1024 || paths.Count >= 10000)
                        throw new InvalidDataException("ZIP 허용 범위를 넘었습니다 (폰트 1개 512MB / 전체 2GB / 10,000개).");
                    // Never use an archive-controlled path, even for nested or malicious ZIP entries.
                    string target = Path.Combine(temp, paths.Count.ToString("D5") + Path.GetExtension(entry.Name).ToLowerInvariant());
                    using (var input = entry.Open()) using (var output = File.Create(target)) {
                        byte[] buffer = new byte[81920]; int read; long bytes = 0;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
                            bytes += read;
                            if (bytes > entry.Length || bytes > 512L * 1024 * 1024) throw new InvalidDataException("ZIP 크기 정보가 올바르지 않습니다.");
                            output.Write(buffer, 0, read);
                        }
                    }
                    paths.Add(target);
                }
            }
            return paths;
        }
        public static bool ArchiveFont(ZipArchiveEntry entry) {
            return IsFont(entry.Name) && !entry.FullName.Replace('\\', '/').StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase) && !entry.Name.StartsWith("._");
        }
        public static void Backup(List<string> files, string destination, Action<int, string> report, Func<bool> cancelled) {
            string temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (var stream = new FileStream(temp, FileMode.CreateNew))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create)) {
                    var hashes = new HashSet<string>(); var notes = new StringBuilder();
                    notes.AppendLine("Font Pocket backup / " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                    for (int i = 0; i < files.Count; i++) {
                        if (cancelled()) throw new OperationCanceledException();
                        string hash = Hash(files[i]);
                        if (hashes.Add(hash)) {
                            string entryName = "fonts/" + i.ToString("D5") + "_" + Path.GetFileName(files[i]);
                            VerifiedArchive.AddFont(archive,entryName,files[i],hash,cancelled);
                            notes.AppendLine(hash + "  " + entryName);
                        }
                        report(i + 1, Path.GetFileName(files[i]));
                    }
                    var manifest = archive.CreateEntry("manifest.txt");
                    using (var writer = new StreamWriter(manifest.Open(), Encoding.UTF8)) writer.Write(notes.ToString());
                }
                if (cancelled()) throw new OperationCanceledException();
                if (File.Exists(destination)) File.Replace(temp, destination, null);
                else File.Move(temp, destination);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    public partial class MainWindow : Form {
        ListView list = new ListView(); Label status = new Label(), count = new Label();
        ProgressBar progress = new ProgressBar(); TextBox log = new TextBox();
        CheckBox allFonts = new CheckBox(); Button cancel = new Button();
        List<Control> controls = new List<Control>(); List<FontItem> items = new List<FontItem>();
        string tempRoot = Path.Combine(Path.GetTempPath(), "FontPocket-" + Guid.NewGuid().ToString("N"));
        bool busy; volatile bool cancelled;
        public MainWindow() { BuildWindow(); }
        Button MakeButton(string text, int width, EventHandler click, bool primary) {
            var b = new PocketButton { Text = text, Size = new Size(width, 34), Primary = primary, Font = Font, Margin = new Padding(0, 0, 8, 0) };
            b.Click += click; b.Click += delegate { RefreshActionAvailability(); }; controls.Add(b); return b;
        }
        void UI(Action a) { if (!IsDisposed) Invoke(a); }
        void Log(string message) { log.AppendText("\r\n" + message); AppendRecent(message); }
        void UpdateCount() { count.Text = list.CheckedItems.Count + "개 선택 / " + items.Count + "개"; RefreshActionAvailability(); }
        void SetBusy(bool value) {
            busy = value; foreach (var control in controls) control.Enabled = !value;
            showDisabledFonts.Enabled = !value && !onlyDisabledFonts.Checked;
            list.Enabled = !value;
            cancel.Enabled = value; if (value) { cancelled = false; progress.Value = 0; }
            if (managerCancel != null) managerCancel.Enabled = value;
            RefreshActionAvailability();
        }
        async void SelectFiles() {
            using (var d = new OpenFileDialog { Title = "폰트 또는 백업 ZIP 선택", Filter = "폰트 / ZIP|*.ttf;*.otf;*.ttc;*.zip", Multiselect = true })
                if (d.ShowDialog() == DialogResult.OK) await AddPaths(d.FileNames);
        }
        async void SelectFolder() {
            using (var d = new FolderBrowserDialog { Description = "하위 폴더의 폰트와 ZIP도 함께 불러옵니다.", ShowNewFolderButton = false })
                if (d.ShowDialog() == DialogResult.OK) await AddPaths(new string[] { d.SelectedPath });
        }
        IEnumerable<string> Walk(string dir) {
            var result = new List<string>(); var pending = new Stack<string>(); pending.Push(dir);
            while (pending.Count > 0 && !cancelled) {
                string current = pending.Pop();
                try {
                    result.AddRange(Directory.GetFiles(current).Where(x => Engine.IsFont(x) || Path.GetExtension(x).Equals(".zip", StringComparison.OrdinalIgnoreCase)));
                    foreach (string child in Directory.GetDirectories(current)) if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                } catch (Exception ex) { UI(delegate { Log(current + ": " + ex.Message); }); }
            }
            return result;
        }
        internal async Task AddPaths(string[] paths) {
            if (busy) return; SetBusy(true); status.Text = "파일을 불러오는 중…";
            var before = new HashSet<FontItem>(items);
            try {
                await Task.Run(delegate {
                    foreach (string path in paths) {
                        if (cancelled) break;
                        var found = Directory.Exists(path) ? Walk(path) : new string[] { path };
                        foreach (string file in found) {
                            if (cancelled) break;
                            try {
                                DownloadLink origin; downloadOrigins.TryGetValue(file, out origin);
                                if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase)) {
                                    string target = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
                                    var extracted = Engine.Extract(file, target);
                                    using (var zip = ZipFile.OpenRead(file)) {
                                        var names = zip.Entries.Where(Engine.ArchiveFont).Select(x => x.Name).ToList();
                                        for (int i = 0; i < extracted.Count && !cancelled; i++) {
                                            try { AddOne(extracted[i], names[i], origin); }
                                            catch (Exception ex) { string detail = names[i] + ": " + ex.Message; UI(delegate { Log(detail); }); }
                                        }
                                    }
                                    if (extracted.Count == 0) UI(delegate { Log(Path.GetFileName(file) + ": 폰트 파일이 없습니다."); });
                                } else if (Engine.IsFont(file)) AddOne(file, Path.GetFileName(file), origin);
                            } catch (Exception ex) { UI(delegate { Log(Path.GetFileName(file) + ": " + ex.Message); }); }
                        }
                    }
                });
                ConfirmMissingFormat(items.Where(x => !before.Contains(x)).ToList());
                ApplyRecommendations();
                if (items.Count > before.Count) Log("대기열 추가 · " + (items.Count - before.Count) + "개 폰트");
                status.Text = cancelled ? "불러오기를 중단했습니다." : list.CheckedItems.Count == 0 ? "선택한 형식의 폰트가 없습니다 · 위에서 설치 형식을 바꿔 주세요." : "형식 선택 적용 완료 · 체크한 폰트만 설치합니다.";
            } catch (Exception ex) { Log(ex.Message); status.Text = "불러오기 실패 · 작업 기록을 확인하세요."; }
            finally { UpdateCount(); SetBusy(false); }
        }
        void AddOne(string path, string name, DownloadLink origin) {
            string hash = Engine.Hash(path);
            FontMetadata metadata = null; string reason = null;
            try { metadata = FontMetadata.Read(path); }
            catch (Exception ex) { reason = ex.Message; }
            UI(delegate {
                if (items.Any(x => x.Hash == hash)) return;
                var item = new FontItem(path) { Name = name, Hash = hash, Metadata = metadata, Reason = reason,
                    FormatOverride = origin != null && origin.FormatOverride,
                    ForceSelected = origin != null && (origin.FormatOverride || (origin.Variant == FontVariant.Variable && metadata != null && metadata.Variable)) }; items.Add(item);
                var row = new ListViewItem(name) { Tag = item };
                row.SubItems.Add(metadata == null ? "정보 읽기 실패" : metadata.Display); row.ToolTipText = metadata == null ? reason : metadata.Display;
                row.SubItems.Add(Path.GetExtension(name).TrimStart('.').ToUpperInvariant() + (metadata != null && metadata.Variable ? " · 가변" : ""));
                row.SubItems.Add((new FileInfo(path).Length / 1024.0).ToString("N0") + " KB"); row.SubItems.Add(reason ?? "선택 분석 대기"); list.Items.Add(row);
            });
        }
        void ApplyRecommendations() {
            Selection.Recommend(items, FormatMode, includeTtc.Checked);
            adjustingChecks = true;
            try { foreach (ListViewItem row in list.Items) { var item = (FontItem)row.Tag; row.Checked = item.Recommended; row.SubItems[4].Text = item.Reason; } }
            finally { adjustingChecks = false; }
            UpdateCount();
            status.Text = list.CheckedItems.Count == 0 && items.Count > 0 ? "선택한 형식의 폰트가 없습니다 · 형식을 바꾸거나 파일을 추가해 주세요." : "설치 형식 적용 · " + list.CheckedItems.Count + "개 선택";
        }
        async Task InstallAll() {
            if (busy || items.Count == 0) return;
            var selected = list.Items.Cast<ListViewItem>().Where(x => x.Checked).ToList();
            if (selected.Count == 0) { status.Text = "설치할 폰트에 체크해 주세요."; return; }
            SetBusy(true); int installed = 0, skipped = 0, failed = 0;
            FontFormat installMode = FormatMode; bool allowTtc = includeTtc.Checked;
            try {
                status.Text = "이미 설치된 폰트를 확인하는 중…";
                await Task.Run(delegate {
                    var hashes = Engine.InstalledHashes(s => UI(delegate { Log(s); }));
                    var faceKeys = new HashSet<string>();
                    var installedVersions=new List<ManagedFont>();
                    foreach (string file in Engine.InstalledFiles(true)) {
                        if (cancelled) break;
                        try { var current=new ManagedFont { Path=file };FontLibrary.ReadMetadata(current);installedVersions.Add(current); foreach (var f in current.Metadata.Faces) faceKeys.Add(Selection.InstallKey(f, file)); } catch { }
                    }
                    for (int i = 0; i < selected.Count && !cancelled; i++) {
                        FontItem item = (FontItem)selected[i].Tag; string state;
                        try {
                            if (item.Metadata == null) throw new InvalidDataException("글꼴 정보를 읽지 못해 설치하지 않습니다.");
                            if (!Selection.CanInstall(item, installMode, allowTtc)) throw new InvalidDataException("선택한 설치 형식에 포함되지 않습니다.");
                            string currentHash = Engine.Hash(item.Path);
                            if (currentHash != item.Hash) throw new IOException("목록 추가 후 파일이 변경됐습니다. 다시 추가해 주세요.");
                            var savedVersion=VersionLibrary.Default.Capture(item.Path);
                            foreach(var oldVersion in installedVersions.Where(f=>f.Identity==savedVersion.Identity)) VersionLibrary.Default.Capture(oldVersion.Path);
                            if (hashes.Contains(currentHash)) { skipped++; state = "이미 설치됨 · 건너뜀"; }
                            else if (item.Metadata.Faces.Any(f => faceKeys.Contains(Selection.InstallKey(f, item.Path)))) { skipped++; state = "기존 글꼴 유지 · 새 파일은 버전 관리에 보관"; }
                            else { Engine.Install(item, hashes); installed++; state = "설치 완료"; foreach (var f in item.Metadata.Faces) faceKeys.Add(Selection.InstallKey(f, item.Path)); }
                        } catch (Exception ex) { failed++; state = "실패 · " + ex.Message; }
                        int index = i; string result = state;
                        UI(delegate { selected[index].SubItems[4].Text = result; progress.Value = (index + 1) * 100 / selected.Count; status.Text = (index + 1) + " / " + selected.Count + " 처리 중"; Log(item.Name + " · " + result); });
                    }
                    if (installed > 0) Engine.Notify();
                });
                if (installed > 0) { managerLoaded = false; backupLoaded = false; }
                status.Text = (cancelled ? "중단" : "완료") + " · 설치 " + installed + " / 중복 " + skipped + " / 실패 " + failed;
                Log(status.Text);
            } catch (Exception ex) { Log(ex.Message); status.Text = "설치 실패 · 작업 기록을 확인하세요."; }
            finally { SetBusy(false); }
        }
        async Task Backup() {
            if (busy) return;
            if (!backupLoaded) await LoadBackupFonts();
            var files = backupFonts.Where(x => x.Selected).Select(x => x.Path).ToList();
            if (files.Count == 0) { status.Text = "백업할 폰트를 먼저 체크해 주세요."; return; }
            string dest;
            using (var dialog = new SaveFileDialog { Title = "폰트 백업 저장", Filter = "ZIP 백업|*.zip", FileName = "내-폰트-" + DateTime.Now.ToString("yyyyMMdd") + ".zip", OverwritePrompt = true }) {
                if (dialog.ShowDialog() != DialogResult.OK) return; dest = dialog.FileName;
            }
            SetBusy(true);
            try {
                status.Text = "백업을 만드는 중…";
                await Task.Run(delegate { Engine.Backup(files, dest, (n, name) => UI(delegate { progress.Value = n * 100 / files.Count; status.Text = n + " / " + files.Count + " 백업 중"; }), () => cancelled); });
                status.Text = "백업 완료 · " + files.Count + "개 파일 확인"; Log("백업 저장: " + dest);
                LocalizedMessage.Show(this, "백업을 저장했습니다.\r\n\r\n" + dest + "\r\n\r\nPC 초기화 전에 외장 드라이브나 클라우드로 복사하세요.", "백업 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (OperationCanceledException) { status.Text = "백업을 중단했습니다."; }
            catch (Exception ex) { status.Text = "백업 실패 · 작업 기록을 확인하세요."; Log(ex.Message); }
            finally { SetBusy(false); }
        }
    }
    static class Program {
        [STAThread] static int Main(string[] args) {
            if (args.Length > 1 && args[0] == "--network-test") {
                try { Verification.Network(args[1]); return 0; } catch { return 1; }
            }
            if (args.Length > 0 && args[0] == "--self-test") {
                try { SelfTest(args[1]); return 0; } catch (Exception ex) { File.WriteAllText(Path.Combine(args[1], "test-failure.txt"), ex.ToString()); return 1; }
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var window = new MainWindow()) {
                if (args.Length > 2 && args[0] == "--preview-compare") {
                    window.Shown += async delegate {
                        var before = new HashSet<string>(Engine.InstalledFiles(true), StringComparer.OrdinalIgnoreCase);
                        await window.AddPaths(new string[] { args[2] });
                        using (var dialog = window.CreateComparisonPreview()) {
                            dialog.Show(window); Application.DoEvents();
                            using (var bmp = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bmp, new Rectangle(Point.Empty, dialog.Size)); bmp.Save(args[1]); }
                            dialog.Close();
                        }
                        if (!before.SetEquals(Engine.InstalledFiles(true))) throw new Exception("Preview changed installed font registrations");
                        window.Close();
                    }; Application.Run(window);
                }
                else if (args.Length > 1 && args[0] == "--preview-manager") {
                    window.Shown += async delegate {
                        await window.PrepareManagerPreview(args.Length > 2 && args[2] == "duplicates"); Application.DoEvents();
                        using (var bmp = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(bmp, new Rectangle(Point.Empty, window.Size)); bmp.Save(args[1]); }
                        window.Close();
                    }; Application.Run(window);
                }
                else if (args.Length > 1 && args[0] == "--preview-backup") {
                    window.Shown += async delegate {
                        await window.PrepareBackupPreview(); Application.DoEvents();
                        using (var bmp = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(bmp, new Rectangle(Point.Empty, window.Size)); bmp.Save(args[1]); }
                        window.Close();
                    }; Application.Run(window);
                }
                else if (args.Length > 2 && args[0] == "--preview-download") {
                    window.Shown += delegate {
                        string source = "https://sun.fo/suit/";
                        var plan = NoonnuDownload.Discover(source, () => false, s => {}, delegate(string url, out string final) { final = source; return File.ReadAllText(args[2]); });
                        CheckedListBox choices;
                        using (var dialog = window.CreateDownloadDialog(plan, out choices)) {
                            dialog.Show(window); Application.DoEvents();
                            using (var bmp = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bmp, new Rectangle(Point.Empty, dialog.Size)); bmp.Save(args[1]); }
                            dialog.Close();
                        }
                        window.Close();
                    };
                    Application.Run(window);
                }
                else if (args.Length > 1 && args[0] == "--preview") {
                    window.Shown += async delegate {
                        if (args.Length > 2) await window.AddPaths(new string[] { args[2] });
                        Application.DoEvents();
                        using (var bmp = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(bmp, new Rectangle(Point.Empty, window.Size)); bmp.Save(args[1]); }
                        window.Close();
                    };
                    Application.Run(window);
                } else { window.Shown += delegate { window.ShowPage(3); }; Application.Run(window); }
            }
            return 0;
        }
        static void SelfTest(string root) {
            Directory.CreateDirectory(root); var report = new List<string>();
            string font = Engine.InstalledFiles(true).First(x => Path.GetExtension(x).Equals(".ttf", StringComparison.OrdinalIgnoreCase));
            string name = Engine.Validate(font); report.Add("PASS: Windows font validation: " + name);
            string bad = Path.Combine(root, "invalid.ttf"); File.WriteAllText(bad, "this is not a font file");
            bool rejected = false; try { Engine.Validate(bad); } catch (InvalidDataException) { rejected = true; }
            if (!rejected) throw new Exception("Malformed font was accepted"); report.Add("PASS: malformed font rejected");
            string zip = Path.Combine(root, "backup.zip"); Engine.Backup(new List<string> { font, font }, zip, (n, s) => {}, () => false);
            var files = Engine.Extract(zip, Path.Combine(root, "restore"));
            if (files.Count != 1 || Engine.Hash(files[0]) != Engine.Hash(font)) throw new Exception("Backup round trip failed");
            report.Add("PASS: backup / restore hash equality and content deduplication");
            using (var z = ZipFile.Open(Path.Combine(root, "traversal.zip"), ZipArchiveMode.Create)) { z.CreateEntryFromFile(font, "../../escaped.ttf"); }
            string safe = Path.Combine(root, "safe"); var extracted = Engine.Extract(Path.Combine(root, "traversal.zip"), safe);
            if (extracted.Count != 1 || !Path.GetFullPath(extracted[0]).StartsWith(Path.GetFullPath(safe) + Path.DirectorySeparatorChar)) throw new Exception("Archive escaped destination");
            report.Add("PASS: ZIP path traversal stays inside extraction directory");
            bool stopped = false;
            string before = Engine.Hash(zip);
            try { Engine.Backup(new List<string> { font }, zip, (n, s) => {}, () => true); } catch (OperationCanceledException) { stopped = true; }
            if (!stopped || Engine.Hash(zip) != before) throw new Exception("Cancellation changed existing backup");
            report.Add("PASS: cancelled backup preserves existing ZIP");
            Engine.Backup(new List<string> { font }, zip, (n, s) => {}, () => false);
            if (Engine.Extract(zip, Path.Combine(root, "replaced")).Count != 1) throw new Exception("Atomic replacement failed");
            report.Add("PASS: existing backup replaced atomically");
            Verification.Local(root, report);
            ManagementTests.Run(root, report);
            ActivationTests.Run(root, report);
            AdvancedTests.Run(root, report);
            ReliabilityTests.Run(root, report);
            File.WriteAllLines(Path.Combine(root, "test-results.txt"), report.ToArray(), Encoding.UTF8);
        }
    }
}
