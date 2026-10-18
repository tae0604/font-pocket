using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    public sealed class PreviewFont : IDisposable {
        PrivateFontCollection collection; public Font Font;
        public PreviewFont(string path, float size) : this(path,size,null) { }
        public PreviewFont(string path, float size,string familyName) : this(path,size,familyName,null) { }
        public PreviewFont(string path, float size,string familyName,FontStyle? preferredStyle) {
            try {
                if (new FileInfo(path).Length > 128L * 1024 * 1024) throw new IOException("미리보기 용량 제한을 넘었습니다.");
                // File-backed private fonts keep GDI+ face identity stable across
                // preview disposal/cache eviction; recycled memory addresses can
                // otherwise resolve to a previously rendered, unrelated face.
                collection = new PrivateFontCollection(); collection.AddFontFile(Path.GetFullPath(path));
                if (collection.Families.Length == 0) throw new IOException("미리보기를 지원하지 않는 폰트입니다.");
                var family = String.IsNullOrWhiteSpace(familyName)?collection.Families[0]:collection.Families.FirstOrDefault(f=>String.Equals(f.Name,familyName,StringComparison.OrdinalIgnoreCase)||String.Equals(f.GetName(0x409),familyName,StringComparison.OrdinalIgnoreCase)||String.Equals(f.GetName(0x412),familyName,StringComparison.OrdinalIgnoreCase));
                if(family==null) throw new NotSupportedException("선택한 TTC 서체를 미리보기 엔진에서 찾을 수 없습니다.");
                var style = new FontStyle[] { FontStyle.Regular, FontStyle.Bold, FontStyle.Italic, FontStyle.Bold | FontStyle.Italic }.FirstOrDefault(x => family.IsStyleAvailable(x));
                if(preferredStyle.HasValue) {if(!family.IsStyleAvailable(preferredStyle.Value)) throw new NotSupportedException("선택한 TTC 스타일은 미리보기 엔진에서 지원하지 않습니다.");style=preferredStyle.Value;}
                Font = new Font(family, size, style, GraphicsUnit.Point);
            } catch { Dispose(); throw; }
        }
        public void Dispose() { if (Font != null) { Font.Dispose(); Font = null; } if (collection != null) { collection.Dispose(); collection = null; } }
    }
    public partial class MainWindow {
        List<ManagedFont> managedFonts = new List<ManagedFont>();
        ListView managerList = new ListView(); TextBox managerSearch = new TextBox(), previewText = new TextBox();
        CheckBox showSystemFonts = new CheckBox(), onlyDuplicates = new CheckBox(), onlyDisabledFonts = new CheckBox();
        Label managerCount = new Label(), previewName = new Label(), previewPath = new Label();
        NumericUpDown previewSize = new NumericUpDown(); Panel previewCanvas = new FontPreviewCanvas();bool layingOutPreview;
        PreviewFont currentPreview; ManagedFont previewTarget;
        bool managerLoaded, updatingManagerChecks, duplicateScanDone;
        Button managerCancel;
        static string RecoveryDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FontPocket", "Removed"); } }
        Control CreateManagerPage() {
            var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Margin = Padding.Empty };managerPage=page;
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); page.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            var search = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            search.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiLanguage.Code=="en"?60:40)); search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
            search.Controls.Add(new Label { Text = "검색", AutoSize = true, Margin = new Padding(0, 9, 0, 0) });
            managerSearch.TextChanged += delegate { RefreshManagerList(); }; controls.Add(managerSearch); managerSearch.Dock=DockStyle.Fill; managerSearch.Margin=new Padding(0,1,10,4); search.Controls.Add(ModernInput.Frame(managerSearch));
            PocketTheme.Cue(managerSearch,"이름, 형식, 메모 또는 태그");
            var reload = MakeButton("새로고침", 100, async delegate { await LoadManagedFonts(); }, false); reload.Height = 36; search.Controls.Add(reload); page.Controls.Add(search);
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            collectionFilter.DropDownStyle=ComboBoxStyle.DropDownList; collectionFilter.Width=155; collectionFilter.Margin=new Padding(0,0,18,0); collectionFilter.SelectedIndexChanged+=delegate { RefreshManagerList(); }; filters.Controls.Add(collectionFilter); controls.Add(collectionFilter);
            showSystemFonts.Text = "시스템 폰트 표시"; showSystemFonts.AutoSize = true; showSystemFonts.CheckedChanged += delegate { RefreshManagerList(); };
            showDisabledFonts.Text = "비활성 표시"; showDisabledFonts.Checked = true; showDisabledFonts.AutoSize = true; showDisabledFonts.CheckedChanged += delegate { RefreshManagerList(); };
            onlyDisabledFonts.Text = "비활성만"; onlyDisabledFonts.AutoSize = true; onlyDisabledFonts.CheckedChanged += delegate { if (onlyDisabledFonts.Checked) onlyDuplicates.Checked = false; showDisabledFonts.Enabled = !busy && !onlyDisabledFonts.Checked; RefreshManagerList(); };
            onlyDuplicates.Text = "중복만"; onlyDuplicates.AutoSize = true; onlyDuplicates.CheckedChanged += delegate { if (onlyDuplicates.Checked) onlyDisabledFonts.Checked = false; RefreshManagerList(); };
            foreach (var filter in new CheckBox[] { showSystemFonts, showDisabledFonts, onlyDisabledFonts, onlyDuplicates }) { filter.Margin = new Padding(0, 7, 16, 0); filters.Controls.Add(filter); controls.Add(filter); }
            managerBasicFilters=filters;managerAdvancedFilters=CreateAdvancedManagerFilters();page.Controls.Add(filters);page.Controls.Add(managerAdvancedFilters);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            tools.Controls.Add(MakeButton("폰트 가져오기",140,delegate {ShowPage(0);links.Focus();},true));
            tools.Controls.Add(MakeButton("상세 정보",100,delegate {OpenDetails(HighlightedFont());},false));
            tools.Controls.Add(MakeButton("비교",80,delegate {CompareManagedFonts();},false));
            managerFilterToggle=MakeButton("필터 ▾",135,delegate {ToggleManagerFilters();},false);managerFilterToggle.Name="ToggleManagerFilters";tools.Controls.Add(managerFilterToggle);
            var moreMenu = new ContextMenuStrip { ShowImageMargin = false, Font = Font,RenderMode=ToolStripRenderMode.System };
            moreMenu.Items.Add("견본 보기",null,delegate {OpenCatalog();});
            moreMenu.Items.Add("중복 정리",null,async delegate {await CleanupExactDuplicates();});
            moreMenu.Items.Add("활성화",null,async delegate {await ToggleManagedFonts(true);});
            moreMenu.Items.Add("비활성화",null,async delegate {await ToggleManagedFonts(false);});
            moreMenu.Items.Add("버전 관리",null,async delegate {await OpenVersionManager(HighlightedFont());});
            moreMenu.Items.Add(new ToolStripSeparator());
            var detailsItem=new ToolStripMenuItem("자세한 목록 보기") {CheckOnClick=true,Name="ManagerDetailedList"};detailsItem.CheckedChanged+=delegate {managerDetailed=detailsItem.Checked;RefreshManagerList();};moreMenu.Items.Add(detailsItem);
            moreMenu.Items.Add(new ToolStripSeparator());
            AddFavoriteAction(moreMenu);
            moreMenu.Items.Add("메모·태그", null, delegate { EditFontNotes(HighlightedFont()); });
            moreMenu.Items.Add("모음 관리", null, delegate { ManageCollections(); });
            moreMenu.Items.Add("버전 보관함 · 미설치 파일",null,async delegate { await OpenVersionManager(null); });
            moreMenu.Items.Add("현재 모음 활성화",null,async delegate { await ToggleCollection(true); });
            moreMenu.Items.Add("현재 모음 비활성화",null,async delegate { await ToggleCollection(false); });
            moreMenu.Items.Add("빠른 검색  Ctrl+K", null, delegate { OpenQuickSearch(); });
            var groupItem=moreMenu.Items.Add("패밀리로 묶기",null,delegate { familyGroups=!familyGroups; RefreshManagerList(); });
            groupItem.Click+=delegate { ((ToolStripMenuItem)groupItem).Checked=familyGroups; };
            moreMenu.Items.Add(new ToolStripSeparator());
            moreMenu.Items.Add("폰트 파일 검사",null,async delegate { await ValidateChosenFonts(); });
            moreMenu.Items.Add("견본·문자표 인쇄 / PDF",null,async delegate { try { await FontReports.Print(this,ChosenManagedFonts()); } catch(Exception ex) { DialogUI.Error(this,ex); } });
            moreMenu.Items.Add("선택 백업",null,async delegate { await ExportManagedFonts(); });
            moreMenu.Items.Add("선택 삭제",null,async delegate { await DeleteManagedFonts(ChosenManagedFonts(),false); });
            moreMenu.Items.Add(new ToolStripSeparator());
            moreMenu.Items.Add("선택 해제", null, delegate { foreach (var f in managedFonts) f.Checked = false; RefreshManagerList(); });
            moreMenu.Items.Add("삭제 폰트 복원", null, async delegate { await RestoreDeletedFonts(); });
            moreMenu.Items.Add(new ToolStripSeparator());
            moreMenu.Items.Add("환경 백업",null,async delegate { await SaveEnvironment(); });
            moreMenu.Items.Add("환경 복원",null,async delegate { await RestoreEnvironment(); });
            moreMenu.Items.Add("Windows 글꼴 설정",null,delegate { OpenWindowsFontSettings(); });
            SimplifyManagerMenu(moreMenu);
            var more = MakeButton("관리 ▾", 90, delegate { }, false);
            more.Name="ManagerMore";more.ContextMenuStrip=moreMenu;
            more.Click += delegate { moreMenu.Show(more, new Point(0, more.Height)); }; tools.Controls.Add(more);
            FormClosed += delegate { moreMenu.Dispose(); };
            managerCancel = MakeButton("중단", 70, delegate { cancelled = true; managerCancel.Enabled = false; }, false); managerCancel.Enabled = false; managerCancel.Visible=false;controls.Remove(managerCancel); tools.Controls.Add(managerCancel); page.Controls.Add(tools);
            managerCount.Dock = DockStyle.Fill; managerCount.ForeColor = Color.FromArgb(105, 112, 124); page.Controls.Add(managerCount);
            managerList.Dock = DockStyle.Fill; managerList.View = View.Details; managerList.FullRowSelect = true; managerList.CheckBoxes = false; managerList.MultiSelect = true; managerList.HideSelection = false; managerList.ShowItemToolTips = true; managerList.BorderStyle = BorderStyle.None;
            managerList.Columns.Add("글꼴 · 스타일", 235); managerList.Columns.Add("형식", 60); managerList.Columns.Add("버전", 70); managerList.Columns.Add("범위", 80); managerList.Columns.Add("상태", 65); managerList.Columns.Add("중복 확인", 115); managerList.Columns.Add("크기", 85); managerList.Columns.Add("추가 날짜",100);
            managerList.Resize+=delegate {ApplyManagerColumns();};
            managerList.HandleCreated += delegate { SetWindowTheme(managerList.Handle, "Explorer", null); SendMessage(managerList.Handle, 0x1036, new IntPtr(0x10000), new IntPtr(0x10000)); };
            managerList.SelectedIndexChanged += delegate { SyncManagerSelection(); };
            managerList.KeyDown+=delegate(object sender,KeyEventArgs e) {
                if(e.Control && e.KeyCode==Keys.A) {foreach(ListViewItem row in managerList.Items) row.Selected=true;e.Handled=true;e.SuppressKeyPress=true;}
                else if(e.KeyCode==Keys.Escape) {foreach(ListViewItem row in managerList.SelectedItems.Cast<ListViewItem>().ToArray()) row.Selected=false;e.Handled=true;e.SuppressKeyPress=true;}
            };
            managerList.DoubleClick+=delegate { OpenDetails(HighlightedFont()); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("상세 정보 · 문자표",null,delegate { OpenDetails(HighlightedFont()); });
            AddFavoriteAction(menu);
            menu.Items.Add("메모·태그",null,delegate { EditFontNotes(HighlightedFont()); });
            menu.Items.Add("버전 관리",null,async delegate { await OpenVersionManager(HighlightedFont()); });
            menu.Items.Add("파일 위치 열기", null, delegate { var font=HighlightedFont(); if (font!=null) OpenManagedLocation(font); });
            menu.Items.Add("표시된 항목 선택", null, delegate { foreach (var f in VisibleManagedFonts()) f.Checked = true; RefreshManagerList(); });
            managerList.ContextMenuStrip = menu; controls.Add(managerList);
            var listPanel = new Surface { Dock = DockStyle.Fill, Padding = new Padding(1, 6, 1, 6), Margin = Padding.Empty }; listPanel.Controls.Add(managerList); page.Controls.Add(listPanel);
            var preview = new Surface { Dock = DockStyle.Fill, Padding = new Padding(10), Margin = new Padding(0, 10, 0, 0) };
            var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 21)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            previewName.Tag="OriginalMetadata"; previewName.Text = UiLanguage.Text("폰트를 선택하면 미리보기가 표시됩니다."); previewName.AutoEllipsis = true; previewName.Dock = DockStyle.Fill; previewLayout.Controls.Add(previewName);
            var input = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty }; input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            previewText.Multiline = true; previewText.Text = FontSamples.Default; previewText.Dock = DockStyle.Fill; previewText.TextChanged += delegate { LayoutManagedPreview(); }; input.Controls.Add(ModernInput.Frame(previewText)); controls.Add(previewText);
            previewSize.Minimum = 12; previewSize.Maximum = 72; previewSize.Value = 28; previewSize.Dock = DockStyle.Fill; previewSize.ValueChanged += delegate { if (previewTarget != null) ShowFontPreview(previewTarget); }; input.Controls.Add(ModernInput.Frame(previewSize)); controls.Add(previewSize); previewLayout.Controls.Add(input);
            previewCanvas.Dock = DockStyle.Fill; previewCanvas.BackColor = Color.White;previewCanvas.AutoScroll=true;previewCanvas.Resize+=delegate {LayoutManagedPreview();};
            previewCanvas.Paint += delegate(object sender, PaintEventArgs e) { if (currentPreview != null) { e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;int width=Math.Max(40,previewCanvas.ClientSize.Width-(previewCanvas.VerticalScroll.Visible?0:SystemInformation.VerticalScrollBarWidth)-8); using (var brush = new SolidBrush(PocketTheme.Ink)) e.Graphics.DrawString(previewText.Text, currentPreview.Font, brush,new RectangleF(0,previewCanvas.AutoScrollPosition.Y,width,Math.Max(previewCanvas.Height,previewCanvas.AutoScrollMinSize.Height))); } }; previewLayout.Controls.Add(previewCanvas);
            previewPath.Dock = DockStyle.Fill; previewPath.ForeColor = Color.FromArgb(110, 118, 130); previewPath.AutoEllipsis = true; previewPath.Font = new Font(Font.FontFamily, 8.25f); previewLayout.Controls.Add(previewPath); preview.Controls.Add(previewLayout); page.Controls.Add(preview);
            BindMenu(moreMenu,ManagerActionReason,t=>ActionDescription(3,t));
            BindMenu(menu,ManagerActionReason,t=>ActionDescription(3,t));
            FormClosed += delegate { ClearFontPreview(); menu.Dispose(); };
            for(int row=0;row<page.Controls.Count;row++)page.SetCellPosition(page.Controls[row],new TableLayoutPanelCellPosition(0,row));
            SetManagerFiltersVisible(false);ApplyManagerColumns();
            return page;
        }
        IEnumerable<ManagedFont> VisibleManagedFonts() { return managedFonts.Where(x => (showSystemFonts.Checked || x.CanRemove) && (onlyDisabledFonts.Checked ? x.Disabled : (showDisabledFonts.Checked || !x.Disabled)) && (!onlyDuplicates.Checked || x.ExactDuplicate || x.NameConflict) && MatchesPersonalFilters(x) && MatchesAdvancedManagerFilters(x)); }
        void SyncManagerSelection() {
            if(updatingManagerChecks) return;
            var selected=new HashSet<ManagedFont>(ChosenManagedFonts());
            foreach(var font in managedFonts) font.Checked=selected.Contains(font);
            var current=HighlightedFont();
            if(current==null) ClearFontPreview();
            else if(previewTarget==null || previewTarget.Path!=current.Path) ShowFontPreview(current);
            UpdateManagerCount();
            UiLanguage.Translate(managerList);
        }
        void RefreshManagerList() {
            string selected = previewTarget == null ? null : previewTarget.Path;
            updatingManagerChecks = true; managerList.BeginUpdate();
            try {
                managerList.Items.Clear();
                managerList.Groups.Clear(); var groups=new Dictionary<string,ListViewGroup>(); managerList.ShowGroups=familyGroups;
                foreach (var font in FontOrdering.Order(VisibleManagedFonts())) {
                    var note=personal==null?null:personal.Note(font);
                    var row = new ListViewItem((note!=null && note.Favorite?"★ ":"")+font.Name) { Tag = font, ToolTipText = font.Path + "\r\n" + font.Version+(note==null?"":"\r\n"+note.Tags+"\r\n"+note.Memo), ForeColor = font.CanRemove && !font.Disabled ? ForeColor : Color.DimGray };
                    if(familyGroups) { string family=font.Metadata==null?font.Name:font.Metadata.Faces[0].Family;ListViewGroup group;if(!groups.TryGetValue(family,out group)) { group=new ListViewGroup(family,HorizontalAlignment.Left);groups[family]=group;managerList.Groups.Add(group); } row.Group=group; }
                    var versionNumber = System.Text.RegularExpressions.Regex.Match(font.Version, @"\d+(?:\.\d+)+");
                    row.SubItems.Add(font.Format); row.SubItems.Add(versionNumber.Success ? versionNumber.Value : font.Version); row.SubItems.Add(font.Scope); row.SubItems.Add(UiLanguage.Text(managerDetailed?(font.Disabled?"비활성":"활성"):(font.Disabled?"사용 안 함":"사용 중"))); row.SubItems.Add(font.DuplicateLabel); row.SubItems.Add((font.Bytes / 1024.0).ToString("N0") + " KB");var added=AddedDate(font);row.SubItems.Add(added==DateTime.MinValue?"—":added.ToString("yyyy-MM-dd"));row.ToolTipText+="\r\n"+UiLanguage.Text("추가 날짜: 최초 확인 시 파일 생성일 기준으로 기록합니다."); managerList.Items.Add(row);
                    row.Selected=font.Checked;
                    if (font.Path == selected && row.Selected) row.Focused = true;
                }
            } finally { updatingManagerChecks = false; managerList.EndUpdate();ApplyManagerColumns();ThemeManager.FitColumns(managerList); }
            if(selected!=null && !managerList.Items.Cast<ListViewItem>().Any(r=>((ManagedFont)r.Tag).Path==selected)) ClearFontPreview();
            SyncManagerSelection();
        }
        void UpdateManagerCount() {
            managerCount.Text = "표시 " + managerList.Items.Count + "개 · 내 계정 활성 " + managedFonts.Count(x => x.CanRemove && !x.Disabled) + " / 비활성 " + managedFonts.Count(x => x.Disabled) + " · 선택 " + managerList.SelectedItems.Count + "개" + (duplicateScanDone ? " · 완전 동일 " + managedFonts.Count(x => x.ExactDuplicate) + "개 / 이름 겹침 " + managedFonts.Count(x => x.NameConflict && !x.ExactDuplicate) + "개" : "") + "\r\n클릭: 한 개 선택 · Ctrl+클릭: 개별 추가·해제 · Shift+클릭: 연속 범위 선택";
            if(!managerDetailed)managerCount.Text=String.Format(UiLanguage.Text("폰트 {0}개 · 선택 {1}개"),managerList.Items.Count,managerList.SelectedItems.Count)+"\r\n"+UiLanguage.Text("폰트를 클릭하면 아래에서 미리 볼 수 있어요. 여러 개를 고르려면 Ctrl+클릭하세요.");
            UpdateManagerFilterCaption();RefreshActionAvailability();
        }
        async void EnsureManagedFonts() { if (!managerLoaded && !busy) await LoadManagedFonts(); }
        internal async Task LoadManagedFonts() {
            if (busy) return;
            var checkedPaths = new HashSet<string>(managedFonts.Where(x => x.Checked).Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
            SetBusy(true); status.Text = "설치된 폰트를 읽는 중…";
            try {
                var loaded = await Task.Run(delegate {
                    var fonts = FontLibrary.Read(); fonts.AddRange(ReadDisabledFonts(fonts));
                    foreach (var font in fonts) { if (cancelled) break; FontLibrary.ReadMetadata(font); font.Checked = checkedPaths.Contains(font.Path); }
                    return FontOrdering.Order(fonts).ToList();
                });
                if (!cancelled) { managedFonts = loaded; managerLoaded = true; duplicateScanDone = false; managerFilterIndex.Clear();RememberFontDates();if(NeedsManagerIndex)await BuildManagerFilterIndex();RefreshManagerList(); status.Text = "폰트 목록 · " + loaded.Count + "개"; }
            } catch (Exception ex) { Log("폰트 목록 오류: " + ex.Message); status.Text = "목록을 읽지 못했습니다. 작업 기록을 확인하세요."; }
            finally { SetBusy(false); }
        }
        void ShowFontPreview(ManagedFont font) {
            ClearFontPreview(); previewTarget = font; previewName.Text = font.Name; previewPath.Text = font.Path;
            try { currentPreview = new PreviewFont(font.Path, (float)previewSize.Value); }
            catch (Exception ex) { previewName.Text = font.Name + " · 미리보기 지원 안 됨"; previewPath.Text = ex.Message; }
            LayoutManagedPreview();
        }
        void LayoutManagedPreview() {if(layingOutPreview) return;layingOutPreview=true;try {if(currentPreview==null) {previewCanvas.AutoScrollMinSize=Size.Empty;return;}int width=Math.Max(40,previewCanvas.ClientSize.Width-(previewCanvas.VerticalScroll.Visible?0:SystemInformation.VerticalScrollBarWidth)-8);using(var g=previewCanvas.CreateGraphics()) previewCanvas.AutoScrollMinSize=new Size(0,(int)Math.Ceiling(g.MeasureString(previewText.Text,currentPreview.Font,width).Height)+8);}finally {layingOutPreview=false;previewCanvas.Invalidate();}}
        void ClearFontPreview() { if (currentPreview != null) { currentPreview.Dispose(); currentPreview = null; } previewTarget = null;previewName.Text=UiLanguage.Text("폰트를 선택하면 미리보기가 표시됩니다.");previewPath.Text="";previewCanvas.AutoScrollPosition=Point.Empty;previewCanvas.AutoScrollMinSize=Size.Empty; previewCanvas.Invalidate(); }
        static void OpenManagedLocation(ManagedFont font) { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + font.Path + "\"") { UseShellExecute = true }); }
        async Task ScanManagedDuplicates(bool filter) {
            if (busy) return;
            if (!managerLoaded) await LoadManagedFonts();
            SetBusy(true); status.Text = "중복 폰트를 확인하는 중…";
            try {
                await Task.Run(delegate { FontLibrary.FindDuplicates(managedFonts.Where(x => !x.Disabled).ToList(), Engine.Hash, () => cancelled, (n, total) => { if (n % 8 == 0 || n == total) UI(delegate { progress.Value = total == 0 ? 0 : n * 100 / total; status.Text = n + " / " + total + " 중복 확인 중"; }); }); });
                duplicateScanDone = true; if (filter) onlyDuplicates.Checked = true; RefreshManagerList();
                status.Text = "중복 확인 완료 · 완전 동일 " + managedFonts.Count(x => x.ExactDuplicate) + "개 · 이름만 같을 수 있는 항목은 따로 표시합니다.";
                foreach (var f in managedFonts.Where(x => x.HashError != null)) Log(f.Name + " · 중복 확인 실패: " + f.HashError);
                Log(status.Text);
            } catch (OperationCanceledException) { duplicateScanDone = false; status.Text = "중복 확인을 중단했습니다."; }
            catch (Exception ex) { Log("중복 확인 오류: " + ex.Message); }
            finally { SetBusy(false); }
        }
        async Task CleanupExactDuplicates() {
            if (busy) return;
            await ScanManagedDuplicates(false); if (!duplicateScanDone) return;
            var groups = DuplicateReview.Groups(managedFonts.Where(x => !x.Disabled).ToList(), FormatMode == FontFormat.OtfOnly);
            if (groups.Count == 0) { LocalizedMessage.Show(this, "정리할 중복 폰트가 없습니다.", "중복 정리"); return; }
            var chosen = ChooseDuplicateCleanup(groups); if (chosen == null) return;
            var keepers = new Dictionary<string, ManagedFont>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in chosen) foreach (var font in group.Removals) keepers[font.Path] = group.Keeper;
            await DeleteManagedFonts(chosen.SelectMany(x => x.Removals).Distinct().ToList(), true, keepers);
        }
        async Task DeleteManagedFonts(List<ManagedFont> selected, bool duplicates, Dictionary<string, ManagedFont> keepers = null) {
            if (busy) return;
            var removable = selected.Where(x => x.CanRemove).ToList();
            if (removable.Count == 0) { status.Text = "삭제할 내 계정 폰트를 선택하세요. 시스템 폰트는 삭제하지 않습니다."; return; }
            string names = String.Join("\r\n", removable.Take(12).Select(x => "• " + x.Name + " [" + x.Format + "]").ToArray());
            string intro = duplicates ? "지정한 폰트를 그룹마다 남기고 " + removable.Count + "개를 정리할까요?" : "선택한 폰트 " + removable.Count + "개를 삭제할까요?";
            if (LocalizedMessage.Show(this, intro + "\r\n\r\n" + names + (removable.Count > 12 ? "\r\n외 " + (removable.Count - 12) + "개" : "") + "\r\n\r\n삭제 전에 복구 ZIP을 자동으로 저장합니다." + (selected.Count > removable.Count ? "\r\n시스템 폰트 " + (selected.Count - removable.Count) + "개는 제외됩니다." : ""), "폰트 삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            ClearFontPreview(); SetBusy(true); int removed = 0, failed = 0; string recovery = null;
            try {
                recovery = Path.Combine(RecoveryDir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zip");
                await Task.Run(delegate {
                    var expected = removable.ToDictionary(x => x.Path, x => Engine.Hash(x.Path), StringComparer.OrdinalIgnoreCase);
                    if (duplicates && removable.Any(x => x.Hash != expected[x.Path])) throw new IOException("중복 확인 이후 파일이 변경됐습니다. 다시 중복을 확인하세요.");
                    Directory.CreateDirectory(RecoveryDir);
                    Engine.Backup(removable.Select(x => x.Path).ToList(), recovery, (n, file) => UI(delegate { progress.Value = n * 100 / removable.Count; status.Text = "삭제 전 백업 " + n + " / " + removable.Count; }), () => cancelled);
                    UI(delegate { Log("삭제 복구 ZIP 저장: " + recovery); });
                    var store = new WindowsFontRemovalStore();
                    foreach (var font in removable) {
                        if (cancelled) break;
                        try {
                            if (Engine.Hash(font.Path) != expected[font.Path]) throw new IOException("백업 이후 파일 내용이 변경돼 삭제하지 않았습니다.");
                            if (duplicates) {
                                ManagedFont keeper;
                                if (keepers == null || !keepers.TryGetValue(font.Path, out keeper) || store.Current(keeper.Path) == null || !File.Exists(keeper.Path) || Engine.Hash(keeper.Path) != keeper.Hash)
                                    throw new IOException("남겨 둘 폰트가 없어졌거나 변경돼 삭제하지 않았습니다.");
                            }
                            string result;
                            if (font.Disabled) {
                                var repo = ActivationRepository.Default;
                                if (Engine.Hash(repo.FontFile(font.DisabledRecord)) != expected[font.Path]) throw new IOException("보관 파일이 변경됐습니다.");
                                try { store.DeleteOwnedFile(font.DisabledRecord.OriginalPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                                repo.Remove(font.DisabledRecord); result = "비활성 폰트 삭제 완료";
                            } else result = FontRemoval.Remove(font, store);
                            removed++; UI(delegate { Log(font.Name + " · " + result); });
                        } catch (Exception ex) { failed++; UI(delegate { Log(font.Name + " · 삭제 실패: " + ex.Message); }); }
                        UI(delegate { progress.Value = (removed + failed) * 100 / removable.Count; status.Text = "폰트 삭제 " + (removed + failed) + " / " + removable.Count; });
                    }
                    if (removed > 0) Engine.Notify();
                });
                status.Text = (cancelled ? "중단" : "삭제 완료") + " · 삭제 " + removed + " / 실패 " + failed; Log(status.Text);
                if (removed > 0) LocalizedMessage.Show(this, "폰트 " + removed + "개를 등록 해제했습니다.\r\n열려 있는 앱에는 다시 실행하거나 Windows에 다시 로그인한 후 반영될 수 있습니다.\r\n\r\n복구 ZIP: " + recovery, "폰트 삭제 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (OperationCanceledException) { status.Text = "폰트 삭제를 중단했습니다."; }
            catch (Exception ex) { Log("삭제 오류: " + ex.Message); status.Text = "삭제 오류 · 작업 기록을 확인하세요."; }
            finally { SetBusy(false); backupLoaded = false; managerLoaded = false; }
            if (removed > 0) await LoadManagedFonts();
        }
        async Task ExportManagedFonts() {
            if (busy) return;
            var files = ChosenManagedFonts().Select(x => x.Path).ToList();
            if (files.Count == 0) { status.Text = "백업할 폰트를 선택하세요."; return; }
            using (var dialog = new SaveFileDialog { Title = "선택한 폰트 백업", Filter = "ZIP|*.zip", FileName = "선택한-폰트-" + DateTime.Now.ToString("yyyyMMdd") + ".zip" }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SetBusy(true);
                try { await Task.Run(delegate { Engine.Backup(files, dialog.FileName, (n, name) => UI(delegate { progress.Value = n * 100 / files.Count; status.Text = n + " / " + files.Count + " 백업 중"; }), () => cancelled); }); status.Text = "선택한 폰트 백업 완료 · " + files.Count + "개"; Log(status.Text + " · " + dialog.FileName); }
                catch (OperationCanceledException) { status.Text = "백업을 중단했습니다."; }
                catch (Exception ex) { Log("선택 백업 오류: " + ex.Message); }
                finally { SetBusy(false); }
            }
        }
        async Task RestoreDeletedFonts() {
            if (busy) return;
            using (var dialog = new OpenFileDialog { Title = "삭제 전 복구 ZIP 선택", Filter = "ZIP|*.zip", InitialDirectory = Directory.Exists(RecoveryDir) ? RecoveryDir : "", Multiselect = true }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                ShowPage(0); await AddPaths(dialog.FileNames); status.Text = "복구 ZIP을 대기열에 추가했습니다. 확인 후 ‘선택한 폰트 설치’를 누르세요.";
            }
        }
        internal async Task PrepareManagerPreview(bool duplicates) {
            await LoadManagedFonts(); ShowPage(3);
            if (duplicates) await ScanManagedDuplicates(true);
            if (managerList.Items.Count > 0) { managerList.Items[0].Selected = true; managerList.Items[0].EnsureVisible(); }
        }
    }
}
