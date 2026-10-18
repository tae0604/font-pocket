using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FontPocket {
    // One receiver per Windows user/session gathers Explorer's separate file launches.
    sealed class FontPocketApplication {
        readonly string id="FontPocket.Files.v1."+WindowsIdentity.GetCurrent().User.Value;
        internal void Run(string[] args) {
            using(var mutex=new Mutex(false,"Local\\"+id)) {
                bool owned;try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
                if(!owned){Forward(FileAssociations.OpenArguments(args));return;}
                try {using(var window=new MainWindow())using(var receiver=new FileReceiver(id,window)) {
                    string[] files=FileAssociations.OpenArguments(args);window.PreviewOnly=files.Length>0;var handle=window.Handle;
                    if(files.Length>0)window.BeginInvoke(new Action(()=>window.QueueFileOpen(files)));
                    else window.Shown+=delegate {window.ShowPage(3);};
                    Application.Run(window);
                }}finally{mutex.ReleaseMutex();}
            }
        }
        [StructLayout(LayoutKind.Sequential)]struct CopyData {internal IntPtr Kind;internal int Length;internal IntPtr Bytes;}
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string className,string title);
        [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SendMessageTimeout(IntPtr window,uint msg,IntPtr sender,ref CopyData data,uint flags,uint timeout,out IntPtr result);
        void Forward(string[] files) {
            try {
                IntPtr target=IntPtr.Zero;for(int i=0;i<100&&target==IntPtr.Zero;i++){target=FindWindowEx(new IntPtr(-3),IntPtr.Zero,null,id);if(target==IntPtr.Zero)Thread.Sleep(100);}
                if(target==IntPtr.Zero)throw new IOException("열린 Font Pocket에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요.");
                // The launcher owns the user's foreground action; transfer that permission
                // before it exits so the existing process can activate the viewer later.
                WindowActivation.GrantToReceiver(target);
                string json=new JavaScriptSerializer().Serialize(files.Select(Path.GetFullPath).ToArray());byte[] bytes=System.Text.Encoding.UTF8.GetBytes(json);
                if(bytes.Length>1048576)throw new IOException("한 번에 여는 파일이 너무 많습니다.");
                IntPtr buffer=Marshal.AllocHGlobal(bytes.Length);try {Marshal.Copy(bytes,0,buffer,bytes.Length);var request=new CopyData {Kind=new IntPtr(1),Length=bytes.Length,Bytes=buffer};IntPtr result;
                    if(SendMessageTimeout(target,0x4a,IntPtr.Zero,ref request,2,10000,out result)==IntPtr.Zero||result!=new IntPtr(1))throw new IOException("열린 Font Pocket이 응답하지 않습니다. 잠시 후 다시 시도해 주세요.");
                }finally{Marshal.FreeHGlobal(buffer);}
            }catch(Exception ex){if(Environment.GetEnvironmentVariable("FONTPOCKET_TEST")=="1")File.WriteAllText(Path.Combine(DataPaths.Root,"forward-error-"+System.Diagnostics.Process.GetCurrentProcess().Id+".txt"),ex.ToString());else MessageBox.Show(ex.Message,"Font Pocket",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        sealed class FileReceiver : NativeWindow,IDisposable {
            readonly MainWindow window;
            internal FileReceiver(string id,MainWindow main) {window=main;CreateHandle(new CreateParams {Caption=id,Parent=new IntPtr(-3)});}
            protected override void WndProc(ref Message message) {
                if(message.Msg==0x4a) {
                    message.Result=IntPtr.Zero;
                    try {var data=(CopyData)Marshal.PtrToStructure(message.LParam,typeof(CopyData));if(data.Kind!=new IntPtr(1)||data.Length<2||data.Length>1048576)return;
                        var bytes=new byte[data.Length];Marshal.Copy(data.Bytes,bytes,0,bytes.Length);var files=new JavaScriptSerializer {MaxJsonLength=1048576}.Deserialize<string[]>(System.Text.Encoding.UTF8.GetString(bytes));
                        if(files==null||files.Length>256||files.Any(p=>String.IsNullOrEmpty(p)||p.Length>32768))return;
                        window.BeginInvoke(new Action(delegate {if(files.Length==0)window.ShowMainWindow();else window.QueueFileOpen(files);}));message.Result=new IntPtr(1);
                    }catch(ArgumentException){}catch(InvalidOperationException){}return;
                }
                base.WndProc(ref message);
            }
            public void Dispose(){DestroyHandle();}
        }
    }
}
