using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FontPocket {
    // Native tooltips skip disabled buttons. Show the same hint from the owning
    // form after a hover delay, without enabling the button or intercepting clicks.
    sealed class DisabledActionHints : IDisposable {
        readonly Form owner; readonly ToolTip tips; readonly Control[] targets;
        readonly Timer timer = new Timer(); Control hovered; string message; DateTime since; bool shown;
        internal DisabledActionHints(Form owner, ToolTip tips, IEnumerable<Control> targets) {
            this.owner=owner; this.tips=tips; this.targets=targets.ToArray();
            timer.Interval=120; timer.Tick+=delegate { Tick(); }; timer.Start();
        }
        void Tick() {
            Control next=null; Point position=Cursor.Position;
            if (!owner.IsDisposed && owner.Visible && Form.ActiveForm==owner && owner.WindowState!=FormWindowState.Minimized)
                next=targets.FirstOrDefault(c=>!c.IsDisposed && c.Visible && !c.Enabled && c.RectangleToScreen(c.ClientRectangle).Contains(position));
            string text=next==null?null:tips.GetToolTip(next);
            if(next!=hovered || text!=message) {
                if(shown && !owner.IsDisposed) tips.Hide(owner);
                hovered=next; message=text; since=DateTime.UtcNow; shown=false;
            }
            if(hovered!=null && !shown && !String.IsNullOrEmpty(message) && (DateTime.UtcNow-since).TotalMilliseconds>=600) {
                Point at=owner.PointToClient(position); at.Offset(12,20);
                tips.Show(message,owner,at,6000); shown=true;
            }
        }
        public void Dispose() { timer.Stop(); timer.Dispose(); if(!owner.IsDisposed) tips.Hide(owner); }
    }
    public partial class MainWindow {
        sealed class ActionRule {
            internal Control Control; internal ToolStripItem MenuItem;
            internal string Description; internal Func<string> Reason; internal bool Idle=true;
        }
        readonly List<ActionRule> actionRules=new List<ActionRule>();
        ToolTip actionTips; DisabledActionHints disabledActionHints;
        void BindAction(Control control,string description,Func<string> reason,bool idle) {
            actionRules.Add(new ActionRule { Control=control,Description=description,Reason=reason,Idle=idle });
        }
        void BindMenu(ContextMenuStrip menu,Func<string,string> reason,Func<string,string> description) {
            menu.ShowItemToolTips=true;
            foreach(ToolStripItem item in menu.Items) {
                if(item is ToolStripSeparator) continue;
                string text=item.Text;
                actionRules.Add(new ActionRule { MenuItem=item,Description=description(text),Reason=()=>reason(text) });
                item.Click+=delegate { RefreshActionAvailability(); };
            }
            menu.Opening+=delegate { RefreshActionAvailability(); };
        }
        static IEnumerable<Control> Descendants(Control root) {
            foreach(Control child in root.Controls) { yield return child; foreach(var nested in Descendants(child)) yield return nested; }
        }
        void InitializeActionAvailability() {
            actionTips=new ToolTip { InitialDelay=500,ReshowDelay=150,AutoPopDelay=6000,ShowAlways=true };
            for(int page=0;page<pages.Length;page++) {
                int section=page;
                foreach(var button in Descendants(pages[page]).OfType<Button>()) {
                    string text=button.Text;
                    BindAction(button,ActionDescription(section,text),()=>ActionReason(section,text),text!="중단");
                }
            }
            foreach(var button in nav) actionTips.SetToolTip(button,button.Text+" 화면으로 이동합니다.");
            disabledActionHints=new DisabledActionHints(this,actionTips,actionRules.Where(r=>r.Control!=null).Select(r=>r.Control));
            list.SelectedIndexChanged+=delegate { RefreshActionAvailability(); };
            managerList.SelectedIndexChanged+=delegate { RefreshActionAvailability(); };
            links.TextChanged+=delegate { RefreshActionAvailability(); };
            Disposed+=delegate { disabledActionHints.Dispose(); actionTips.Dispose(); };
            RefreshActionAvailability();
        }
        void RefreshActionAvailability() {
            if(actionTips==null || IsDisposed) return;
            foreach(var rule in actionRules) {
                string reason=busy && rule.Idle?"다른 작업이 끝난 뒤 사용할 수 있습니다.":rule.Reason();
                string hint=rule.Description+(reason==null?"":"\r\n"+reason);
                if(rule.Control!=null && !rule.Control.IsDisposed) {
                    rule.Control.Enabled=reason==null;
                    if(actionTips.GetToolTip(rule.Control)!=hint) actionTips.SetToolTip(rule.Control,UiLanguage.Text(hint));
                    rule.Control.AccessibleDescription=hint;
                } else if(rule.MenuItem!=null && !rule.MenuItem.IsDisposed) {
                    rule.MenuItem.Enabled=reason==null; rule.MenuItem.ToolTipText=UiLanguage.Text(hint);
                    rule.MenuItem.AccessibleDescription=hint;
                }
            }
        }
        string ActionReason(int section,string text) {
            if(text=="중단") return busy && !cancelled?null:busy?"작업을 중단하고 있습니다.":"진행 중인 작업이 없습니다.";
            if(section==3) return ManagerActionReason(text);
            if(section==1) {
                switch(text) {
                    case "선택한 폰트 백업": return backupFonts.Any(f=>f.Selected)?null:"백업할 폰트를 체크하세요.";
                    case "전체 선택": return backupFonts.Any(f=>!f.Selected)?null:"선택하지 않은 폰트가 없습니다.";
                    case "전체 해제": return backupFonts.Any(f=>f.Selected)?null:"체크한 폰트가 없습니다.";
                    case "검색 결과 선택": return backupFonts.Any(f=>f.Matches(backupSearch.Text) && !f.Selected)?null:"검색 결과에 선택하지 않은 폰트가 없습니다.";
                }
            }
            if(section==0) {
                switch(text) {
                    case "대기열에 추가": case "바로 설치": return LinkActionReason();
                    case "선택 제거": return list.SelectedItems.Count>0?null:"목록에서 제거할 파일 행을 선택하세요.";
                    case "목록 비우기": return items.Count>0?null:"대기열이 비어 있습니다.";
                    case "상세 정보": case "상세 정보 · 문자표": case "미리보기":
                        return list.SelectedItems.Count>0 || list.CheckedItems.Count>0?null:"미리 볼 폰트를 선택하거나 체크하세요.";
                    case "체크 폰트 비교": return ComparisonReason(list.CheckedItems.Cast<ListViewItem>().Select(r=>((FontItem)r.Tag).Path));
                    case "선택한 폰트 설치": return list.CheckedItems.Cast<ListViewItem>().Any(r=>((FontItem)r.Tag).Metadata!=null && Selection.CanInstall((FontItem)r.Tag,FormatMode,includeTtc.Checked))?null:"설치할 폰트를 체크하세요.";
                    case "체크한 파일 버전으로 보관": return list.CheckedItems.Count>0?null:"보관할 폰트 파일을 체크하세요.";
                }
            }
            return null;
        }
        string LinkActionReason() {
            var addresses=links.Text.Split(new[]{'\r','\n','\t',' '},StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
            if(addresses.Length==0) return "폰트 사이트나 다운로드 주소를 붙여넣으세요.";
            if(addresses.Length>30) return "주소는 한 번에 30개까지 넣을 수 있습니다.";
            foreach(string address in addresses) { try { NoonnuDownload.InputUri(address); } catch(InvalidDataException) { return "공개 HTTPS 폰트 주소를 입력하세요. https://는 생략할 수 있습니다."; } }
            return null;
        }
        static string ComparisonReason(IEnumerable<string> paths) {
            var files=paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if(files.Count<2) return "비교할 폰트를 2개 이상 선택하거나 체크하세요.";
            if(files.Count>12) return "한 번에 최대 12개까지 비교할 수 있습니다.";
            long bytes=0; foreach(var file in files) { try { bytes+=new FileInfo(file).Length; } catch { return "선택한 폰트 파일을 찾을 수 없습니다. 목록을 새로고침하세요."; } }
            return bytes>256L*1024*1024?"비교할 파일의 총 용량을 256MB 이하로 줄이세요.":null;
        }
        string ManagerActionReason(string text) {
            var highlighted=HighlightedFont();
            switch(text) {
                case "상세 정보": case "상세 정보 · 문자표": case "메모·태그": case "즐겨찾기": case "파일 위치 열기":
                    return highlighted!=null?null:"목록에서 폰트를 선택하세요.";
                case "버전 관리": return highlighted==null?"버전을 관리할 폰트를 선택하세요.":highlighted.Identity==null?"이 폰트의 식별 정보를 읽을 수 없습니다.":null;
                case "견본 보기": case "표시된 항목 선택": return managerList.Items.Count>0?null:"표시된 폰트가 없습니다.";
                case "중복 정리": return managedFonts.Count>1?null:"중복을 확인할 폰트가 2개 이상 필요합니다.";
                case "활성화": case "비활성화":
                    bool enable=text=="활성화";
                    return ChosenManagedFonts().Any(f=>f.CanRemove && f.Disabled==enable)?null:enable?"활성화할 내 계정의 비활성 폰트를 선택하세요.":"비활성화할 내 계정의 활성 폰트를 선택하세요.";
                case "비교": return ComparisonReason(ChosenManagedFonts().Select(f=>f.Path));
                case "폰트 파일 검사": case "견본·문자표 인쇄 / PDF": return ChosenManagedFonts().Count>0?null:"작업할 폰트를 선택하세요.";
                case "선택 백업": case "선택 해제": return managerList.SelectedItems.Count>0?null:"목록에서 폰트를 선택하세요.";
                case "선택 삭제": return ChosenManagedFonts().Any(f=>f.CanRemove)?null:"삭제할 내 계정 폰트를 선택하세요. 시스템 폰트는 보호됩니다.";
                case "현재 모음 활성화": case "현재 모음 비활성화":
                    if(CurrentCollection==null || CurrentCollection.Id=="all") return "모음이나 즐겨찾기를 먼저 선택하세요.";
                    bool activate=text=="현재 모음 활성화";
                    return VisibleManagedFonts().Any(f=>f.CanRemove && f.Disabled==activate)?null:"현재 모음에 상태를 전환할 내 계정 폰트가 없습니다.";
            }
            return null;
        }
        string ActionDescription(int section,string text) {
            switch(text) {
                case "상세 정보": case "상세 정보 · 문자표": return "선택한 폰트의 문자표, 지원 범위와 제작 정보를 봅니다.";
                case "견본 보기": return "현재 표시된 모든 폰트의 견본을 한 화면에서 봅니다.";
                case "중복 정리": return "중복 폰트를 찾고, 남길 폰트를 확인한 뒤 정리합니다.";
                case "활성화": return "선택한 비활성 폰트를 다시 사용할 수 있게 합니다.";
                case "비활성화": return "폰트 파일을 보관하고 사용을 중지합니다. 나중에 다시 활성화할 수 있습니다.";
                case "비교": case "체크 폰트 비교": return "선택한 폰트 2~12개를 같은 문장으로 비교합니다.";
                case "버전 관리": return "선택한 글꼴의 버전을 보관하고 비교하거나 전환합니다.";
                case "더 보기 ▾": return "메모, 모음, 검사, 백업과 삭제 등의 기능을 엽니다.";
                case "새로고침": return "설치된 폰트 목록과 상태를 다시 읽습니다.";
                case "중단": return "진행 중인 작업을 안전하게 중단합니다.";
                case "대기열에 추가": return "주소에서 폰트를 내려받아 설치 대기열에 추가합니다.";
                case "바로 설치": return "주소에서 폰트를 내려받고 선택한 형식으로 설치합니다.";
                case "파일 · ZIP 추가": return "폰트 파일이나 ZIP을 설치 대기열에 추가합니다.";
                case "폴더 추가": return "선택한 폴더와 하위 폴더의 폰트를 불러옵니다.";
                case "선택 제거": return "선택한 행을 대기열에서 제거합니다. 원본 파일은 유지합니다.";
                case "목록 비우기": return "설치 대기열을 비웁니다. 원본 파일은 유지합니다.";
                case "미리보기": return "선택한 파일을 설치 전에 미리 봅니다.";
                case "선택한 폰트 설치": return "대기열에서 체크한 폰트를 현재 계정에 설치합니다.";
                case "선택한 폰트 백업": return "체크한 폰트를 ZIP 파일로 백업합니다.";
                case "선택 백업": return "목록에서 선택한 폰트를 ZIP 파일로 백업합니다.";
                case "전체 선택": return "검색으로 숨겨진 폰트도 포함해 모두 체크합니다.";
                case "전체 해제": return "체크한 폰트의 체크를 모두 해제합니다.";
                case "선택 해제": return "목록에서 선택한 행을 모두 해제합니다.";
                case "검색 결과 선택": return "현재 검색 결과에 해당하는 폰트를 체크합니다.";
                case "메모·태그": return "선택한 폰트의 메모, 태그와 모음을 편집합니다.";
                case "즐겨찾기": return "선택한 폰트를 즐겨찾기에 추가하거나 해제합니다. 체크 표시는 등록 상태입니다.";
                case "모음 관리": return "폰트 모음과 조건에 따른 자동 모음을 만들고 편집합니다.";
                case "버전 보관함 · 미설치 파일": return "보관된 버전을 확인하고 설치하지 않은 파일도 추가합니다.";
                case "현재 모음 활성화": return "현재 모음에 표시된 내 계정의 비활성 폰트를 활성화합니다.";
                case "현재 모음 비활성화": return "현재 모음에 표시된 내 계정의 활성 폰트를 비활성화합니다.";
                case "빠른 검색  Ctrl+K": return "이름, 메모와 태그로 폰트를 빠르게 찾습니다.";
                case "패밀리로 묶기": return "목록의 폰트를 같은 글꼴 패밀리끼리 묶거나 해제합니다.";
                case "폰트 파일 검사": return "선택한 폰트의 파일 구조, 체크섬과 메모리 로드를 검사합니다.";
                case "견본·문자표 인쇄 / PDF": return "선택한 폰트의 견본이나 문자표를 인쇄하거나 PDF로 저장합니다.";
                case "선택 삭제": return "선택한 내 계정 폰트를 복구 ZIP으로 보관한 뒤 삭제합니다.";
                case "삭제 폰트 복원": return "이전에 저장한 복구 ZIP을 불러와 다시 설치할 수 있습니다.";
                case "환경 백업": return "폰트와 개인 정보, 활성 상태, 버전 보관함을 백업합니다.";
                case "환경 복원": return "저장한 환경 백업을 불러와 복원합니다.";
                case "Windows 글꼴 설정": return "Windows의 글꼴 설정을 엽니다.";
                case "파일 위치 열기": return "선택한 폰트 파일이 있는 폴더를 엽니다.";
                case "표시된 항목 선택": return "현재 필터에 표시된 폰트를 모두 선택합니다.";
                case "체크한 파일 버전으로 보관": return "대기열에서 체크한 파일을 버전 보관함에 추가합니다.";
            }
            return text+" 기능을 실행합니다.";
        }
    }
}
