using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    public static class FontVersionSwitch {
        public static void Use(ArchivedVersion version,VersionLibrary vault,List<ManagedFont> fonts,IFontActivationStore store,ActivationRepository repository,Action<string> install,Action<string> validate) {
            string path=vault.FileFor(version); if(Engine.Hash(path)!=version.Hash) throw new IOException("보관된 버전의 내용이 변경됐습니다."); validate(path);
            var target=new ManagedFont { Path=path };FontLibrary.ReadMetadata(target); if(target.Identity!=version.Identity) throw new IOException("보관한 폰트 정보가 일치하지 않습니다.");
            var same=fonts.Where(f=>f.Identity==version.Identity).ToList(); var active=same.Where(f=>!f.Disabled).ToList();
            foreach(var f in active) if(!f.CanRemove && Engine.Hash(f.Path)!=version.Hash) throw new InvalidOperationException("같은 글꼴의 시스템 폰트가 활성화되어 있습니다. 시스템 폰트는 자동 교체하지 않습니다.");
            foreach(var f in same) vault.Capture(f.Path);
            bool already=active.Any(f=>Engine.Hash(f.Path)==version.Hash);
            var disabled=new List<DisabledFont>();
            try {
                foreach(var f in active.Where(f=>f.CanRemove && Engine.Hash(f.Path)!=version.Hash)) disabled.Add(FontActivation.Disable(f,store,repository));
                if(!already) { var existing=same.FirstOrDefault(f=>f.Disabled && Engine.Hash(f.Path)==version.Hash); if(existing!=null) FontActivation.Enable(existing.DisabledRecord,store,repository); else install(path); }
            } catch(Exception ex) {
                var errors=new List<string>(); foreach(var record in disabled) try { FontActivation.Enable(record,store,repository); } catch(Exception rollback) { errors.Add(rollback.Message); }
                if(errors.Count>0) throw new IOException("버전 전환 실패. 복원하지 못한 항목은 비활성 목록과 보관함에 남겼습니다: "+String.Join("; ",errors.ToArray()),ex); throw;
            }
        }
    }
    public partial class MainWindow {
        async Task OpenVersionManager(ManagedFont selected) {
            if(busy) return; if(selected!=null && selected.Identity==null) { status.Text="폰트 식별 정보를 읽을 수 없습니다.";return; }
            var vault=VersionLibrary.Default;
            try {
                SetBusy(true); if(selected!=null) await Task.Run(delegate { foreach(var f in managedFonts.Where(f=>f.Identity==selected.Identity)) vault.Capture(f.Path); });
            } catch(Exception ex) { DialogUI.Error(this,ex);return; } finally { SetBusy(false); }
            using(var form=DialogUI.Form(selected==null?"폰트 버전 보관함":"폰트 버전 관리 · "+selected.Name,960,600)) {
                var layout=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=4,ColumnCount=1 };layout.RowStyles.Add(new RowStyle(SizeType.Absolute,60));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));form.Controls.Add(layout);
                layout.Controls.Add(new Label { Text="같은 글꼴·스타일의 버전을 비교하고 사용할 버전을 선택합니다.\r\n기존 버전은 삭제하지 않고 보관·비활성화합니다. 버전 번호만으로 최신 여부를 단정하지 않습니다.",Dock=DockStyle.Fill });
                var query=new TextBox { Dock=DockStyle.Top };layout.Controls.Add(query);
                var listbox=new ListView { Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=true,HideSelection=false };listbox.Columns.Add("버전",160);listbox.Columns.Add("형식",70);listbox.Columns.Add("상태",110);listbox.Columns.Add("SHA-256",190);listbox.Columns.Add("글꼴",330);layout.Controls.Add(listbox);
                Action refresh=delegate { listbox.Items.Clear(); foreach(var v in vault.ReadAll().Where(v=>(selected==null || v.Identity==selected.Identity) && (v.Name+" "+v.Version+" "+v.Format).IndexOf(query.Text,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(v=>v.Name).ThenBy(v=>v.Version,StringComparer.OrdinalIgnoreCase)) { string state=managedFonts.Any(f=>!f.Disabled && f.Identity==v.Identity && Engine.Hash(f.Path)==v.Hash)?"활성":"보관";var row=new ListViewItem(v.Version) { Tag=v };row.SubItems.Add(v.Format);row.SubItems.Add(state);row.SubItems.Add(v.Hash.Substring(0,20));row.SubItems.Add(v.Name);listbox.Items.Add(row); } };try { refresh(); } catch(Exception ex) { DialogUI.Error(this,ex);return; }
                query.TextChanged+=delegate { try { refresh(); }catch(Exception ex) {DialogUI.Error(form,ex);} };
                var tools=new FlowLayoutPanel { Dock=DockStyle.Fill };layout.Controls.Add(tools);
                tools.Controls.Add(DialogUI.Button("버전 파일 추가",delegate { using(var pick=new OpenFileDialog { Filter="폰트 / ZIP|*.ttf;*.otf;*.ttc;*.zip",Multiselect=true }) { if(pick.ShowDialog(form)!=DialogResult.OK) return; try { foreach(string path in pick.FileNames) { var files=Path.GetExtension(path).Equals(".zip",StringComparison.OrdinalIgnoreCase)?Engine.Extract(path,Path.Combine(tempRoot,Guid.NewGuid().ToString("N"))):new List<string>{path};foreach(string file in files) vault.Capture(file); } refresh(); } catch(Exception ex) { DialogUI.Error(form,ex); } } }));
                tools.Controls.Add(DialogUI.Button("선택 버전 비교",delegate { var sources=listbox.SelectedItems.Cast<ListViewItem>().Select(r=>(ArchivedVersion)r.Tag).Select(v=>new FontSource { Path=vault.FileFor(v),Name=v.Name+" · "+v.Version,Origin="버전 보관함" }).ToList();OpenComparison(sources,"폰트 버전 비교",FontSamples.Default); }));
                tools.Controls.Add(DialogUI.Button("상세 정보",delegate { if(listbox.SelectedItems.Count==1) { var v=(ArchivedVersion)listbox.SelectedItems[0].Tag;OpenDetails(new ManagedFont { Path=vault.FileFor(v) }); } }));
                tools.Controls.Add(DialogUI.Button("메모·태그",delegate { if(listbox.SelectedItems.Count==1) { var v=(ArchivedVersion)listbox.SelectedItems[0].Tag;var font=new ManagedFont {Path=vault.FileFor(v)};FontLibrary.ReadMetadata(font);EditFontNotes(font); } }));
                var use=DialogUI.Button("이 버전 사용",async delegate {
                    if(listbox.SelectedItems.Count!=1 || busy) return;var v=(ArchivedVersion)listbox.SelectedItems[0].Tag;
                    if(LocalizedMessage.Show(form,"선택한 버전을 활성화하고 같은 글꼴·스타일의 다른 내 계정 버전을 비활성화할까요?\r\n기존 파일은 보관합니다. 열려 있는 앱에는 재실행 후 반영될 수 있습니다.","버전 전환",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
                    layout.Enabled=false;SetBusy(true);
                    try { await Task.Run(delegate { FontVersionSwitch.Use(v,vault,managedFonts,new WindowsFontActivationStore(),ActivationRepository.Default,p=>Engine.Install(new FontItem(p),Engine.InstalledHashes(s=>UI(delegate {Log(s);}))),p=>Engine.Validate(p)); Engine.Notify(); }); Log(v.Name+" · 버전 전환 완료 · "+v.Version);managerLoaded=false;backupLoaded=false; }
                    catch(Exception ex) { Log("버전 전환 오류: "+ex.Message);DialogUI.Error(form,ex);managerLoaded=false; }
                    finally { SetBusy(false);layout.Enabled=true; }
                    await LoadManagedFonts();try {refresh();}catch(Exception ex) {DialogUI.Error(form,ex);}
                },true);tools.Controls.Add(use);
                form.FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(busy) e.Cancel=true; };form.ShowDialog(this);
            }
        }
        void KeepQueuedVersions() {
            if(busy) return;var selected=list.CheckedItems.Cast<ListViewItem>().Select(r=>(FontItem)r.Tag).ToList();if(selected.Count==0) { status.Text="보관할 폰트를 체크하세요.";return; }
            try { foreach(var item in selected) VersionLibrary.Default.Capture(item.Path);status.Text=selected.Count+"개 파일을 버전 보관함에 저장했습니다.";Log(status.Text); } catch(Exception ex) { DialogUI.Error(this,ex); }
        }
    }
}
