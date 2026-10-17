using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    public partial class MainWindow {
        PersonalLibrary personal; ComboBox collectionFilter=new ComboBox(); bool familyGroups,hotkeyRegistered; Form quickSearch;
        [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr handle,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr handle,int id);
        public class CollectionChoice { public string Id {get;set;} public string Name {get;set;} public override string ToString() { return Name; } }
        List<CollectionChoice> collectionChoices=new List<CollectionChoice>();
        CollectionChoice CurrentCollection {get {return collectionFilter.SelectedIndex>=0 && collectionFilter.SelectedIndex<collectionChoices.Count?collectionChoices[collectionFilter.SelectedIndex]:null;}}
        void InitializeAdvancedFeatures() {
            collectionFilter.DrawMode=DrawMode.Normal;
            try { personal=new PersonalLibrary(PersonalLibrary.DefaultPath); }
            catch(Exception ex) { Log("개인 정보 읽기 실패 · 원본 유지: "+ex.Message); personal=new PersonalLibrary(PersonalLibrary.DefaultPath+".recovered-"+DateTime.Now.ToString("yyyyMMddHHmmss")); }
            RefreshCollections();
            HandleCreated+=delegate { if(Environment.GetCommandLineArgs().Any(x=>x.StartsWith("--")) || Environment.GetEnvironmentVariable("FONTPOCKET_TEST")=="1") return; hotkeyRegistered=RegisterHotKey(Handle,0x504f,0x4000|0x1|0x2,0x20); if(!hotkeyRegistered) Log("빠른 검색 단축키 Ctrl+Alt+Space를 등록하지 못했습니다. 앱에서 Ctrl+K를 사용하세요."); };
            HandleDestroyed+=delegate { if(hotkeyRegistered) { UnregisterHotKey(Handle,0x504f); hotkeyRegistered=false; } };
            FormClosed+=delegate { if(quickSearch!=null && !quickSearch.IsDisposed) quickSearch.Close(); };
        }
        protected override void WndProc(ref Message m) { if(m.Msg==0x312 && m.WParam.ToInt32()==0x504f) { BeginInvoke((Action)delegate { OpenQuickSearch(); }); return; } base.WndProc(ref m); }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData) { if(keyData==(Keys.Control|Keys.K)) { OpenQuickSearch();return true; } return base.ProcessCmdKey(ref msg,keyData); }
        bool MatchesPersonalFilters(ManagedFont f) {
            if(personal==null) return f.Matches(managerSearch.Text);
            string query=managerSearch.Text; bool text=f.Matches(query)||personal.SearchText(f).IndexOf(query.Trim(),StringComparison.OrdinalIgnoreCase)>=0;
            var selection=CurrentCollection; return text && (selection==null || selection.Id=="all" || personal.InCollection(f,selection.Id));
        }
        void RefreshCollections() {
            string previous=CurrentCollection==null?"all":CurrentCollection.Id;
            collectionFilter.BeginUpdate();var choices=new List<CollectionChoice> {new CollectionChoice { Id="all",Name="모든 폰트" },new CollectionChoice { Id="favorite",Name="★ 즐겨찾기" }};
            if(personal!=null) foreach(var c in personal.Data.Collections) choices.Add(new CollectionChoice { Id=c.Id,Name=(c.Smart?"자동 · ":"")+c.Name });collectionChoices=choices;collectionFilter.Items.Clear();foreach(var choice in choices) collectionFilter.Items.Add(choice.Name);
            collectionFilter.SelectedIndex=0; for(int i=0;i<collectionChoices.Count;i++) if(collectionChoices[i].Id==previous) collectionFilter.SelectedIndex=i; collectionFilter.EndUpdate();
        }
        void OpenDetails(ManagedFont font) {OpenDetails(font,this);}
        void OpenDetails(ManagedFont font,IWin32Window owner) { if(font==null) { status.Text="상세 정보를 볼 폰트를 선택하세요."; return; } try { using(var dialog=new FontDetailsDialog(font.Path,managedFonts)) dialog.ShowDialog(owner); } catch(Exception ex) { DialogUI.Error(owner,ex); } }
        ManagedFont HighlightedFont() { return managerList.FocusedItem!=null && managerList.FocusedItem.Selected?(ManagedFont)managerList.FocusedItem.Tag:managerList.SelectedItems.Count>0?(ManagedFont)managerList.SelectedItems[0].Tag:null; }
        void AddFavoriteAction(ContextMenuStrip menu) {
            menu.ShowCheckMargin=true;
            var item=new ToolStripMenuItem("즐겨찾기",null,delegate {ToggleFavorite(HighlightedFont());});
            menu.Items.Add(item);
            menu.Opening+=delegate {var font=HighlightedFont();item.Checked=font!=null && personal!=null && personal.Note(font).Favorite;};
        }
        void ToggleFavorite(ManagedFont font) {
            if(busy || font==null || personal==null) return;
            var note=personal.Note(font);bool previous=note.Favorite;note.Favorite=!previous;
            try {personal.Save();RefreshManagerList();status.Text=font.Name+(note.Favorite?" · 즐겨찾기에 추가했습니다.":" · 즐겨찾기를 해제했습니다.");}
            catch(Exception ex) {note.Favorite=previous;DialogUI.Error(this,ex);}
        }
        void EditFontNotes(ManagedFont font) {
            if(font==null || personal==null) return;
            var note=personal.Note(font);
            using(var form=DialogUI.Form("메모와 태그 · "+font.Name,600,550)) {
                var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=7 }; foreach(int h in new[]{24,34,24,130,24,130,40}) layout.RowStyles.Add(new RowStyle(h==130?SizeType.Percent:SizeType.Absolute,h==130?50:h)); form.Controls.Add(layout);

                layout.Controls.Add(new Label { Text="태그 · 쉼표로 구분",AutoSize=true }); var tags=new TextBox { Text=note.Tags,Dock=DockStyle.Fill,MaxLength=2000 }; layout.Controls.Add(tags);
                layout.Controls.Add(new Label { Text="메모",AutoSize=true }); var memo=new TextBox { Text=note.Memo,Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,MaxLength=16000 }; layout.Controls.Add(memo);
                layout.Controls.Add(new Label { Text="모음 · 자동 모음은 조건에 따라 포함됩니다.",AutoSize=true }); var collections=new CheckedListBox { Dock=DockStyle.Fill,CheckOnClick=true }; var manual=personal.Data.Collections.Where(c=>!c.Smart).ToList(); foreach(var c in manual) collections.Items.Add(c.Name,note.Collections.Contains(c.Id)); layout.Controls.Add(collections);
                var save=DialogUI.Button("저장",delegate { form.DialogResult=DialogResult.OK; },true); layout.Controls.Add(save);
                if(form.ShowDialog(this)!=DialogResult.OK) return;
                var old=note; var updated=new FontNote { Key=note.Key,Favorite=note.Favorite,Tags=String.Join(", ",tags.Text.Split(new[]{',',';','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()),Memo=memo.Text,Collections=collections.CheckedIndices.Cast<int>().Select(i=>manual[i].Id).ToList() };
                int index=personal.Data.Notes.IndexOf(old); personal.Data.Notes[index]=updated;
                try { personal.Save(); RefreshManagerList(); } catch(Exception ex) { personal.Data.Notes[index]=old; DialogUI.Error(this,ex); }
            }
        }
        void ManageCollections() {
            using(var form=DialogUI.Form("모음 관리",640,490)) {
                var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3 }; layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); form.Controls.Add(layout);
                layout.Controls.Add(new Label { Text="일반 모음에는 선택한 폰트를 담을 수 있습니다.\r\n자동 모음은 이름·태그·형식·상태 조건에 맞는 폰트를 모읍니다.",Dock=DockStyle.Fill }); var listbox=new ListBox { Dock=DockStyle.Fill }; layout.Controls.Add(listbox);
                Action refresh=delegate { listbox.Items.Clear(); foreach(var c in personal.Data.Collections) listbox.Items.Add(c.Name+(c.Smart?" · 자동: "+c.Query:"")); }; refresh();
                var tools=new FlowLayoutPanel { Dock=DockStyle.Fill }; layout.Controls.Add(tools);
                Action<bool> add=delegate(bool smart) { string name=DialogUI.Ask(form,smart?"자동 모음":"새 모음","모음 이름","",false); if(String.IsNullOrWhiteSpace(name)) return; string query=""; if(smart) { query=DialogUI.Ask(form,"자동 모음 조건","이름·태그·TTF·OTF·활성·비활성·가변 / 여러 조건은 ; 로 구분","",false); if(String.IsNullOrWhiteSpace(query)) return; } var c=new FontCollection { Name=name.Trim(),Smart=smart,Query=query }; personal.Data.Collections.Add(c); if(!smart) foreach(var f in ChosenManagedFonts()) personal.Note(f).Collections.Add(c.Id); try { personal.Save(); refresh(); RefreshCollections(); } catch(Exception ex) { personal.Data.Collections.Remove(c); foreach(var n in personal.Data.Notes) n.Collections.Remove(c.Id); DialogUI.Error(form,ex); } };
                tools.Controls.Add(DialogUI.Button("새 모음",delegate { add(false); })); tools.Controls.Add(DialogUI.Button("자동 모음",delegate { add(true); }));
                tools.Controls.Add(DialogUI.Button("수정",delegate { int i=listbox.SelectedIndex; if(i<0) return; var c=personal.Data.Collections[i]; string name=DialogUI.Ask(form,"모음 수정","모음 이름",c.Name,false); if(String.IsNullOrWhiteSpace(name)) return; string query=c.Smart?DialogUI.Ask(form,"조건 수정","조건은 ; 로 구분",c.Query,false):c.Query; if(query==null) return; string oldName=c.Name,oldQuery=c.Query; c.Name=name.Trim();c.Query=query; try { personal.Save();refresh();RefreshCollections();RefreshManagerList(); } catch(Exception ex) { c.Name=oldName;c.Query=oldQuery;DialogUI.Error(form,ex); } }));
                tools.Controls.Add(DialogUI.Button("모음 삭제",delegate { int i=listbox.SelectedIndex;if(i<0) return;var c=personal.Data.Collections[i]; if(LocalizedMessage.Show(form,"모음만 삭제합니다. 폰트 파일은 유지합니다.","모음 삭제",MessageBoxButtons.OKCancel)!=DialogResult.OK) return; personal.Data.Collections.Remove(c); try { personal.Save();refresh();RefreshCollections(); } catch(Exception ex) { personal.Data.Collections.Insert(i,c);DialogUI.Error(form,ex); } }));
                form.ShowDialog(this);
            } RefreshManagerList();
        }
        void OpenCatalog() { using(var form=new FontCatalogDialog(VisibleManagedFonts().ToList())) form.ShowDialog(this); }
        async Task ToggleCollection(bool enable) {
            var choice=CurrentCollection;if(busy || choice==null || choice.Id=="all") { status.Text="전환할 모음이나 즐겨찾기를 먼저 선택하세요.";return; }
            var targets=new HashSet<string>(VisibleManagedFonts().Where(f=>f.CanRemove && f.Disabled==enable).Select(f=>f.Path),StringComparer.OrdinalIgnoreCase);if(targets.Count==0) {status.Text="현재 모음에 전환할 폰트가 없습니다.";return;}
            await ToggleManagedFonts(enable,managedFonts.Where(f=>targets.Contains(f.Path)).ToList());
        }
        void OpenQueueDetails() { var rows=list.SelectedItems.Cast<ListViewItem>().ToList(); if(rows.Count==0) rows=list.CheckedItems.Cast<ListViewItem>().Take(1).ToList(); if(rows.Count==0) { status.Text="상세 정보를 볼 파일을 선택하세요.";return; }var item=(FontItem)rows[0].Tag;OpenDetails(new ManagedFont { Path=item.Path,Name=item.Name }); }
        async Task ValidateChosenFonts() {
            if(busy) return;var fonts=ChosenManagedFonts(); if(fonts.Count==0) { status.Text="검사할 폰트를 선택하세요.";return; } SetBusy(true);var results=new List<string>();
            try { await Task.Run(delegate { for(int i=0;i<fonts.Count && !cancelled;i++) { var f=fonts[i]; try { var metadata=FontMetadata.Read(f.Path); for(int face=0;face<metadata.Faces.Count;face++) { var info=OpenTypeDetails.Read(f.Path,face,true); results.Add(f.Name+" · 서체 "+(face+1)+"\r\n"+String.Join("\r\n",info.Checks.ToArray())); } Engine.Validate(f.Path);results.Add("통과 · Windows 메모리 로드\r\n"); } catch(Exception ex) { results.Add(f.Name+" · 오류: "+ex.Message+"\r\n"); } int n=i+1; UI(delegate { progress.Value=n*100/fonts.Count;status.Text=n+" / "+fonts.Count+" 검사 중"; }); } });
                status.Text=cancelled?"폰트 검사 중단":"폰트 검사 완료 · "+fonts.Count+"개";
                using(var form=DialogUI.Form("폰트 검사 결과",780,600)) { form.Controls.Add(DialogUI.ReadOnly(String.Join("\r\n",results.ToArray())+"\r\n파일 구조·체크섬·메모리 로드 검사입니다. 모든 렌더링 문제나 악성 코드를 판별하지는 않습니다."));form.ShowDialog(this); } Log("폰트 파일 검사 완료 · "+fonts.Count+"개");
            } catch(Exception ex) { DialogUI.Error(this,ex); } finally { SetBusy(false); }
        }
        void OpenQuickSearch() {
            if(quickSearch!=null && !quickSearch.IsDisposed) { quickSearch.Activate();return; }
            var form=DialogUI.Form("빠른 폰트 검색 · Ctrl+K",720,510); quickSearch=form;form.TopMost=true;var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3 };layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); form.Controls.Add(layout);
            var search=new TextBox { Dock=DockStyle.Fill };layout.Controls.Add(search);var results=new ListBox { Dock=DockStyle.Fill,IntegralHeight=false,HorizontalScrollbar=true };layout.Controls.Add(results);var tools=new FlowLayoutPanel { Dock=DockStyle.Fill };layout.Controls.Add(tools);
            Action refresh=delegate { var rows=managedFonts.Where(f=>f.Matches(search.Text)||(personal!=null && personal.SearchText(f).IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0)).Take(200).ToArray();results.DataSource=rows; };
            search.TextChanged+=delegate { refresh(); };results.DoubleClick+=delegate { var f=results.SelectedItem as ManagedFont;if(f!=null) OpenDetails(f,form); };
            tools.Controls.Add(DialogUI.Button("상세 정보",delegate { OpenDetails(results.SelectedItem as ManagedFont,form); }));
            tools.Controls.Add(DialogUI.Button("관리에서 보기",delegate { var f=results.SelectedItem as ManagedFont;if(f==null) return;form.Close();WindowState=FormWindowState.Normal;Show();Activate();ShowPage(3);managerSearch.Text="";onlyDuplicates.Checked=false;onlyDisabledFonts.Checked=false;showDisabledFonts.Checked=true;showSystemFonts.Checked=true;collectionFilter.SelectedIndex=0; foreach(ListViewItem r in managerList.Items) { r.Selected=((ManagedFont)r.Tag).Path==f.Path;if(r.Selected) r.EnsureVisible(); } }));
            tools.Controls.Add(DialogUI.Button("활성 상태 전환",async delegate { var f=results.SelectedItem as ManagedFont;if(f==null || busy) return; if(!f.CanRemove) { LocalizedMessage.Show(form,"시스템 폰트는 전환하지 않습니다.");return; }form.Close();await ToggleManagedFonts(f.Disabled,new List<ManagedFont> {f}); }));
            form.ShowInTaskbar=false;form.KeyPreview=true;form.KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.KeyCode==Keys.Escape) form.Close(); };
            form.Shown+=async delegate { if(!managerLoaded && !busy) await LoadManagedFonts();refresh();search.Focus(); }; form.FormClosed+=delegate { quickSearch=null;form.Dispose(); }; form.Show();form.Activate();search.Focus();
        }
        void OpenWindowsFontSettings() { try { Process.Start(new ProcessStartInfo("ms-settings:fonts") { UseShellExecute=true }); } catch(Exception ex) { DialogUI.Error(this,ex); } }
    }
}
