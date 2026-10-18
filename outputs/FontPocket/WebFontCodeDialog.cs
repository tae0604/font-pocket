using System;
using System.Drawing;
using System.Windows.Forms;

namespace FontPocket {
    sealed class WebFontCodeDialog : Form {
        public WebFontCodeDialog(string path,OpenTypeDetails info) {
            Text="웹폰트로 사용";Size=new Size(760,590);MinimumSize=new Size(680,500);Padding=new Padding(18);StartPosition=FormStartPosition.CenterParent;PocketTheme.Apply(this);
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));Controls.Add(layout);
            layout.Controls.Add(new Label {Text="폰트 파일을 웹사이트에 올린 뒤 아래 경로를 맞추세요.\r\n폰트 파일은 ‘웹폰트 파일로 저장’에서 만들 수 있습니다.",Dock=DockStyle.Fill});
            var row=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,Margin=Padding.Empty};row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,80));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));layout.Controls.Add(row);
            row.Controls.Add(new Label {Text="폰트 URL",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft});var url=new TextBox {Text="./fonts/font.woff2",Dock=DockStyle.Fill,Name="WebFontUrl"};row.Controls.Add(url);var format=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};format.Items.AddRange(new object[]{"WOFF2","TTF","OTF"});format.SelectedIndex=0;row.Controls.Add(format);
            var code=DialogUI.ReadOnly("");code.Name="WebFontCss";code.Tag="OriginalMetadata";code.WordWrap=false;code.ScrollBars=ScrollBars.Both;code.Font=new Font("Consolas",10);layout.Controls.Add(code);
            layout.Controls.Add(new Label {Text="CSS를 적용하고 HTML 요소에 class=\"font-pocket\"을 추가하세요.\r\n게시 전 폰트의 웹 배포 라이선스를 확인하세요.",Dock=DockStyle.Fill});
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty};layout.Controls.Add(buttons);buttons.Controls.Add(DialogUI.Button("닫기",delegate {Close();}));var copy=DialogUI.Button("CSS 복사",delegate {Clipboard.SetText(code.Text);});buttons.Controls.Add(copy);
            Action refresh=delegate {Uri absolute;string value=url.Text.Trim();bool valid=value.Length>0&&!(Uri.TryCreate(value,UriKind.Absolute,out absolute)&&absolute.Scheme!="https")&&value.IndexOfAny(new[]{'\r','\n','\0'})<0;copy.Enabled=valid;code.Text=valid?WebFontPackage.BuildCss(path,info,format.SelectedIndex==0?".woff2":format.SelectedIndex==1?".ttf":".otf",value).Replace("\n","\r\n"):UiLanguage.Text("HTTPS 주소 또는 웹사이트의 상대 경로를 넣어 주세요.");};url.TextChanged+=delegate {refresh();};format.SelectedIndexChanged+=delegate {refresh();};refresh();
        }
    }
}
