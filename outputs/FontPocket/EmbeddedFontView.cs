using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FontPocket {
    sealed class EmbeddedFontView : UserControl {
        internal readonly WebView2 Browser=new WebView2 {Dock=DockStyle.Fill};
        readonly Label state=new Label {Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter};
        readonly string html;
        internal readonly string WorkFolder;
        Task initialization;
        public EmbeddedFontView(string document) {
            html=document;WorkFolder=Path.Combine(Path.GetTempPath(),"FontPocket-Web-"+Guid.NewGuid().ToString("N"));
            Controls.Add(Browser);Controls.Add(state);state.Text=UiLanguage.Text("미리보기를 준비하는 중…");
        }
        public Task StartAsync(){return initialization??(initialization=InitializeAsync());}
        async Task InitializeAsync() {
            Directory.CreateDirectory(WorkFolder);File.WriteAllText(Path.Combine(WorkFolder,"index.html"),html,new UTF8Encoding(false));
            try {
                var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(DataPaths.Root,"WebView2"));
                if(IsDisposed)return;
                await Browser.EnsureCoreWebView2Async(environment);if(IsDisposed)return;
                var core=Browser.CoreWebView2;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsStatusBarEnabled=false;
                core.Settings.IsWebMessageEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
                core.SetVirtualHostNameToFolderMapping("fontpocket.local",WorkFolder,CoreWebView2HostResourceAccessKind.DenyCors);
                core.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){if(!e.Uri.StartsWith("https://fontpocket.local/",StringComparison.Ordinal))e.Cancel=true;};
                core.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;};
                core.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e){e.State=CoreWebView2PermissionState.Deny;};
                core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
                core.WebResourceRequested+=delegate(object sender,CoreWebView2WebResourceRequestedEventArgs e){Uri uri;if(Uri.TryCreate(e.Request.Uri,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="fontpocket.local")return;if(e.Request.Uri.StartsWith("data:")||e.Request.Uri.StartsWith("blob:"))return;e.Response=environment.CreateWebResourceResponse(new MemoryStream(),403,"Blocked","");};
                var loaded=new TaskCompletionSource<bool>();
                EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=null;
                completed=delegate(object sender,CoreWebView2NavigationCompletedEventArgs e){core.NavigationCompleted-=completed;if(e.IsSuccess)loaded.TrySetResult(true);else loaded.TrySetException(new IOException(e.WebErrorStatus.ToString()));};
                Disposed+=delegate {loaded.TrySetCanceled();};
                core.NavigationCompleted+=completed;core.Navigate("https://fontpocket.local/index.html");
                if(await Task.WhenAny(loaded.Task,Task.Delay(30000))!=loaded.Task)throw new IOException(UiLanguage.Text("미리보기 준비 시간을 초과했습니다."));
                await loaded.Task;if(IsDisposed)return;state.Visible=false;Browser.BringToFront();
            }catch(Exception ex){if(!IsDisposed){state.Text=UiLanguage.Text("앱 내부 미리보기를 열지 못했습니다. Microsoft Edge WebView2 Runtime 설치 여부를 확인해 주세요.")+"\r\n"+ex.Message;state.BringToFront();}throw;}
        }
        protected override void Dispose(bool disposing){if(disposing){Browser.Dispose();try{if(Directory.Exists(WorkFolder))Directory.Delete(WorkFolder,true);}catch(IOException){}catch(UnauthorizedAccessException){}}base.Dispose(disposing);}
    }
}
