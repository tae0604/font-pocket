using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FontPocket {
    sealed class VariableAxisDialog : Form {
        readonly EmbeddedFontView view;
        readonly TextBox sample=new TextBox {Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Text=FontSamples.Default};
        readonly NumericUpDown size=new NumericUpDown {Minimum=12,Maximum=160,Value=48,Width=75};
        readonly Dictionary<string,NumericUpDown> values=new Dictionary<string,NumericUpDown>();
        readonly Timer updateTimer=new Timer {Interval=70};bool ready;
        readonly Label heading=new Label {Dock=DockStyle.Fill,AutoEllipsis=true};
        public VariableAxisDialog(string path,OpenTypeDetails details) {
            Text="가변 축 조절";Size=new Size(1000,760);MinimumSize=new Size(780,600);Padding=new Padding(18);StartPosition=FormStartPosition.CenterParent;PocketTheme.Apply(this);
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,30));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,76));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Math.Min(210,44*(details.Axes.Count+1))));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(layout);
            heading.Text=details.Family;heading.Tag="OriginalMetadata";layout.Controls.Add(heading);layout.Controls.Add(sample);
            var options=new Panel {Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty};var rows=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,Margin=Padding.Empty,ColumnCount=3,RowCount=details.Axes.Count+1,Padding=Padding.Empty};rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));options.Controls.Add(rows);layout.Controls.Add(options);
            AddRow(rows,0,"크기",12,160,48,size);
            int index=1;foreach(var axis in details.Axes){var number=new NumericUpDown {Minimum=(decimal)axis.Min,Maximum=(decimal)axis.Max,Value=(decimal)axis.Default,DecimalPlaces=2,Increment=0.01m,Width=95,Name="AxisValue_"+axis.Tag};values.Add(axis.Tag,number);AddRow(rows,index++,Label(axis.Tag),axis.Min,axis.Max,axis.Default,number);}
            view=new EmbeddedFontView(CanvasHtml(path)){Dock=DockStyle.Fill};layout.Controls.Add(view);
            sample.TextChanged+=delegate {Schedule();};size.ValueChanged+=delegate {Schedule();};
            updateTimer.Tick+=async delegate {updateTimer.Stop();try{await RenderAsync();}catch(ObjectDisposedException){}catch(InvalidOperationException){}catch(System.Runtime.InteropServices.COMException ex){ready=false;if(!IsDisposed)heading.Text=ex.Message;}};
            Shown+=async delegate {try {await view.StartAsync();ready=true;await RenderAsync();}catch { }};
        }
        void AddRow(TableLayoutPanel rows,int row,string caption,double min,double max,double initial,NumericUpDown number) {
            rows.RowStyles.Add(new RowStyle(SizeType.Absolute,44));rows.Controls.Add(new Label {Text=caption,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);
            var slider=new TrackBar {Minimum=0,Maximum=1000,Value=max==min?0:(int)Math.Round((initial-min)/(max-min)*1000),Dock=DockStyle.Fill,TickStyle=TickStyle.None,AutoSize=false,Height=32,Margin=new Padding(0,6,10,0),Enabled=max>min};rows.Controls.Add(slider,1,row);number.Anchor=AnchorStyles.Left;rows.Controls.Add(number,2,row);bool synchronizing=false;
            slider.ValueChanged+=delegate {if(synchronizing)return;synchronizing=true;number.Value=Math.Min(number.Maximum,Math.Max(number.Minimum,(decimal)(min+(max-min)*slider.Value/1000.0)));synchronizing=false;Schedule();};
            number.ValueChanged+=delegate {if(synchronizing)return;synchronizing=true;slider.Value=max==min?0:Math.Max(0,Math.Min(1000,(int)Math.Round(((double)number.Value-min)/(max-min)*1000)));synchronizing=false;Schedule();};
        }
        void Schedule(){if(!IsDisposed){updateTimer.Stop();updateTimer.Start();}}
        async Task RenderAsync(){if(!ready||IsDisposed||view.IsDisposed)return;var json=new JavaScriptSerializer();var axes=new Dictionary<string,double>();foreach(var pair in values)axes.Add(pair.Key,(double)pair.Value.Value);await view.Browser.CoreWebView2.ExecuteScriptAsync("setPreview("+json.Serialize(sample.Text)+","+size.Value.ToString(CultureInfo.InvariantCulture)+","+json.Serialize(axes)+")");}
        static string Label(string tag){string name=tag=="wght"?"굵기":tag=="wdth"?"글자 너비":tag=="opsz"?"광학 크기":tag=="GRAD"?"획 농도":tag=="slnt"?"기울기":tag=="ital"?"기울임 전환":"";return name==""?tag:UiLanguage.Text(name)+" ("+tag+")";}
        static string CanvasHtml(string path){if(Path.GetExtension(path).Equals(".ttc",StringComparison.OrdinalIgnoreCase))throw new NotSupportedException(UiLanguage.Text("TTC 가변 축 조절은 지원하지 않습니다."));if(new FileInfo(path).Length>128L*1024*1024)throw new IOException(UiLanguage.Text("가변 미리보기는 128MB까지 지원합니다."));return "<!doctype html><meta charset='utf-8'><style>@font-face{font-family:pocket;src:url(data:font/ttf;base64,"+Convert.ToBase64String(File.ReadAllBytes(path))+")}body{margin:0;padding:12px;background:white;color:#232830}#sample{font-family:pocket;font-size:48px;line-height:1.5;white-space:pre-wrap;overflow-wrap:anywhere}</style><div id='sample'></div><script>function setPreview(text,size,axes){const s=document.querySelector('#sample');s.textContent=text;s.style.fontSize=size+'px';s.style.fontVariationSettings=Object.entries(axes).map(([tag,value])=>'\"'+tag+'\" '+value).join(',')}</script>";}
        protected override void Dispose(bool disposing){if(disposing)updateTimer.Dispose();base.Dispose(disposing);}
    }
}
