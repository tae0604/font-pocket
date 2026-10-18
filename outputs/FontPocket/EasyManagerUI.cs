using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FontPocket {
    public partial class MainWindow {
        TableLayoutPanel managerPage;Control managerBasicFilters,managerAdvancedFilters;
        Button managerFilterToggle;bool managerDetailed,managerFiltersOpen,applyingManagerColumns;
        TableLayoutPanel importPage;Control importFormatCard;Button importOptionsToggle;bool importOptionsOpen;
        void SetImportOptionsVisible(bool open) {
            importOptionsOpen=open;if(importPage==null||importFormatCard==null)return;
            importPage.SuspendLayout();importFormatCard.Visible=open;importPage.RowStyles[2].Height=open?50:0;importPage.ResumeLayout();
            importOptionsToggle.Text=UiLanguage.Text("설치 옵션")+(open?" ▴":" ▾");
        }
        void ToggleManagerFilters(){SetManagerFiltersVisible(!managerFiltersOpen);}
        void SimplifyManagerMenu(ContextMenuStrip menu) {
            var original=menu.Items.Cast<ToolStripItem>().ToArray();menu.Items.Clear();
            Action<string> keep=delegate(string title){var item=original.FirstOrDefault(i=>i.Text==title);if(item!=null)menu.Items.Add(item);};
            Action<string,string[]> group=delegate(string title,string[] names){var branch=new ToolStripMenuItem(title);foreach(string name in names){var item=original.FirstOrDefault(i=>i.Text==name);if(item!=null)branch.DropDownItems.Add(item);}menu.Items.Add(branch);};
            keep("즐겨찾기");keep("메모·태그");keep("중복 정리");menu.Items.Add(new ToolStripSeparator());
            group("사용 설정",new[]{"활성화","비활성화","현재 모음 활성화","현재 모음 비활성화"});
            group("모음·버전",new[]{"모음 관리","버전 관리","버전 보관함 · 미설치 파일"});
            group("백업·복원",new[]{"선택 백업","삭제 폰트 복원","환경 백업","환경 복원"});
            group("도구",new[]{"견본 보기","폰트 파일 검사","견본·문자표 인쇄 / PDF"});
            group("보기",new[]{"자세한 목록 보기","패밀리로 묶기"});
            menu.Items.Add(new ToolStripSeparator());keep("선택 삭제");
            foreach(var item in original)if(item.Owner==null)item.Dispose();
        }
        void SetManagerFiltersVisible(bool open) {
            managerFiltersOpen=open;if(managerPage==null)return;managerPage.SuspendLayout();
            managerBasicFilters.Visible=open;managerAdvancedFilters.Visible=open;managerPage.RowStyles[1].Height=open?40:0;managerPage.RowStyles[2].Height=open?40:0;managerPage.ResumeLayout();UpdateManagerFilterCaption();
        }
        void UpdateManagerFilterCaption() {
            if(managerFilterToggle==null)return;
            bool active=managerWeight.SelectedIndex>0||managerLanguage.SelectedIndex>0||managerVariant.SelectedIndex>0||managerFormat.SelectedIndex>0||collectionFilter.SelectedIndex>0||showSystemFonts.Checked||!showDisabledFonts.Checked||onlyDisabledFonts.Checked||onlyDuplicates.Checked;
            managerFilterToggle.Text=UiLanguage.Text(active?"필터 · 적용 중":"필터")+(managerFiltersOpen?" ▴":" ▾");
        }
        void ApplyManagerColumns() {
            if(applyingManagerColumns||managerList.IsDisposed||managerList.Columns.Count<8)return;applyingManagerColumns=true;
            try {previewPath.Visible=managerDetailed;int[] widths=managerDetailed?new[]{235,60,70,80,65,115,85,100}:new[]{Math.Max(280,managerList.ClientSize.Width-250),0,0,0,90,0,0,159};
                for(int i=0;i<widths.Length;i++)if(managerList.Columns[i].Width!=widths[i])managerList.Columns[i].Width=widths[i];
            }finally{applyingManagerColumns=false;}
        }
    }
}
