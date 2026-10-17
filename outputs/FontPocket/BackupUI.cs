using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    public class BackupFont {
        public string Path, Name, Format, Size;
        public bool Selected = true;
        public bool Matches(string query) { return String.IsNullOrWhiteSpace(query) || (Name + " " + System.IO.Path.GetFileName(Path) + " " + Format).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0; }
    }
    public partial class MainWindow {
        List<BackupFont> backupFonts = new List<BackupFont>();
        ListView backupList = new ListView(); TextBox backupSearch = new TextBox(); Label backupCount = new Label();
        bool backupLoaded, updatingBackupChecks;
        Control CreateBackupPage() {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            var scope = new Panel { Dock = DockStyle.Fill };
            scope.Controls.Add(new Label { Text = "백업할 폰트를 체크한 뒤 ZIP으로 저장하세요.", AutoSize = true, Location = Point.Empty });
            allFonts.Text = "모든 사용자와 Windows 기본 폰트도 표시"; allFonts.AutoSize = true; allFonts.Location = new Point(0, 24); scope.Controls.Add(allFonts); controls.Add(allFonts);
            allFonts.CheckedChanged += async delegate { if (!busy) await LoadBackupFonts(); }; layout.Controls.Add(scope);
            var searchBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
            searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            searchBar.Controls.Add(new Label { Text = "검색", AutoSize = true, Margin = new Padding(0, 5, 0, 0) });
            backupSearch.Dock = DockStyle.Fill; backupSearch.Margin = new Padding(0, 1, 12, 4); backupSearch.TextChanged += delegate { RefreshBackupList(); }; controls.Add(backupSearch); searchBar.Controls.Add(backupSearch);
            var refresh = MakeButton("새로고침", 100, async delegate { await LoadBackupFonts(); }, false); refresh.Height = 28; searchBar.Controls.Add(refresh); layout.Controls.Add(searchBar);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            tools.Controls.Add(MakeButton("전체 선택", 100, delegate { foreach (var f in backupFonts) f.Selected = true; RefreshBackupList(); }, false));
            tools.Controls.Add(MakeButton("전체 해제", 100, delegate { foreach (var f in backupFonts) f.Selected = false; RefreshBackupList(); }, false));
            tools.Controls.Add(MakeButton("검색 결과 선택", 130, delegate { foreach (var f in backupFonts.Where(x => x.Matches(backupSearch.Text))) f.Selected = true; RefreshBackupList(); }, false));
            layout.Controls.Add(tools);
            backupList.Dock = DockStyle.Fill; backupList.View = View.Details; backupList.CheckBoxes = true; backupList.FullRowSelect = true; backupList.HideSelection = false;
            backupList.BorderStyle = BorderStyle.None; backupList.ShowItemToolTips = true; backupList.Columns.Add("글꼴 · 스타일", 365); backupList.Columns.Add("형식", 95); backupList.Columns.Add("크기", 85); backupList.Columns.Add("파일", 315);
            backupList.HandleCreated += delegate { SetWindowTheme(backupList.Handle, "Explorer", null); SendMessage(backupList.Handle, 0x1036, new IntPtr(0x10000), new IntPtr(0x10000)); };
            backupList.ItemCheck += delegate(object sender, ItemCheckEventArgs e) { if (!updatingBackupChecks) { ((BackupFont)backupList.Items[e.Index].Tag).Selected = e.NewValue == CheckState.Checked; UpdateBackupCount(); } };
            controls.Add(backupList);
            var listSurface = new Surface { Dock = DockStyle.Fill, Padding = new Padding(1, 6, 1, 6), Margin = Padding.Empty }; listSurface.Controls.Add(backupList); layout.Controls.Add(listSurface);
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
            bottom.Controls.Add(MakeButton("선택한 폰트 백업", 180, async delegate { await Backup(); }, true));
            backupCount.AutoSize = true; backupCount.ForeColor = Color.FromArgb(100, 110, 126); backupCount.Margin = new Padding(10, 8, 0, 0); bottom.Controls.Add(backupCount); layout.Controls.Add(bottom);
            UpdateBackupCount(); return layout;
        }
        async void EnsureBackupFonts() { if (!backupLoaded && !busy) await LoadBackupFonts(); }
        internal async Task LoadBackupFonts() {
            if (busy) return;
            bool includeMachine = allFonts.Checked;
            var previous = backupFonts.ToDictionary(x => x.Path, x => x.Selected, StringComparer.OrdinalIgnoreCase);
            SetBusy(true); status.Text = "설치된 폰트 목록을 읽는 중…";
            try {
                var loaded = await Task.Run(delegate {
                    var rows = new List<BackupFont>();
                    foreach (string path in Engine.InstalledFiles(includeMachine)) {
                        if (cancelled) break;
                        string name = Path.GetFileNameWithoutExtension(path); bool variable = false;
                        try { var metadata = FontMetadata.Read(path); name = metadata.Display; variable = metadata.Variable; } catch { }
                        string size = ""; try { size = (new FileInfo(path).Length / 1024.0).ToString("N0") + " KB"; } catch { }
                        rows.Add(new BackupFont { Path = path, Name = name, Format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant() + (variable ? " · 가변" : ""), Size = size,
                            Selected = !previous.ContainsKey(path) || previous[path] });
                    }
                    return rows.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                });
                if (!cancelled) { backupFonts = loaded; backupLoaded = true; RefreshBackupList(); status.Text = "백업할 폰트를 선택하세요 · " + loaded.Count + "개"; }
                else status.Text = "폰트 목록 읽기를 중단했습니다.";
            } catch (Exception ex) { status.Text = "폰트 목록을 읽지 못했습니다."; Log("백업 목록 오류: " + ex.Message); }
            finally { SetBusy(false); }
        }
        void RefreshBackupList() {
            updatingBackupChecks = true; backupList.BeginUpdate();
            try {
                backupList.Items.Clear();
                foreach (var font in backupFonts.Where(x => x.Matches(backupSearch.Text))) {
                    var row = new ListViewItem(font.Name) { Tag = font, Checked = font.Selected, ToolTipText = font.Path };
                    row.SubItems.Add(font.Format); row.SubItems.Add(font.Size); row.SubItems.Add(Path.GetFileName(font.Path)); backupList.Items.Add(row);
                }
            } finally { updatingBackupChecks = false; backupList.EndUpdate(); }
            UpdateBackupCount();
        }
        void UpdateBackupCount() { backupCount.Text = backupFonts.Count(x => x.Selected) + "개 선택 / 전체 " + backupFonts.Count + "개 · 표시 " + backupList.Items.Count + "개"; RefreshActionAvailability(); }
        internal async Task PrepareBackupPreview() { await LoadBackupFonts(); ShowPage(1); }
    }
}
