using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FontPocket {
    static class FileAssociations {
        internal static readonly string[] Extensions={".ttf",".otf",".ttc",".otc",".sfnt",".woff",".woff2"};
        internal const string ProgId="FontPocket.Font",Capabilities=@"Software\FontPocket\Capabilities";
        internal static string TypeId(string ext){return ProgId+"."+ext.TrimStart('.').ToUpperInvariant();}
        internal static string TypeName(string ext) {
            string[] names=UiLanguage.Code=="ko"?new[]{"TrueType 글꼴","OpenType 글꼴","TrueType 글꼴 모음","OpenType 글꼴 모음","SFNT 글꼴","WOFF 웹 글꼴","WOFF2 웹 글꼴"}:new[]{"TrueType font","OpenType font","TrueType font collection","OpenType font collection","SFNT font","WOFF web font","WOFF2 web font"};
            return names[Array.IndexOf(Extensions,ext)];
        }
        internal sealed class Value {internal string Key,Name,Data;internal Value(string key,string name,string data){Key=key;Name=name;Data=data;}}
        internal static List<Value> Registration(string exe) {
            exe=Path.GetFullPath(exe);if(exe.Contains("\""))throw new ArgumentException("Invalid executable path");
            string command="\""+exe+"\" --open \"%1\"",classes=@"Software\Classes\",app=classes+@"Applications\FontPocket.exe";
            var values=new List<Value> {
                new Value(classes+ProgId,"",UiLanguage.Code=="ko"?"글꼴 파일":"Font file"),new Value(classes+ProgId+@"\DefaultIcon","","\""+exe+"\",0"),
                new Value(classes+ProgId+@"\shell\open\command","",command),new Value(app,"FriendlyAppName","Font Pocket"),new Value(app+@"\shell\open\command","",command),
                new Value(Capabilities,"ApplicationName","Font Pocket"),new Value(Capabilities,"ApplicationDescription","Preview, install and manage fonts with Font Pocket."),new Value(Capabilities,"ApplicationIcon","\""+exe+"\",0"),
                new Value(@"Software\RegisteredApplications","Font Pocket",Capabilities)
            };
            foreach(string ext in Extensions) {
                string type=TypeId(ext);values.Add(new Value(classes+type,"",TypeName(ext)));values.Add(new Value(classes+type+@"\DefaultIcon","","\""+exe+"\",0"));values.Add(new Value(classes+type+@"\shell\open\command","",command));
                values.Add(new Value(classes+ext+@"\OpenWithProgids",type,""));values.Add(new Value(app+@"\SupportedTypes",ext,""));values.Add(new Value(Capabilities+@"\FileAssociations",ext,type));
            }
            return values;
        }
        [DllImport("shell32.dll")]static extern void SHChangeNotify(uint eventId,uint flags,IntPtr first,IntPtr second);
        internal static void Register() {
            foreach(var value in Registration(Application.ExecutablePath))using(var key=Registry.CurrentUser.CreateSubKey(value.Key))key.SetValue(value.Name,value.Data,RegistryValueKind.String);
            foreach(string ext in Extensions)using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Classes\"+ext+@"\OpenWithProgids",true))if(key!=null)key.DeleteValue(ProgId,false);
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
        }
        internal static void OpenDefaults() {Register();Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser="+Uri.EscapeDataString("Font Pocket")){UseShellExecute=true});}
        internal static string[] OpenArguments(string[] args) {
            if(args.Length==0)return new string[0];int start=args[0]=="--open"?1:0;
            if(start==0&&args[0].StartsWith("--",StringComparison.Ordinal))return new string[0];
            return args.Skip(start).Where(p=>!p.StartsWith("--",StringComparison.Ordinal)).ToArray();
        }
    }
    public partial class MainWindow {
        internal bool PreviewOnly;
        FontDetailsDialog filePreview;
        readonly HashSet<string> openedPreviewPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> pendingOpenPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Timer fileOpenTimer;
        bool openingFileBatch;
        protected override void SetVisibleCore(bool value) {base.SetVisibleCore(value&&!PreviewOnly);}
        internal void ShowMainWindow() {PreviewOnly=false;Show();if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Activate();}
        internal void QueueFileOpen(string[] paths) {
            foreach(string path in paths)try {if(File.Exists(path)&&FontImport.Supported(path))pendingOpenPaths.Add(Path.GetFullPath(path));}catch(ArgumentException){}
            if(fileOpenTimer==null) {
                fileOpenTimer=new Timer {Interval=180};
                fileOpenTimer.Tick+=async delegate {
                    if(busy||openingFileBatch)return;
                    fileOpenTimer.Stop();var batch=pendingOpenPaths.ToArray();pendingOpenPaths.Clear();
                    if(batch.Length>0)await OpenFontFilesAsync(batch);
                };
                FormClosed+=delegate {fileOpenTimer.Dispose();if(filePreview!=null&&!filePreview.IsDisposed)filePreview.Close();};
            }
            fileOpenTimer.Stop();fileOpenTimer.Start();
        }
        internal async System.Threading.Tasks.Task OpenFontFilesAsync(string[] paths) {
            if(busy||openingFileBatch){QueueFileOpen(paths);return;}
            openingFileBatch=true;
            try {
                var valid=paths.Where(p=>File.Exists(p)&&FontImport.Supported(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if(valid.Length==0){if(PreviewOnly)Close();return;}
                var prior=new HashSet<FontItem>(items);
                bothFormats.Checked=true;includeTtc.Checked=true;await AddPaths(valid);
                if(cancelled)return;
                var added=items.Where(i=>i.Metadata!=null&&!prior.Contains(i)).ToList();
                // Reopening an already queued file must still select it in the viewer.
                foreach(string path in valid){string prepared;if(!importedSources.TryGetValue(Path.GetFullPath(path),out prepared))prepared=path;var item=items.FirstOrDefault(i=>String.Equals(i.Path,prepared,StringComparison.OrdinalIgnoreCase)&&i.Metadata!=null);if(item!=null&&!added.Contains(item))added.Add(item);}
                foreach(var item in added)openedPreviewPaths.Add(item.Path);
                var fonts=items.Where(i=>i.Metadata!=null&&openedPreviewPaths.Contains(i.Path)).Select(i=>new ManagedFont {Path=i.Path,Metadata=i.Metadata,Name=i.Name}).ToList();
                if(fonts.Count==0){if(PreviewOnly)Close();return;}
                if(filePreview==null||filePreview.IsDisposed) {
                    filePreview=new FontDetailsDialog(fonts[0].Path);var dialog=filePreview;
                    dialog.Text="Font Pocket";dialog.StartPosition=FormStartPosition.CenterScreen;
                    var layout=dialog.Controls.OfType<TableLayoutPanel>().Single();layout.RowCount=4;layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
                    var bar=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,8,0,0)};
                    var install=DialogUI.Button("설치",async delegate {
                        string selected=dialog.SelectedFile;dialog.Hide();ShowPage(0);ShowMainWindow();
                        foreach(ListViewItem row in list.Items)row.Checked=String.Equals(((FontItem)row.Tag).Path,selected,StringComparison.OrdinalIgnoreCase);
                        await InstallAll();if(!dialog.IsDisposed)dialog.Close();
                    },true);install.Name="InstallOpenedFont";bar.Controls.Add(install);
                    var queue=DialogUI.Button("대기열에서 보기",delegate {ShowPage(0);ShowMainWindow();dialog.Close();});queue.Name="ViewOpenedQueue";bar.Controls.Add(queue);layout.Controls.Add(bar,0,3);
                    dialog.FormClosed+=delegate {filePreview=null;openedPreviewPaths.Clear();dialog.Dispose();if(PreviewOnly&&!openingFileBatch&&pendingOpenPaths.Count==0)Close();};
                    dialog.AddFileChoices(fonts);dialog.Show();
                }else filePreview.AddFileChoices(fonts);
                WindowActivation.BringForward(filePreview);
            }catch(Exception ex){DialogUI.Error(filePreview!=null?(System.Windows.Forms.IWin32Window)filePreview:this,ex);if(PreviewOnly)Close();}
            finally {openingFileBatch=false;if(pendingOpenPaths.Count>0){if(fileOpenTimer!=null)fileOpenTimer.Start();}else if(PreviewOnly&&(filePreview==null||filePreview.IsDisposed)&&!IsDisposed)Close();}
        }
    }
}
