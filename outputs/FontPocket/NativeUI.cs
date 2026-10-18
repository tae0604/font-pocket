using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FontPocket {
    class Surface : Panel {
        public Surface() { DoubleBuffered = true; BackColor = Color.White; Padding = new Padding(16); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent == null ? Color.FromArgb(247, 248, 250) : Parent.BackColor);
            using (var path = Shape.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), 5))
            using (var fill = new SolidBrush(BackColor)) using (var border = new Pen(PocketTheme.Border)) {
                e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path);
            }
        }
    }
    static class Shape {
        public static GraphicsPath Round(RectangleF r, float radius) {
            var p = new GraphicsPath(); float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
    }
    class PocketButton : Button {
        public bool Primary;
        public PocketButton() {FlatStyle=FlatStyle.System;UseVisualStyleBackColor=true;}
    }
    class PocketNavigationButton : Button {
        public bool Selected, Navigation;
        bool hover, pressed;
        public PocketNavigationButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; DoubleBuffered = true; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent == null ? Color.White : Parent.BackColor);
            Color fill = Selected ? PocketTheme.AccentSoft : Color.Transparent;
            if (hover) fill = Color.FromArgb(239, 242, 246);
            if (pressed) fill = Color.FromArgb(227, 232, 239);
            if (!Enabled) fill = Color.FromArgb(245, 246, 248);
            using (var path = Shape.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), 5))
            using (var brush = new SolidBrush(fill)) {
                e.Graphics.FillPath(brush, path);

            }
            if (Navigation && Selected) using (var accent = new SolidBrush(PocketTheme.Accent)) e.Graphics.FillRectangle(accent, 0, 10, 3, Height - 20);
            Color text = !Enabled ? Color.FromArgb(155, 161, 172) : Color.FromArgb(32, 38, 48);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
            flags |= Navigation ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Navigation ? 14 : 4, 0, Width - (Navigation ? 20 : 8), Height), text, flags);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 8));
        }
    }
    public partial class MainWindow {
        Panel pageHost; Control[] pages; PocketNavigationButton[] nav; Label pageTitle = new Label(), pageSubtitle = new Label();
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string list);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        void BuildWindow() {
            Text = "Font Pocket " + UpdateClient.CurrentVersion.ToString(3); Size = new Size(1240, 900); MinimumSize = new Size(1120, 800);
            StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(249, 250, 251);
            Font = new Font("맑은 고딕", 9.5f); ForeColor = Color.FromArgb(32, 38, 48); AutoScaleMode = AutoScaleMode.Dpi;
            PocketTheme.Apply(this);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 216)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(shell);
            var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(242, 244, 247), Padding = new Padding(10) }; shell.Controls.Add(sidebar, 0, 0);
            sidebar.Controls.Add(new PocketBrandLockup {Location=new Point(16,17),Size=new Size(172,36)});
            nav = new PocketNavigationButton[4]; string[] labels = { "폰트 다운로드", "백업", "작업 기록", "폰트 관리" }; int[] order = { 3, 0, 1, 2 };
            for (int i = 0; i < 4; i++) {
                int index = order[i]; nav[index] = new PocketNavigationButton { Text = labels[index], Navigation = true, Location = new Point(10, 84 + i * 44), Size = new Size(196, 36), Font = Font };
                nav[index].Click += delegate { ShowPage(index); }; sidebar.Controls.Add(nav[index]);
            }
            var version = new Label { Text = UpdateClient.CurrentVersion.ToString(3), ForeColor = Color.FromArgb(125, 134, 148), AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, Location = new Point(18, 715) };
            sidebar.Controls.Add(version); sidebar.Resize += delegate { version.Top = sidebar.Height - version.Height - 26; };
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(24, 22, 24, 14) };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); main.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); main.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); shell.Controls.Add(main, 1, 0);
            var heading = new Panel { Dock = DockStyle.Fill };
            pageTitle.Font = new Font("맑은 고딕", 15, FontStyle.Bold); pageTitle.AutoSize = true; pageTitle.Location = new Point(-2, 0);
            heading.Controls.Add(pageTitle); main.Controls.Add(heading);
            pageHost = new Panel { Dock = DockStyle.Fill }; main.Controls.Add(pageHost);
            var import = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
            import.RowStyles.Add(new RowStyle(SizeType.Absolute, 128)); import.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); import.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); import.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); import.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            import.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            var linkCard = new Surface { Dock = DockStyle.Fill, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 8) }; linkCard.Controls.Add(CreateLinkPanel()); import.Controls.Add(linkCard);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 0), Margin = Padding.Empty };
            toolbar.Controls.Add(MakeButton("파일 · ZIP 추가", 135, delegate { SelectFiles(); }, false)); toolbar.Controls.Add(MakeButton("폴더 추가", 100, delegate { SelectFolder(); }, false));
            toolbar.Controls.Add(MakeButton("선택 제거", 95, delegate { foreach (ListViewItem row in list.SelectedItems) { items.Remove((FontItem)row.Tag); list.Items.Remove(row); } ApplyRecommendations(); }, false));
            toolbar.Controls.Add(MakeButton("목록 비우기", 105, delegate { items.Clear(); list.Items.Clear(); UpdateCount(); }, false));
            toolbar.Controls.Add(MakeButton("미리보기", 95, delegate { PreviewQueuedFonts(false); }, false));
            toolbar.Controls.Add(MakeButton("체크 폰트 비교", 130, delegate { PreviewQueuedFonts(true); }, false));
            toolbar.Controls.Add(MakeButton("상세 정보", 95, delegate { OpenQueueDetails(); }, false));
            count.AutoSize = true; count.ForeColor = Color.FromArgb(100, 110, 126); count.Margin = new Padding(12, 8, 0, 0); import.Controls.Add(toolbar);
            var formatCard = new Surface { Dock = DockStyle.Fill, Padding = new Padding(12, 5, 12, 5), Margin = new Padding(0, 0, 0, 10) }; formatCard.Controls.Add(CreateFormatPanel()); import.Controls.Add(formatCard);
            list.Dock = DockStyle.Fill; list.View = View.Details; list.FullRowSelect = true; list.HideSelection = false;
            list.BorderStyle = BorderStyle.None; list.BackColor = Color.White; list.AllowDrop = true; list.CheckBoxes = true; list.ShowItemToolTips = true;
            list.Columns.Add("파일", 205); list.Columns.Add("글꼴 · 스타일", 215); list.Columns.Add("형식", 100); list.Columns.Add("크기", 75); list.Columns.Add("상태", 275);
            var rowHeight = new ImageList { ImageSize = new Size(1, 30), ColorDepth = ColorDepth.Depth32Bit }; rowHeight.Images.Add(new Bitmap(1, 30)); list.SmallImageList = rowHeight;
            list.HandleCreated += delegate { SetWindowTheme(list.Handle, "Explorer", null); SendMessage(list.Handle, 0x1036, new IntPtr(0x10000), new IntPtr(0x10000)); };
            list.ItemChecked += delegate { UpdateCount(); }; list.ItemCheck += HandleItemCheck;
            list.DoubleClick += delegate { PreviewQueuedFonts(false); };
            var queueMenu=new ContextMenuStrip(); queueMenu.Items.Add("상세 정보 · 문자표",null,delegate { OpenQueueDetails(); }); queueMenu.Items.Add("체크한 파일 버전으로 보관",null,delegate { KeepQueuedVersions(); }); list.ContextMenuStrip=queueMenu;
            list.DragEnter += delegate(object sender, DragEventArgs e) { if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            list.DragDrop += async delegate(object sender, DragEventArgs e) { await AddPaths((string[])e.Data.GetData(DataFormats.FileDrop)); };
            var listCard = new Surface { Dock = DockStyle.Fill, Padding = new Padding(1, 7, 1, 7), Margin = Padding.Empty }; listCard.Controls.Add(list); import.Controls.Add(listCard);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0), WrapContents = false };
            actions.Controls.Add(MakeButton("선택한 폰트 설치", 180, async delegate { await InstallAll(); }, true));
            cancel = MakeButton("중단", 80, delegate { cancelled = true; cancel.Enabled = false; }, false); cancel.Enabled = false; controls.Remove(cancel); actions.Controls.Add(cancel);
            actions.Controls.Add(count);
            import.Controls.Add(actions);
            import.Controls.Add(CreateActivityPanel());
            var backup = CreateBackupPage();
            var history = new Surface { Dock = DockStyle.Fill, Padding = new Padding(18) };
            log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.Dock = DockStyle.Fill; log.BorderStyle = BorderStyle.None; log.BackColor = Color.White; log.Font = PocketTheme.Body(); log.Text = "다운로드와 설치 결과가 여기에 표시됩니다."; history.Controls.Add(log);
            var manager = CreateManagerPage();
            managerList.SmallImageList = rowHeight; backupList.SmallImageList = rowHeight;
            InitializeAdvancedFeatures();
            pages = new Control[] { import, backup, history, manager }; foreach (var page in pages) pageHost.Controls.Add(page);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 9, 0, 0) }; footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            status.Dock = DockStyle.Fill; status.ForeColor = Color.FromArgb(100, 110, 126); status.Text = "파일이나 링크를 추가해 주세요."; status.AutoEllipsis = true;
            progress.Dock = DockStyle.Fill; progress.Margin = new Padding(14, 3, 0, 4); footer.Controls.Add(status); footer.Controls.Add(progress); main.Controls.Add(footer);
            InitializeAppUpdates(sidebar);
            InitializeActionAvailability();
            BindMenu(queueMenu,t=>ActionReason(0,t),t=>ActionDescription(0,t));
            ShowPage(0); UpdateCount();
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) { e.Cancel = true; cancelled = true; status.Text = "중단 중입니다. 완료되면 닫아 주세요."; } };
            FormClosed += delegate { queueMenu.Dispose(); rowHeight.Dispose(); try { if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true); } catch { } };
        }
        internal void ShowPage(int index) {
            for (int i = 0; i < pages.Length; i++) { pages[i].Visible = i == index; nav[i].Selected = i == index; nav[i].Invalidate(); }
            pages[index].BringToFront();
            pageTitle.Text = new string[] { "폰트 다운로드", "폰트 백업", "작업 기록", "폰트 관리" }[index];
            if (index == 1) EnsureBackupFonts();
            if (index == 3) EnsureManagedFonts();
        }
    }
}
