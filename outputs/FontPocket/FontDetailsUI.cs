using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    static class DialogUI {
        public static Button Button(string title,EventHandler action,bool primary=false) { var b=new PocketButton { Text=title,Primary=primary,AutoSize=false,Size=new Size(110,34),Margin=new Padding(0,0,8,0) }; b.Click+=action; return b; }
        public static Form Form(string title,int width,int height) { var form=new Form { Text=title,Size=new Size(width,height),MinimumSize=new Size(Math.Min(width,700),Math.Min(height,480)),StartPosition=FormStartPosition.CenterParent,Padding=new Padding(18) };PocketTheme.Apply(form);return form; }
        public static TextBox ReadOnly(string text) { return new TextBox { Text=text,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BackColor=Color.White,BorderStyle=BorderStyle.None }; }
        public static string Ask(IWin32Window owner,string title,string label,string initial,bool multiline) {
            using(var form=Form(title,540,multiline?340:190)) {
                var layout=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=3,ColumnCount=1 }; layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
                layout.Controls.Add(new Label { Text=label,Dock=DockStyle.Fill }); var input=new TextBox { Text=initial,Dock=DockStyle.Fill,Multiline=multiline,MaxLength=16000 }; layout.Controls.Add(input);
                var ok=Button("저장",delegate { form.DialogResult=DialogResult.OK; },true); layout.Controls.Add(ok); form.Controls.Add(layout); if(!multiline) form.AcceptButton=ok;
                return form.ShowDialog(owner)==DialogResult.OK?input.Text:null;
            }
        }
        public static void Error(IWin32Window owner,Exception ex) { LocalizedMessage.Show(owner,ex.Message,"작업을 완료하지 못했습니다",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
    }
    class CharacterGrid : ScrollableControl {
        OpenTypeDetails details; List<int> characters=new List<int>(); PreviewFont preview; int columns=10,cell=68; public Action<int> Picked;
        public CharacterGrid() { DoubleBuffered=true; AutoScroll=true; BackColor=Color.White; SetStyle(ControlStyles.ResizeRedraw,true); }
        public void SetFont(string path,OpenTypeDetails info,string family,FontStyle? style) { if(preview!=null) preview.Dispose(); preview=null; details=info; try { preview=new PreviewFont(path,23,family,style); } catch { } Filter("",0,0x10ffff); }
        public void Filter(string query,int first,int last,Func<int,bool> extra=null) { int code; string value=(query??"").Trim(); bool numeric=Int32.TryParse(value.Replace("U+","").Replace("u+",""),System.Globalization.NumberStyles.HexNumber,null,out code) && (value.StartsWith("U+",StringComparison.OrdinalIgnoreCase) || value.Length>=4); characters=details.Characters.Keys.Where(c=>c>=first && c<=last && (extra==null || extra(c)) && (String.IsNullOrWhiteSpace(value) || Char.ConvertFromUtf32(c).Contains(value) || (numeric && c==code))).ToList(); AutoScrollPosition=Point.Empty; ResizeGrid(); }
        void ResizeGrid() {cell=(int)Math.Ceiling(68*DeviceDpi/96.0); columns=Math.Max(1,(ClientSize.Width-SystemInformation.VerticalScrollBarWidth)/cell); AutoScrollMinSize=new Size(0,((characters.Count+columns-1)/columns)*cell); Invalidate(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); ResizeGrid(); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;float scale=DeviceDpi/96f; int offset=AutoScrollPosition.Y,first=Math.Max(0,-offset/cell),last=Math.Min((characters.Count+columns-1)/columns,first+ClientSize.Height/cell+2);
            using(var border=new Pen(PocketTheme.Border)) using(var ink=new SolidBrush(PocketTheme.Ink)) using(var muted=new SolidBrush(PocketTheme.Muted)) {
                for(int row=first;row<last;row++) for(int col=0;col<columns;col++) { int i=row*columns+col; if(i>=characters.Count) break; var r=new Rectangle(col*cell,row*cell+offset,cell,cell); e.Graphics.DrawRectangle(border,r); string c=Char.ConvertFromUtf32(characters[i]); if(preview!=null) e.Graphics.DrawString(c,preview.Font,ink,new RectangleF(r.X+6*scale,r.Y+2*scale,cell-8*scale,42*scale)); else e.Graphics.DrawString("—",Font,ink,r.X+8*scale,r.Y+6*scale); e.Graphics.DrawString("U+"+characters[i].ToString("X4"),Font,muted,r.X+4*scale,r.Y+46*scale); }
            }
        }
        protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); int index=((e.Y-AutoScrollPosition.Y)/cell)*columns+e.X/cell; if(index>=0 && index<characters.Count && Picked!=null) Picked(characters[index]); }
        protected override void Dispose(bool disposing) { if(disposing && preview!=null) preview.Dispose(); base.Dispose(disposing); }
    }
    public class FontDetailsDialog : Form {
        sealed class FaceChoice {
            public string Path,Label,Family; public int Index,Weight;
            public override string ToString() {return Label;}
        }
        string path; OpenTypeDetails details; TabControl tabs=new PocketTabs(); ComboBox faces=new ComboBox(); Label summary=new Label();
        TextBox inspectionText; int currentFaceIndex,inspectionGeneration; bool inspectionStarted;
        public FontDetailsDialog(string file) : this(file,null) { }
        internal string SelectedFile {get{return path;}}
        internal void AddFileChoices(IEnumerable<ManagedFont> files) {
            var known=new HashSet<string>(faces.Items.Cast<FaceChoice>().Select(c=>Path.GetFullPath(c.Path)),StringComparer.OrdinalIgnoreCase);
            faces.BeginUpdate();try {foreach(var font in files) {
                if(font.Metadata==null||!known.Add(Path.GetFullPath(font.Path)))continue;
                for(int i=0;i<font.Metadata.Faces.Count;i++) {var face=font.Metadata.Faces[i];faces.Items.Add(new FaceChoice {Path=font.Path,Index=i,Family=face.Family,Weight=face.Weight,Label=face.Family+" · "+face.Style+"  ["+Path.GetExtension(font.Path).TrimStart('.').ToUpperInvariant()+"] · "+(String.IsNullOrEmpty(font.Name)?Path.GetFileName(font.Path):font.Name)});}
            }}finally{faces.EndUpdate();}
            faces.AccessibleName=UiLanguage.Text("열린 폰트 파일 및 서체");UpdateSummary();
        }
        public FontDetailsDialog(string file,IEnumerable<ManagedFont> related) {
            path=file; Text="폰트 상세 정보"; Size=new Size(970,760); MinimumSize=new Size(780,580); StartPosition=FormStartPosition.CenterParent; Font=new Font("맑은 고딕",9.5f); BackColor=Color.FromArgb(249,250,251); Padding=new Padding(18);
            PocketTheme.Apply(this);
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3 }; layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); Controls.Add(layout);
            faces.DropDownStyle=ComboBoxStyle.DropDownList; faces.Dock=DockStyle.Fill;
            var meta=FontMetadata.Read(path);var families=new HashSet<string>(meta.Faces.Select(f=>f.FamilyKey));
            var files=new List<ManagedFont> {new ManagedFont {Path=file,Metadata=meta}};
            if(related!=null) files.AddRange(related.Where(f=>f.Metadata!=null && f.Metadata.Faces.Any(face=>families.Contains(face.FamilyKey))));
            var choices=new List<FaceChoice>();
            foreach(var font in files.GroupBy(f=>Path.GetFullPath(f.Path),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()))
                for(int i=0;i<font.Metadata.Faces.Count;i++) {
                    var face=font.Metadata.Faces[i];if(!String.Equals(font.Path,file,StringComparison.OrdinalIgnoreCase) && !families.Contains(face.FamilyKey)) continue;
                    choices.Add(new FaceChoice {Path=font.Path,Index=i,Family=face.Family,Weight=face.Weight,
                        Label=face.Family+" · "+face.Style+"  ["+Path.GetExtension(font.Path).TrimStart('.').ToUpperInvariant()+"]"+(font.Disabled?" · 비활성":"")+" · "+Path.GetFileName(font.Path)});
                }
            foreach(var choice in choices.OrderBy(c=>c.Family,StringComparer.CurrentCultureIgnoreCase).ThenBy(c=>c.Weight).ThenBy(c=>c.Label,StringComparer.CurrentCultureIgnoreCase)) faces.Items.Add(choice);
            faces.AccessibleName="같은 패밀리의 폰트 및 파일 내 서체";layout.Controls.Add(faces);
            summary.Dock=DockStyle.Fill; summary.ForeColor=Color.DimGray; layout.Controls.Add(summary); tabs.Dock=DockStyle.Fill; layout.Controls.Add(tabs);
            faces.SelectedIndexChanged+=delegate { try {var choice=(FaceChoice)faces.SelectedItem;path=choice.Path;LoadFace(choice.Index); } catch(Exception ex) { DialogUI.Error(this,ex); } };
            tabs.SelectedIndexChanged+=delegate {InspectSelectedFace();};
            faces.SelectedIndex=faces.Items.Cast<FaceChoice>().ToList().FindIndex(c=>String.Equals(c.Path,file,StringComparison.OrdinalIgnoreCase) && c.Index==0);
        }
        void UpdateSummary() {if(details==null)return;
            summary.Text=faces.Items.Count+"개 선택 가능 · "+(details.CmapSupported?details.Characters.Count.ToString("N0")+"개 문자":"문자 수 확인 불가")+" · "+details.GlyphCount.ToString("N0")+"개 글리프 · 이 파일 "+details.FaceCount+"개 서체";
        }
        void LoadFace(int index) {
            currentFaceIndex=index;inspectionGeneration++;inspectionStarted=false;inspectionText=null;
            details=OpenTypeDetails.Read(path,index,false); foreach(TabPage page in tabs.TabPages.Cast<TabPage>().ToArray()) { tabs.TabPages.Remove(page); page.Dispose(); }
            UpdateSummary();
            var preview=Page("미리보기"); var previewLayout=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=2,ColumnCount=1 }; previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); preview.Controls.Add(previewLayout);
            string previewFamily=details.FaceCount>1?details.Name(1):null;FontStyle? previewStyle=details.FaceCount>1?(FontStyle?)details.StyleFlags:null;
            var previewSource=new FontSource {Path=path,Family=previewFamily,Style=previewStyle,Name=details.Family+" "+details.Subfamily,Origin="원본 파일 미리보기"};
            previewLayout.Controls.Add(new FontPreviewTile(previewSource,FontSamples.Default,28) { Dock=DockStyle.Fill });
            var previewTools=new FlowLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0) };
            previewTools.Controls.Add(DialogUI.Button("문구·크기 변경",delegate { using(var compare=new FontComparison(new List<FontSource> { previewSource },FontSamples.Default,"폰트 미리보기")) compare.ShowDialog(this); }));
            if(details.Axes.Count>0) previewTools.Controls.Add(DialogUI.Button("가변 축 조절",delegate { try { VariablePreview.Open(this,path,details); } catch(Exception ex) { DialogUI.Error(this,ex); } })); previewLayout.Controls.Add(previewTools);
            var chars=Page("문자표"); var charLayout=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=3,ColumnCount=1 }; charLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); charLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); charLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); chars.Controls.Add(charLayout);
            var bar=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false }; var query=new TextBox { Width=200 }; var ranges=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=170 }; ranges.Items.AddRange(new object[]{"모든 문자","한글","라틴·숫자","한자","일본어","기호·이모지"}); ranges.SelectedIndex=0; bar.Controls.Add(new Label { Text="문자 / U+코드",AutoSize=true,Margin=new Padding(0,5,8,0) }); bar.Controls.Add(query); bar.Controls.Add(ranges); var controls=new CheckBox { Text="제어 문자 포함",AutoSize=true,Margin=new Padding(12,3,0,0) };bar.Controls.Add(controls);charLayout.Controls.Add(bar);
            var grid=new CharacterGrid { Dock=DockStyle.Fill }; grid.SetFont(path,details,previewFamily,previewStyle); charLayout.Controls.Add(grid); var selected=new TextBox { ReadOnly=true,Width=420 }; string picked="";
            var bottom=new FlowLayoutPanel { Dock=DockStyle.Fill }; bottom.Controls.Add(selected); bottom.Controls.Add(DialogUI.Button("문자 복사",delegate { if(picked.Length>0) Clipboard.SetText(picked); })); charLayout.Controls.Add(bottom);
            grid.Picked=delegate(int c) { picked=Char.ConvertFromUtf32(c); selected.Text=picked+"  U+"+c.ToString("X4")+" · 글리프 "+details.Characters[c]; };
            Action filter=delegate { grid.Filter(query.Text,0,0x10ffff,delegate(int c) { if(!controls.Checked && (c<32 || (c>=0x7f && c<=0x9f))) return false; switch(ranges.SelectedIndex) { case 1:return (c>=0xac00 && c<=0xd7a3)||(c>=0x1100 && c<=0x11ff)||(c>=0x3130 && c<=0x318f)||(c>=0xa960 && c<=0xa97f)||(c>=0xd7b0 && c<=0xd7ff);case 2:return c<=0x24f;case 3:return (c>=0x3400 && c<=0x9fff)||(c>=0xf900 && c<=0xfaff)||(c>=0x20000 && c<=0x323af);case 4:return c>=0x3040 && c<=0x30ff;case 5:return (c>=0x2000 && c<=0x2bff)||(c>=0x1f000 && c<=0x1faff);default:return true; } }); }; query.TextChanged+=delegate { filter(); }; ranges.SelectedIndexChanged+=delegate { filter(); };controls.CheckedChanged+=delegate { filter(); };filter();
            var languages=Page("지원 언어·문자 범위"); var languageLayout=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=2,ColumnCount=1 }; languageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,60)); languageLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); languages.Controls.Add(languageLayout);
            languageLayout.Controls.Add(new Label { Text="실제 Unicode 문자 매핑을 기준으로 확인합니다.\r\n문자 집합 충족은 언어 전체 지원을 보장하지 않으며 조합·문맥 처리는 별도입니다.",Dock=DockStyle.Fill,ForeColor=Color.DimGray });
            var coverage=new ListView { Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,BorderStyle=BorderStyle.None }; coverage.Columns.Add("언어 / 문자 집합",300); coverage.Columns.Add("포함 / 기준",180); coverage.Columns.Add("결과",180); foreach(var c in details.Coverage()) { var row=new ListViewItem(c.Name); row.SubItems.Add(c.Present.ToString("N0")+" / "+c.Total.ToString("N0")); row.SubItems.Add(c.Status); coverage.Items.Add(row); } languageLayout.Controls.Add(coverage);
            var info=Page("정보"); var text=new StringBuilder(); string[] labels={"저작권","패밀리","스타일","고유 식별자","전체 이름","버전","PostScript 이름","상표","제작사","디자이너","설명","제작사 URL","디자이너 URL","라이선스","라이선스 URL"}; for(int i=0;i<labels.Length;i++) text.AppendLine(UiLanguage.Text(labels[i])+"\r\n"+(i==1?details.Family:i==2?details.Subfamily:details.Name(i))+"\r\n"); text.AppendLine(UiLanguage.Text("Windows 호환 패밀리")+"\r\n"+details.Name(1)+"\r\n\r\n"+UiLanguage.Text("Windows 호환 스타일")+"\r\n"+details.Name(2)+"\r\n"); text.AppendLine(UiLanguage.Text("파일")+"\r\n"+path+"\r\n\r\n"+UiLanguage.Text("크기")+"\r\n"+new FileInfo(path).Length.ToString("N0")+" bytes"); foreach(var axis in details.Axes) text.AppendLine("\r\n가변 축 "+axis.Tag+" · "+axis.Min+" / "+axis.Default+" / "+axis.Max); var metadataText=DialogUI.ReadOnly(text.ToString());metadataText.Tag="OriginalMetadata";info.Controls.Add(metadataText);
            var checks=Page("검사");inspectionText=DialogUI.ReadOnly(UiLanguage.Text("검사 탭을 열면 파일 검사가 시작됩니다."));inspectionText.Name="FontInspectionResults";checks.Controls.Add(inspectionText);
            PocketTheme.StyleTree(tabs);
        }
        async void InspectSelectedFace() {
            if(tabs.SelectedIndex!=4 || inspectionText==null || inspectionStarted)return;
            inspectionStarted=true;var target=inspectionText;string file=path;int face=currentFaceIndex,generation=inspectionGeneration;target.Text=UiLanguage.Text("파일 검사 중…");
            string report;
            try {report=await Task.Run(delegate {var result=OpenTypeDetails.Read(file,face,true);try {Engine.Validate(file);result.Checks.Add("통과 · Windows 메모리 로드");}catch(Exception ex){result.Checks.Add("실패 · Windows 메모리 로드 · "+ex.Message);}return String.Join("\r\n",result.Checks.ToArray())+"\r\n\r\n파일 구조·체크섬·로드 검사이며 모든 렌더링 문제를 검사하지는 않습니다.";});}
            catch(Exception ex){report="검사 실패\r\n"+ex.Message;}
            if(!IsDisposed && !target.IsDisposed && generation==inspectionGeneration)target.Text=UiLanguage.Text(report);
        }
        TabPage Page(string title) { var p=new TabPage(title) { Padding=new Padding(12),BackColor=Color.White }; tabs.TabPages.Add(p); return p; }
    }
    static class VariablePreview {
        internal static string BuildHtml(string path,OpenTypeDetails details) {
            if(new FileInfo(path).Length>128L*1024*1024) throw new IOException("가변 미리보기는 128MB까지 지원합니다.");
            if(Path.GetExtension(path).Equals(".ttc",StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("TTC 가변 축 조절은 지원하지 않습니다.");
            var html=new StringBuilder("<!doctype html><html lang='ko'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Font Pocket · 가변 폰트</title><style>@font-face{font-family:pocket;src:url(data:font/ttf;base64,");
            html.Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(")}body{margin:18px auto;padding:0 24px;max-width:1000px;background:#f9fafb;color:#202630;font:15px system-ui}textarea{box-sizing:border-box;width:100%;padding:12px;border:1px solid #ddd;border-radius:5px}#sample{font-family:pocket;font-size:48px;line-height:1.5;white-space:pre-wrap;overflow-wrap:anywhere;background:white;padding:24px}label{display:flex;gap:16px;margin:18px 0;align-items:center}input[type=range]{flex:1}h1{font-size:22px}</style><h1>");
            html.Append(WebUtility.HtmlEncode(details.Name(1))).Append("</h1><p>").Append(WebUtility.HtmlEncode(UiLanguage.Text("폰트를 설치하지 않고 프로그램 안에서 가변 축을 조절합니다."))).Append("</p><textarea id='text' rows='3'>").Append(WebUtility.HtmlEncode(FontSamples.Default)).Append("</textarea><label>").Append(WebUtility.HtmlEncode(UiLanguage.Text("크기"))).Append(" <input id='size' type='range' min='12' max='120' value='48'><output>48</output></label>");
            foreach(var a in details.Axes) html.Append("<label>").Append(WebUtility.HtmlEncode(AxisLabel(a.Tag))).Append("<input class='axis' data-tag='").Append(WebUtility.HtmlEncode(a.Tag)).Append("' type='range' step='0.01' min='").Append(a.Min.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("' max='").Append(a.Max.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("' value='").Append(a.Default.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("'><output></output></label>");
            html.Append("<div id='sample'></div><script>const s=document.querySelector('#sample');function update(){s.textContent=document.querySelector('#text').value;s.style.fontSize=document.querySelector('#size').value+'px';s.style.fontVariationSettings=Array.from(document.querySelectorAll('.axis')).map(x=>'\"'+x.dataset.tag+'\" '+x.value).join(',');document.querySelectorAll('input').forEach(x=>x.nextElementSibling.textContent=x.value)}document.addEventListener('input',update);update();</script></html>");
            return html.ToString();
        }
        static string AxisLabel(string tag) {string label=tag=="wght"?"굵기":tag=="wdth"?"글자 너비":tag=="opsz"?"광학 크기":tag=="GRAD"?"획 농도":tag=="slnt"?"기울기":tag=="ital"?"기울임 전환":"";return tag+(label==""?"":" · "+UiLanguage.Text(label));}
        public static void Open(Form owner,string path,OpenTypeDetails details) {
            var form=new VariableAxisDialog(path,details);form.FormClosed+=delegate {form.Dispose();};form.Show(owner);
        }
    }
}

