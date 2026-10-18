using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    public partial class MainWindow {
        bool checkingAppUpdate;
        // The network boundary is replaceable for offline interaction tests.
        Func<UpdateSettings,AppUpdate> checkForAppUpdate=UpdateClient.Check;
        Func<AppUpdate,Func<bool>,Action<int>,string> downloadAppUpdate=UpdateClient.Download;
        Action<string,string> startAppUpdate=delegate(string file,string arguments) {Process.Start(new ProcessStartInfo(file,arguments){UseShellExecute=true});};
        Func<string,MessageBoxButtons,MessageBoxIcon,DialogResult> appUpdatePrompt=null;

        void InitializeAppUpdates(Panel sidebar) {
            var settings=new PocketButton {Text="설정",Name="AppSettings",Location=new Point(18,sidebar.Height-118),Size=new Size(180,28),Anchor=AnchorStyles.Left|AnchorStyles.Bottom,TabIndex=4};
            var update=new PocketButton {Text="업데이트 확인",Name="CheckAppUpdates",Location=new Point(18,sidebar.Height-80),Size=new Size(180,28),Anchor=AnchorStyles.Left|AnchorStyles.Bottom,TabIndex=5};
            sidebar.Controls.Add(settings);sidebar.Controls.Add(update);
            sidebar.Resize+=delegate {settings.Top=sidebar.Height-118;update.Top=sidebar.Height-80;};
            update.Click+=async delegate {await CheckForAppUpdatesAsync(false);};
            settings.Click+=delegate {ShowAppSettings();};
            BindAction(update,"공식 배포처에서 앱 업데이트를 확인합니다.",()=>checkingAppUpdate?"업데이트 확인 중입니다.":null,true);
            BindAction(settings,"언어와 자동 업데이트 확인을 설정합니다.",()=>checkingAppUpdate?"업데이트 확인 중입니다.":null,true);
            Shown+=async delegate {
                if(Environment.GetEnvironmentVariable("FONTPOCKET_TEST")=="1" || Environment.GetCommandLineArgs().Length>1)return;
                await CheckForAppUpdatesAsync(true);
            };
        }

        DialogResult UpdateMessage(string text,MessageBoxButtons buttons,MessageBoxIcon icon) {
            return appUpdatePrompt!=null?appUpdatePrompt(text,buttons,icon):LocalizedMessage.Show(this,text,"Font Pocket 업데이트",buttons,icon);
        }

        void RecordAppUpdateCheck() {
            // Reload so a background check never overwrites a newly saved preference.
            try {var latest=UpdateSettings.Load();latest.LastCheckUtc=DateTime.UtcNow.ToString("o");latest.Save();}
            catch(Exception ex) {Log("앱 업데이트 확인: "+ex.Message);}
        }

        async Task CheckForAppUpdatesAsync(bool automatic) {
            if(IsDisposed || busy || checkingAppUpdate)return;
            UpdateSettings settings;
            try {settings=UpdateSettings.Load();}
            catch(Exception ex) {if(!automatic)DialogUI.Error(this,ex);else Log("앱 업데이트 확인: "+ex.Message);return;}
            DateTime last;
            if(automatic && (!settings.AutomaticCheck || (DateTime.TryParse(settings.LastCheckUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out last) && DateTime.UtcNow-last.ToUniversalTime()<TimeSpan.FromHours(24))))return;
            checkingAppUpdate=true;
            if(!automatic) {SetBusy(true);progress.Style=ProgressBarStyle.Marquee;status.Text="업데이트 확인 중…";}
            RefreshActionAvailability();
            AppUpdate available=null;Exception failure=null;bool stopped=false;
            try {
                available=await Task.Run(()=>checkForAppUpdate(settings));
                RecordAppUpdateCheck();
                stopped=!automatic && cancelled;
            }catch(Exception ex) {failure=ex;}
            finally {
                checkingAppUpdate=false;
                if(!IsDisposed) {
                    if(!automatic) {progress.Style=ProgressBarStyle.Blocks;progress.Value=0;SetBusy(false);}
                    RefreshActionAvailability();
                }
            }
            if(IsDisposed)return;
            if(!automatic && (stopped || cancelled)) {status.Text="업데이트 확인을 중단했습니다.";return;}
            if(failure!=null) {
                if(automatic)Log("앱 업데이트 확인: "+failure.Message);
                else {status.Text="업데이트 확인 실패";UpdateMessage("업데이트를 확인하지 못했습니다. 잠시 후 다시 시도하세요.\r\n"+failure.Message,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
                return;
            }
            if(automatic && (busy || OwnedForms.Any(f=>f.Visible)))return;
            if(available==null) {
                if(!automatic) {status.Text="현재 최신 버전을 사용하고 있습니다.";UpdateMessage("현재 최신 버전을 사용하고 있습니다.",MessageBoxButtons.OK,MessageBoxIcon.Information);}
                return;
            }
            if(!automatic)status.Text="새 업데이트가 있습니다.";
            string question=String.Format(UiLanguage.Text("Font Pocket {0} 버전이 있습니다.\r\n지금 업데이트하시겠습니까?\r\n\r\n다운로드 후 앱을 닫고 설치합니다. 설치된 폰트와 개인 설정은 유지됩니다."),available.Version.ToString(3));
            if(UpdateMessage(question,MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes && !IsDisposed && !busy)
                await InstallAppUpdateAsync(available);
        }

        async Task InstallAppUpdateAsync(AppUpdate available) {
            if(busy || IsDisposed)return;
            SetBusy(true);progress.Style=ProgressBarStyle.Blocks;status.Text="업데이트 다운로드 중…";
            var report=new Progress<int>(p=> {if(!IsDisposed && busy) {progress.Value=Math.Max(0,Math.Min(100,p));status.Text="업데이트 다운로드 중 · "+p+"%";}});
            try {
                string file=await Task.Run(()=>downloadAppUpdate(available,()=>cancelled,p=>((IProgress<int>)report).Report(p)));
                if(cancelled || IsDisposed)throw new OperationCanceledException();
                if(!String.Equals(Engine.Hash(file),available.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("실행 전 설치 파일 검증에 실패했습니다.");
                string directory=File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"unins000.exe"))?AppDomain.CurrentDomain.BaseDirectory:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Font Pocket");
                startAppUpdate(file,UpdateClient.InstallArguments(directory));
                SetBusy(false);Close();
            }catch(OperationCanceledException) {if(!IsDisposed)status.Text="업데이트를 중단했습니다.";}
            catch(Exception ex) {
                if(!IsDisposed) {status.Text="업데이트 실패";UpdateMessage("업데이트하지 못했습니다. 기존 앱은 계속 사용할 수 있습니다.\r\n"+ex.Message,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            }finally {if(!IsDisposed) {progress.Style=ProgressBarStyle.Blocks;SetBusy(false);}}
        }
    }
}
