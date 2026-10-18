using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FontPocket {
    sealed class WebFontExportDialog : Form {
        readonly string path;readonly OpenTypeDetails details;
        public WebFontExportDialog(string file,OpenTypeDetails info) {
            path=file;details=info;Text="웹폰트 파일로 저장";Size=new Size(650,490);MinimumSize=new Size(610,460);StartPosition=FormStartPosition.CenterParent;Padding=new Padding(18);PocketTheme.Apply(this);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,45));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,102));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,28));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));Controls.Add(layout);
            layout.Controls.Add(new Label{Text="폰트 파일·CSS·예제 HTML을 ZIP으로 내보냅니다.",Dock=DockStyle.Fill});
            var modes=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
            var original=new RadioButton{Text="원본 TTF/OTF + CSS + HTML",AutoSize=true};
            var woff2=new RadioButton{Text="WOFF2로 변환 + CSS + HTML",AutoSize=true};var single=new RadioButton{Text="WOFF2 파일만 저장",AutoSize=true,Checked=true};modes.Controls.Add(single);modes.Controls.Add(original);modes.Controls.Add(woff2);layout.Controls.Add(modes);
            var license=DialogUI.ReadOnly(info.Name(13)+"\r\n\r\n"+info.Name(14));license.Tag="OriginalMetadata";layout.Controls.Add(license);
            var state=new Label{Text="폰트 라이선스에서 웹 배포·변환 허용 여부를 확인하세요.",Dock=DockStyle.Fill,AutoEllipsis=true};layout.Controls.Add(state);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty};layout.Controls.Add(buttons);
            var close=DialogUI.Button("닫기",delegate{Close();});var save=DialogUI.Button("파일 저장",async delegate {
                using(var picker=new SaveFileDialog{Filter=single.Checked?"WOFF2|*.woff2":"ZIP|*.zip",FileName=single.Checked?"font.woff2":"webfont.zip",DefaultExt=single.Checked?"woff2":"zip",OverwritePrompt=true}) {
                    if(picker.ShowDialog(this)!=DialogResult.OK)return;
                    buttons.Enabled=false;modes.Enabled=false;state.Text=UiLanguage.Text("웹폰트를 준비하는 중…");
                    try {byte[] converted=(single.Checked||woff2.Checked)?await Woff2Converter.ConvertAsync(this,path):null;if(IsDisposed)return;if(single.Checked)await Task.Run(()=>WebFontPackage.SaveWoff2(converted,picker.FileName));else await Task.Run(()=>WebFontPackage.Create(path,details,converted,picker.FileName));if(!IsDisposed)state.Text=UiLanguage.Text("웹폰트 파일을 저장했습니다.");}
                    catch(Exception ex){if(!IsDisposed){state.Text=UiLanguage.Text("웹폰트 내보내기 실패");DialogUI.Error(this,ex);}}
                    finally{if(!IsDisposed){buttons.Enabled=true;modes.Enabled=true;}}
                }
            });buttons.Controls.Add(close);buttons.Controls.Add(save);CancelButton=close;
        }
    }
    static class WebFontPackage {
        internal static void ValidateInput(string path) {
            string ext=Path.GetExtension(path).ToLowerInvariant();
            if(ext!=".ttf"&&ext!=".otf")throw new NotSupportedException("웹폰트 내보내기는 단일 TTF/OTF 파일을 지원합니다. TTC는 지원하지 않습니다.");
            if(new FileInfo(path).Length>128L*1024*1024)throw new IOException("웹폰트 내보내기는 128MB까지 지원합니다.");
        }
        internal static void Create(string path,OpenTypeDetails info,byte[] converted,string destination) {
            ValidateInput(path);string ext=converted==null?Path.GetExtension(path).ToLowerInvariant():".woff2";
            if(converted!=null&&(converted.Length<48||Encoding.ASCII.GetString(converted,0,4)!="wOF2"))throw new InvalidDataException("WOFF2 변환 결과가 올바르지 않습니다.");
            string css=BuildCss(path,info,ext,"./fonts/font"+ext);
            string html="<!doctype html><html lang='en'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><link rel='stylesheet' href='style.css'><title>"+WebUtility.HtmlEncode(info.Family)+"</title><body><h1>"+WebUtility.HtmlEncode(info.Family)+"</h1><p class='font-pocket' style='font-size:36px;white-space:pre-wrap'>"+WebUtility.HtmlEncode(FontSamples.Default)+"</p></body></html>";
            string temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create)){
                    if(converted==null)zip.CreateEntryFromFile(path,"fonts/font"+ext,CompressionLevel.Optimal);else using(var target=zip.CreateEntry("fonts/font.woff2").Open())target.Write(converted,0,converted.Length);
                    Text(zip,"style.css",css);Text(zip,"index.html",html);
                    Text(zip,"FONT-LICENSE.txt",info.Name(0)+"\r\n\r\n"+info.Name(13)+"\r\n\r\n"+info.Name(14));
                    foreach(string licenseName in new[]{"OFL.txt","LICENSE","LICENSE.txt","LICENSE.md","LICENCE.txt"}){string licenseFile=Path.Combine(Path.GetDirectoryName(path),licenseName);if(File.Exists(licenseFile)&&new FileInfo(licenseFile).Length<1024*1024)zip.CreateEntryFromFile(licenseFile,"licenses/"+licenseName);}
                    Text(zip,"README.txt","Extract this ZIP and open index.html. Keep style.css beside index.html and preserve the fonts folder.\r\nUse the font-pocket CSS class in your website. Review FONT-LICENSE.txt and the publisher's license before publishing or converting fonts.\r\n\r\n압축을 풀고 index.html을 여세요. 웹사이트에는 style.css와 fonts 폴더를 함께 올리고 font-pocket 클래스를 적용하세요. 게시·변환 가능 여부는 원본 폰트 라이선스를 확인하세요.\r\nNo subsetting or glyph changes were requested; variable fonts retain their axes. CSS uses their weight range when available.\r\n");
                }
                if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);
            }finally{if(File.Exists(temp))File.Delete(temp);}
        }
        internal static string BuildCss(string path,OpenTypeDetails info,string ext,string url) {
            int staticWeight=FontMetadata.Read(path).Faces[0].Weight;
            string weight=staticWeight>0?staticWeight.ToString(CultureInfo.InvariantCulture):(info.StyleFlags&1)!=0?"700":"400";var axis=info.Axes.FirstOrDefault(a=>a.Tag=="wght");if(axis!=null)weight=axis.Min.ToString(CultureInfo.InvariantCulture)+" "+axis.Max.ToString(CultureInfo.InvariantCulture);
            string format=ext==".woff2"?"woff2":ext==".otf"?"opentype":"truetype";
            var width=info.Axes.FirstOrDefault(a=>a.Tag=="wdth");string stretch=width==null?"":"  font-stretch: "+width.Min.ToString(CultureInfo.InvariantCulture)+"% "+width.Max.ToString(CultureInfo.InvariantCulture)+"%;\n";
            string quotedUrl=CssString(url),family=CssString(info.Family);
            return "@font-face {\n  font-family: "+family+";\n  src: url("+quotedUrl+") format('"+format+"');\n  font-weight: "+weight+";\n"+stretch+"  font-style: "+((info.StyleFlags&2)!=0?"italic":"normal")+";\n  font-display: swap;\n}\n\n.font-pocket { font-family: "+family+", sans-serif; }\n";
        }
        static string CssString(string value){return "\""+value.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\d ").Replace("\n","\\a ")+"\"";}
        internal static void SaveWoff2(byte[] bytes,string destination) {
            if(bytes==null||bytes.Length<48||Encoding.ASCII.GetString(bytes,0,4)!="wOF2")throw new InvalidDataException("WOFF2 변환 결과가 올바르지 않습니다.");
            string temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        static void Text(ZipArchive zip,string name,string text){using(var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false)))writer.Write(text);}
    }
    static class Woff2Converter {
        internal static async Task<byte[]> ConvertAsync(Form owner,string path) {
            return await ConvertCoreAsync(owner,path,false);
        }
        internal static async Task<byte[]> DecompressAsync(Form owner,string path) {
            byte[] header=new byte[48];using(var file=File.OpenRead(path)){if(file.Read(header,0,48)!=48||Encoding.ASCII.GetString(header,0,4)!="wOF2")throw new InvalidDataException("WOFF2 헤더가 올바르지 않습니다.");}
            uint size=((uint)header[16]<<24)|((uint)header[17]<<16)|((uint)header[18]<<8)|header[19];if(size<12||size>FontImport.Limit)throw new InvalidDataException("WOFF2 변환 크기가 너무 큽니다.");
            return await ConvertCoreAsync(owner,path,true);
        }
        static async Task<byte[]> ConvertCoreAsync(Form owner,string path,bool decompress) {
            if(!decompress)WebFontPackage.ValidateInput(path);
            string operation=decompress?"decompress":"compress";
            string html="<!doctype html><meta charset='utf-8'><p>"+WebUtility.HtmlEncode(UiLanguage.Text(decompress?"설치용 폰트로 변환하는 중…":"WOFF2로 변환하는 중…"))+"</p><script type='module'>import{"+operation+"}from './woff2-encoder.js';window.convertState='running';try{const input=new Uint8Array(await(await fetch('./input.font')).arrayBuffer());const output=await "+operation+"(input);let result='';for(let i=0;i<output.length;i+=32768)result+=String.fromCharCode(...output.subarray(i,i+32768));window.convertResult=btoa(result);window.convertState='done';}catch(e){window.convertError=String(e);window.convertState='error';}</script>";
            using(var form=DialogUI.Form(decompress?"설치용 폰트 변환":"웹폰트로 사용",500,200))using(var view=new EmbeddedFontView(html){Dock=DockStyle.Fill}) {
                Directory.CreateDirectory(view.WorkFolder);File.Copy(path,Path.Combine(view.WorkFolder,"input.font"));
                File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"WebAssets","woff2-encoder.js"),Path.Combine(view.WorkFolder,"woff2-encoder.js"));
                form.Controls.Add(view);form.Show(owner);await view.StartAsync();var core=view.Browser.CoreWebView2;var serializer=new JavaScriptSerializer();DateTime deadline=DateTime.UtcNow.AddMinutes(3);
                while(true){if(form.IsDisposed||owner.IsDisposed)throw new OperationCanceledException();if(DateTime.UtcNow>deadline)throw new IOException("WOFF2 변환 제한 시간을 넘었습니다.");string state=await core.ExecuteScriptAsync("window.convertState || 'starting'");if(state=="\"done\"")break;if(state=="\"error\"")throw new InvalidDataException(serializer.Deserialize<string>(await core.ExecuteScriptAsync("window.convertError")));await Task.Delay(100);}
                int length=serializer.Deserialize<int>(await core.ExecuteScriptAsync("window.convertResult.length"));if(length>256*1024*1024)throw new IOException("WOFF2 변환 결과가 너무 큽니다.");
                var encoded=new StringBuilder(length);for(int start=0;start<length;start+=262144){if(form.IsDisposed||owner.IsDisposed)throw new OperationCanceledException();encoded.Append(serializer.Deserialize<string>(await core.ExecuteScriptAsync("window.convertResult.slice("+start+","+Math.Min(start+262144,length)+")")));}
                form.Close();return Convert.FromBase64String(encoded.ToString());
            }
        }
    }
}
