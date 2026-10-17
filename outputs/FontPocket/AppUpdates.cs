using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FontPocket {
    public class UpdateSettings {
        public string LastCheckUtc="";
        [ScriptIgnore] public string Repository {get {return OfficialUpdateSource.Repository;}}
        public bool AutomaticCheck=true;
        public static string SettingsPath {get {return Path.Combine(DataPaths.Root,"Updates","settings.json");}}
        public static UpdateSettings Load() {
            string file=SettingsPath;
            if(!File.Exists(file)) return new UpdateSettings();
            if(new FileInfo(file).Length>65536)throw new InvalidDataException("업데이트 설정이 너무 큽니다.");
            var settings=new JavaScriptSerializer().Deserialize<UpdateSettings>(File.ReadAllText(file));
            if(settings==null)throw new InvalidDataException("업데이트 설정을 읽을 수 없습니다.");
            return settings;
        }
        public void Save() {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            string temp=SettingsPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));if(File.Exists(SettingsPath))File.Replace(temp,SettingsPath,null);else File.Move(temp,SettingsPath);}
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
    public class GitHubAsset {public string name,browser_download_url,digest;public long size;}
    public class GitHubRelease {public string tag_name,body;public bool draft,prerelease;public List<GitHubAsset> assets;}
    public class AppUpdate {public Version Version;public string Notes,Url,Hash,ChecksumUrl,FileName;public long Size;}
    public static class UpdateClient {
        public static Version CurrentVersion {get {return typeof(UpdateClient).Assembly.GetName().Version;}}
        public static string Repository(string value) {
            value=(value??"").Trim().TrimEnd('/');if(value.Length==0)return "";
            if(value.StartsWith("https://github.com/",StringComparison.OrdinalIgnoreCase))value=value.Substring(19);
            if(!Regex.IsMatch(value,@"\A[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]+\z") || value.Split('/')[1]=="." || value.Split('/')[1]=="..")
                throw new ArgumentException("GitHub 저장소를 소유자/저장소 또는 https://github.com/소유자/저장소 형식으로 입력하세요.");
            return value;
        }
        public static Version ParseVersion(string tag) {
            tag=(tag??"").Trim();if(tag.StartsWith("v",StringComparison.OrdinalIgnoreCase))tag=tag.Substring(1);
            Version value;if(!Regex.IsMatch(tag,@"\A\d+\.\d+\.\d+(?:\.\d+)?\z") || !Version.TryParse(tag,out value))throw new InvalidDataException("릴리스 버전 태그를 확인하세요. 예: v3.1.18");
            return new Version(value.Major,value.Minor,value.Build,Math.Max(0,value.Revision));
        }
        static void ValidateAsset(string url,string repository,string name) {
            Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri) || uri.Scheme!="https" || uri.Host!="github.com" || !uri.IsDefaultPort || uri.UserInfo!="" || uri.Query!="" || uri.Fragment!="" ||
                !uri.AbsolutePath.StartsWith("/"+repository+"/releases/download/",StringComparison.OrdinalIgnoreCase) ||
                Uri.UnescapeDataString(uri.Segments.Last())!=name)throw new InvalidDataException("설정한 GitHub 저장소의 릴리스 파일이 아닙니다.");
        }
        public static AppUpdate Parse(string json,string repository,Version current) {
            repository=Repository(repository);if(repository.Length==0)throw new ArgumentException("GitHub 저장소를 먼저 설정하세요.");
            var release=new JavaScriptSerializer {MaxJsonLength=2*1024*1024}.Deserialize<GitHubRelease>(json);
            if(release==null)throw new InvalidDataException("릴리스 정보를 읽을 수 없습니다.");
            if(release.draft || release.prerelease)return null;
            var version=ParseVersion(release.tag_name);if(version<=current)return null;
            string file="FontPocket-Setup-"+version.ToString(version.Revision==0?3:4)+".exe";
            var matches=(release.assets??new List<GitHubAsset>()).Where(a=>a!=null && a.name==file).ToList();
            if(matches.Count!=1)throw new InvalidDataException("릴리스에 "+file+" 파일을 올려 주세요.");
            var asset=matches[0];ValidateAsset(asset.browser_download_url,repository,file);
            if(asset.size<2 || asset.size>256L*1024*1024)throw new InvalidDataException("업데이트 설치 파일 크기가 올바르지 않습니다.");
            string hash=asset.digest??"";hash=hash.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)?hash.Substring(7):"";
            var update=new AppUpdate {Version=version,Notes=release.body??"",Url=asset.browser_download_url,FileName=file,Size=asset.size};
            if(Regex.IsMatch(hash,@"\A[0-9a-fA-F]{64}\z"))update.Hash=hash.ToLowerInvariant();
            else {
                var checksum=(release.assets??new List<GitHubAsset>()).SingleOrDefault(a=>a!=null && a.name==file+".sha256");
                if(checksum==null)throw new InvalidDataException("GitHub SHA-256 정보가 없습니다. 설치 파일과 같은 이름의 .sha256 파일도 올려 주세요.");
                ValidateAsset(checksum.browser_download_url,repository,checksum.name);update.ChecksumUrl=checksum.browser_download_url;
            }
            return update;
        }
        static HttpWebRequest Request(string url) {
            var request=(HttpWebRequest)WebRequest.Create(url);request.UserAgent="FontPocket/"+CurrentVersion.ToString(3);request.Accept=url.StartsWith("https://api.github.com/",StringComparison.OrdinalIgnoreCase)?"application/vnd.github+json":"application/octet-stream";
            request.Headers["X-GitHub-Api-Version"]="2022-11-28";request.Timeout=15000;request.ReadWriteTimeout=15000;request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
            return request;
        }
        static string ReadText(string url,int limit) {
            using(var response=Request(url).GetResponse())using(var stream=response.GetResponseStream())using(var reader=new StreamReader(stream,Encoding.UTF8)) {
                var result=new StringBuilder();var buffer=new char[4096];int n;while((n=reader.Read(buffer,0,buffer.Length))>0){if(result.Length+n>limit)throw new InvalidDataException("업데이트 응답이 너무 큽니다.");result.Append(buffer,0,n);}return result.ToString();
            }
        }
        public static AppUpdate Check(UpdateSettings settings) {
            string repository=Repository(settings.Repository);if(repository.Length==0)return null;
            var update=Parse(ReadText("https://api.github.com/repos/"+repository+"/releases/latest",2*1024*1024),repository,CurrentVersion);
            if(update!=null && update.Hash==null) {
                string text=ReadText(update.ChecksumUrl,1024).Trim();var match=Regex.Match(text,@"\A([0-9a-fA-F]{64})(?:\s+\*?([^\r\n]+))?\z");
                if(!match.Success || (match.Groups[2].Success && match.Groups[2].Value!=update.FileName))throw new InvalidDataException("업데이트 SHA-256 파일 형식이 올바르지 않습니다.");update.Hash=match.Groups[1].Value.ToLowerInvariant();
            }
            return update;
        }
        public static void CopyVerified(Stream source,string target,long size,string hash,Func<bool> cancel,Action<int> progress) {
            if(size<2 || size>256L*1024*1024 || !Regex.IsMatch(hash??"",@"\A[0-9a-fA-F]{64}\z"))throw new InvalidDataException("업데이트 검증 정보가 올바르지 않습니다.");
            string partial=target+".part";try {
                using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    var buffer=new byte[65536];long total=0;int n;while((n=source.Read(buffer,0,buffer.Length))>0){if(cancel())throw new OperationCanceledException();total+=n;if(total>size)throw new InvalidDataException("업데이트 파일 크기가 일치하지 않습니다.");output.Write(buffer,0,n);progress((int)(total*100/size));}if(total!=size)throw new InvalidDataException("업데이트 다운로드가 완료되지 않았습니다.");
                }
                if(cancel())throw new OperationCanceledException();
                if(!String.Equals(Engine.Hash(partial),hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("업데이트 파일의 SHA-256이 일치하지 않습니다.");
                using(var file=File.OpenRead(partial))if(file.ReadByte()!=77 || file.ReadByte()!=90)throw new InvalidDataException("Windows 설치 파일이 아닙니다.");
                File.Move(partial,target);
            }finally {if(File.Exists(partial))File.Delete(partial);}
        }
        public static string Download(AppUpdate update,Func<bool> cancel,Action<int> progress) {
            string official=Repository(OfficialUpdateSource.Repository);
            if(official.Length==0)throw new InvalidOperationException("공식 업데이트 서버가 아직 연결되지 않았습니다.");
            ValidateAsset(update.Url,official,update.FileName);
            string directory=Path.Combine(DataPaths.Root,"Updates","Downloads");Directory.CreateDirectory(directory);
            string target=Path.Combine(directory,Guid.NewGuid().ToString("N")+"-"+update.FileName);
            using(var response=Request(update.Url).GetResponse())using(var stream=response.GetResponseStream())CopyVerified(stream,target,update.Size,update.Hash,cancel,progress);
            return target;
        }
        public static string InstallArguments(string directory) {
            if(!Path.IsPathRooted(directory) || directory.IndexOf('"')>=0 || directory.Any(c=>c<' '))throw new ArgumentException("설치 경로가 올바르지 않습니다.");
            return "/SILENT /NORESTART /CLOSEAPPLICATIONS /FONTPOCKETUPDATE=1 /LANG="+(UiLanguage.Code=="en"?"english":"korean")+" /DIR=\""+Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+"\"";
        }
    }
    public partial class MainWindow {
        bool checkingAppUpdate;Form updatesWindow;
        void InitializeAppUpdates(Panel sidebar) {
            var button=new Button {Text="업데이트",FlatStyle=FlatStyle.System,UseVisualStyleBackColor=true,Location=new Point(18,sidebar.Height-80),Size=new Size(90,28),Anchor=AnchorStyles.Left|AnchorStyles.Bottom};
            button.Click+=delegate {if(!busy)ShowAppUpdates();};sidebar.Controls.Add(button);sidebar.Resize+=delegate {button.Top=sidebar.Height-80;};
            BindAction(button,"공식 배포처에서 앱 업데이트를 확인합니다.",()=>busy?"현재 작업이 끝난 뒤 확인하세요.":null,true);
            var language=new Button {Text="언어",FlatStyle=FlatStyle.System,UseVisualStyleBackColor=true,Location=new Point(116,sidebar.Height-80),Size=new Size(82,28),Anchor=AnchorStyles.Left|AnchorStyles.Bottom};
            sidebar.Controls.Add(language);language.Click+=delegate {ShowLanguageSettings();};sidebar.Resize+=delegate {language.Top=sidebar.Height-80;};
            BindAction(language,"언어 설정",()=>busy?"현재 작업이 끝난 뒤 확인하세요.":null,true);
            Shown+=async delegate {
                if(Environment.GetEnvironmentVariable("FONTPOCKET_TEST")=="1" || Environment.GetCommandLineArgs().Any(a=>a.StartsWith("--")))return;
                try {
                    var settings=UpdateSettings.Load();DateTime last;if(!settings.AutomaticCheck || settings.Repository.Length==0 || (DateTime.TryParse(settings.LastCheckUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out last) && DateTime.UtcNow-last.ToUniversalTime()<TimeSpan.FromHours(24)))return;
                    checkingAppUpdate=true;var update=await Task.Run(()=>UpdateClient.Check(settings));settings.LastCheckUtc=DateTime.UtcNow.ToString("o");settings.Save();
                    if(update!=null && !IsDisposed && !busy && updatesWindow==null && LocalizedMessage.Show(this,"Font Pocket "+update.Version.ToString(3)+" 버전이 있습니다.\r\n업데이트 화면을 열까요?","새 업데이트",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)ShowAppUpdates();
                }catch(Exception ex){if(!IsDisposed)Log("앱 업데이트 확인: "+ex.Message);}finally{checkingAppUpdate=false;}
            };
        }
        void ShowAppUpdates() {
            if(updatesWindow!=null && !updatesWindow.IsDisposed){updatesWindow.Activate();return;}
            UpdateSettings settings;try {settings=UpdateSettings.Load();}catch(Exception ex){DialogUI.Error(this,ex);return;}
            var form=DialogUI.Form("Font Pocket 업데이트",620,430);updatesWindow=form;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};foreach(int height in new[]{32,34,0,26,40})layout.RowStyles.Add(new RowStyle(height==0?SizeType.Percent:SizeType.Absolute,height==0?100:height));form.Controls.Add(layout);
            layout.Controls.Add(new Label {Text="현재 버전 "+UpdateClient.CurrentVersion.ToString(3),AutoSize=true},0,0);
            var automatic=new CheckBox {Text="앱을 시작할 때 새 버전 자동 확인 (하루 한 번)",Checked=settings.AutomaticCheck,AutoSize=true};layout.Controls.Add(automatic,0,1);
            var result=DialogUI.ReadOnly(settings.Repository.Length==0?"공식 업데이트 서버가 아직 연결되지 않았습니다.":"공식 배포처에서 새 버전을 확인합니다.");layout.Controls.Add(result,0,2);
            var bar=new ProgressBar {Dock=DockStyle.Fill,Visible=false};layout.Controls.Add(bar,0,3);var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};layout.Controls.Add(buttons,0,4);
            bool downloading=false;AppUpdate available=null;
            Func<bool> save=delegate {try {settings.AutomaticCheck=automatic.Checked;settings.Save();return true;}catch(Exception ex){DialogUI.Error(form,ex);return false;}};
            var saveButton=DialogUI.Button("설정 저장",delegate {if(save())result.Text="업데이트 설정을 저장했습니다.";});buttons.Controls.Add(saveButton);
            var check=DialogUI.Button("업데이트 확인",delegate {});buttons.Controls.Add(check);var install=DialogUI.Button("업데이트 설치",delegate {});install.Enabled=false;buttons.Controls.Add(install);
            var stop=DialogUI.Button("닫기",delegate {if(downloading)cancelled=true;else form.Close();});buttons.Controls.Add(stop);
            check.Click+=async delegate {
                if(checkingAppUpdate){result.Text="이미 업데이트를 확인하고 있습니다. 잠시 후 다시 눌러 주세요.";return;}if(!save())return;
                if(settings.Repository.Length==0){result.Text="공식 업데이트 서버가 아직 연결되지 않았습니다.";return;}
                checkingAppUpdate=true;check.Enabled=false;install.Enabled=false;result.Text="새 버전을 확인하는 중…";
                try {available=await Task.Run(()=>UpdateClient.Check(settings));settings.LastCheckUtc=DateTime.UtcNow.ToString("o");settings.Save();if(!form.IsDisposed){result.Text=available==null?"현재 최신 버전을 사용하고 있습니다.":"새 버전 "+available.Version.ToString(3)+"\r\n\r\n"+available.Notes;install.Enabled=available!=null;}}
                catch(Exception ex){if(!form.IsDisposed)result.Text="업데이트를 확인하지 못했습니다. 잠시 후 다시 시도하세요.\r\n"+ex.Message;}
                finally{checkingAppUpdate=false;if(!form.IsDisposed)check.Enabled=true;}
            };
            install.Click+=async delegate {
                if(available==null || busy)return;
                if(LocalizedMessage.Show(form,"Font Pocket "+available.Version.ToString(3)+" 버전으로 업데이트할까요?\r\n다운로드 후 앱을 닫고 설치합니다. 완료되면 다시 실행합니다.\r\n설치된 폰트와 개인 설정은 유지됩니다.","업데이트 설치",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                downloading=true;SetBusy(true);check.Enabled=install.Enabled=saveButton.Enabled=automatic.Enabled=false;bar.Visible=true;stop.Text="중단";result.Text="업데이트 다운로드 중…";
                try {
                    var update=available;string file=await Task.Run(()=>UpdateClient.Download(update,()=>cancelled,p=>{if(!form.IsDisposed && form.IsHandleCreated)form.BeginInvoke((Action)delegate {if(!form.IsDisposed){bar.Value=p;result.Text="업데이트 다운로드 중 · "+p+"%";}});}));
                    if(cancelled || form.IsDisposed)throw new OperationCanceledException();
                    if(Engine.Hash(file)!=update.Hash)throw new InvalidDataException("실행 전 설치 파일 검증에 실패했습니다.");
                    string directory=File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"unins000.exe"))?AppDomain.CurrentDomain.BaseDirectory:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Font Pocket");
                    Process.Start(new ProcessStartInfo(file,UpdateClient.InstallArguments(directory)){UseShellExecute=true});
                    downloading=false;SetBusy(false);form.Close();Close();
                }catch(OperationCanceledException){if(!form.IsDisposed)result.Text="업데이트를 중단했습니다.";}
                catch(Exception ex){if(!form.IsDisposed)result.Text="업데이트하지 못했습니다. 기존 앱은 계속 사용할 수 있습니다.\r\n"+ex.Message;}
                finally{downloading=false;if(!IsDisposed)SetBusy(false);if(!form.IsDisposed){check.Enabled=saveButton.Enabled=automatic.Enabled=true;install.Enabled=available!=null;stop.Text="닫기";}}
            };
            form.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(downloading){cancelled=true;e.Cancel=true;result.Text="다운로드를 중단하는 중…";}};
            form.FormClosed+=delegate {updatesWindow=null;form.Dispose();};form.Show(this);
        }
    }
}
