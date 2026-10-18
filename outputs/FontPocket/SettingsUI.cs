using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FontPocket {
    public partial class MainWindow {
        Form settingsWindow;
        void ShowAppSettings() {
            if(busy || checkingAppUpdate)return;
            if(settingsWindow!=null && !settingsWindow.IsDisposed) {settingsWindow.Activate();return;}
            UpdateSettings preferences;
            try {preferences=UpdateSettings.Load();}catch(Exception ex) {DialogUI.Error(this,ex);return;}
            var form=DialogUI.Form("Font Pocket 설정",560,510);settingsWindow=form;
            form.FormBorderStyle=FormBorderStyle.FixedDialog;form.MaximizeBox=false;form.MinimizeBox=false;
            form.MinimumSize=Size.Empty;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=Padding.Empty};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,122));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,98));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,125));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));form.Controls.Add(layout);
            var languageGroup=new GroupBox {Text="언어",Dock=DockStyle.Fill,Padding=new Padding(12),Margin=new Padding(0,0,0,12)};layout.Controls.Add(languageGroup,0,0);
            var languageLayout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};languageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));languageLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));languageGroup.Controls.Add(languageLayout);
            var language=new ComboBox {Name="SettingsLanguage",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top};language.Items.AddRange(new object[]{"한국어","English"});language.SelectedIndex=UiLanguage.PreferredCode=="en"?1:0;languageLayout.Controls.Add(language,0,0);
            languageLayout.Controls.Add(new Label {Text="언어 변경은 앱을 다시 시작하면 적용됩니다.",AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(0,5,0,0)},0,1);
            var updateGroup=new GroupBox {Text="업데이트",Dock=DockStyle.Fill,Padding=new Padding(12),Margin=new Padding(0,0,0,12)};layout.Controls.Add(updateGroup,0,1);
            var automatic=new CheckBox {Name="SettingsAutomaticUpdates",Text="앱을 시작할 때 새 버전 자동 확인 (하루 한 번)",Checked=preferences.AutomaticCheck,AutoSize=true,Dock=DockStyle.Top};updateGroup.Controls.Add(automatic);
            var associations=new GroupBox {Text="폰트 파일 연결",Dock=DockStyle.Fill,Padding=new Padding(12),Margin=new Padding(0,0,0,12)};
            var associationLayout=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};associationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,39));associationLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));associations.Controls.Add(associationLayout);
            associationLayout.Controls.Add(new Label {Text="폰트 파일을 더블클릭해 Font Pocket에서 열 수 있습니다.\r\nWindows에서 기본 앱으로 Font Pocket을 선택해 주세요.",Dock=DockStyle.Fill},0,0);
            var associate=DialogUI.Button("기본 앱 설정",delegate {try {FileAssociations.OpenDefaults();}catch(Exception ex){DialogUI.Error(form,ex);}});associate.Name="FontFileAssociations";associate.Width=160;associationLayout.Controls.Add(associate,0,1);layout.Controls.Add(associations,0,2);
            layout.Controls.Add(new Label {Text="Font Pocket "+UpdateClient.CurrentVersion.ToString(3),AutoSize=true,Dock=DockStyle.Top,ForeColor=PocketTheme.Muted,Padding=new Padding(0,6,0,0)},0,3);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,3,0,0)};layout.Controls.Add(buttons,0,4);
            var close=DialogUI.Button("취소",delegate {form.Close();});close.DialogResult=DialogResult.Cancel;
            var save=DialogUI.Button("저장",delegate {
                try {
                    // Only save the preference owned by this window; retain last-check time.
                    var latest=UpdateSettings.Load();latest.AutomaticCheck=automatic.Checked;latest.Save();
                    UiLanguage.Save(language.SelectedIndex==1?"en":"ko");
                    form.Close();status.Text="설정을 저장했습니다.";
                }catch(Exception ex) {DialogUI.Error(form,ex);}
            });save.Name="SaveAppSettings";
            buttons.Controls.Add(close);buttons.Controls.Add(save);form.AcceptButton=save;form.CancelButton=close;
            form.FormClosed+=delegate {settingsWindow=null;form.Dispose();};form.Show(this);
        }
    }
}
