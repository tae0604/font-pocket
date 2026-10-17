using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FontPocketSetup {
    public sealed class SetupLanguageDialog : Form {
        public readonly ComboBox Languages=new ComboBox();
        public readonly Label Prompt=new Label();
        public readonly Button Confirm=new Button(),Cancel=new Button();
        public string Language {get {return Languages.SelectedIndex==0?"korean":"english";}}
        public static string DefaultLanguage(ushort windowsLanguage) {return (windowsLanguage&0x3ff)==0x12?"korean":"english";}
        public SetupLanguageDialog(string language) {
            Font=SystemFonts.MessageBoxFont;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(400,172);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;
            using(var stream=typeof(SetupLanguageDialog).Assembly.GetManifestResourceStream("FontPocket.Icon")) if(stream!=null)using(var icon=new Icon(stream))Icon=(Icon)icon.Clone();
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(18,14,18,12),ColumnCount=1,RowCount=3};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(layout);
            Prompt.Dock=DockStyle.Fill;layout.Controls.Add(Prompt,0,0);
            Languages.DropDownStyle=ComboBoxStyle.DropDownList;Languages.Dock=DockStyle.Top;Languages.Items.AddRange(new object[]{"한국어","English"});layout.Controls.Add(Languages,0,1);
            var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(0,6,0,0)};layout.Controls.Add(actions,0,2);
            foreach(var button in new[]{Cancel,Confirm}) {button.Size=new Size(86,28);button.FlatStyle=FlatStyle.System;button.UseVisualStyleBackColor=true;actions.Controls.Add(button);}
            Confirm.DialogResult=DialogResult.OK;Cancel.DialogResult=DialogResult.Cancel;AcceptButton=Confirm;CancelButton=Cancel;
            Languages.SelectedIndexChanged+=delegate {ApplyLanguage();};Languages.SelectedIndex=language=="korean"?0:1;
        }
        void ApplyLanguage() {
            bool korean=Language=="korean";
            Text=korean?"설치 언어 선택":"Select Setup Language";
            Prompt.Text=korean?"설치 중에 사용할 언어를 선택하세요.":"Select the language to use during installation.";
            Confirm.Text=korean?"확인":"OK";Cancel.Text=korean?"취소":"Cancel";
        }
    }
    public static class SetupLauncher {
        [DllImport("kernel32.dll")] static extern ushort GetUserDefaultUILanguage();
        public static string Quote(string value) {
            var result=new StringBuilder("\"");int slashes=0;
            foreach(char c in value) {
                if(c=='\\') {slashes++;continue;}
                if(c=='"')result.Append('\\',slashes*2+1);else result.Append('\\',slashes);
                result.Append(c);slashes=0;
            }
            result.Append('\\',slashes*2).Append('"');return result.ToString();
        }
        public static string ExplicitLanguage(string[] args) {
            string option=args.LastOrDefault(a=>a.StartsWith("/LANG=",StringComparison.OrdinalIgnoreCase));
            if(option==null)return null;string value=option.Substring(6).ToLowerInvariant();
            if(value!="korean" && value!="english")throw new ArgumentException("Supported languages: korean, english");return value;
        }
        [STAThread] public static int Main(string[] args) {
            string language=SetupLanguageDialog.DefaultLanguage(GetUserDefaultUILanguage()),directory=null;
            try {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                string explicitLanguage=ExplicitLanguage(args);if(explicitLanguage!=null)language=explicitLanguage;
                bool silent=args.Any(a=>a.Equals("/SILENT",StringComparison.OrdinalIgnoreCase)||a.Equals("/VERYSILENT",StringComparison.OrdinalIgnoreCase));
                if(!silent && explicitLanguage==null)using(var dialog=new SetupLanguageDialog(language)) {if(dialog.ShowDialog()!=DialogResult.OK)return 2;language=dialog.Language;}
                directory=Path.Combine(Path.GetTempPath(),"FontPocket-Setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
                string setup=Path.Combine(directory,"FontPocket-Install.exe");
                using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("FontPocket.Setup")) {
                    if(source==null)throw new InvalidDataException("Missing installer payload");
                    using(var output=new FileStream(setup,FileMode.CreateNew,FileAccess.Write,FileShare.None))source.CopyTo(output);
                }
                var forwarded=args.Where(a=>!a.StartsWith("/LANG=",StringComparison.OrdinalIgnoreCase)).Concat(new[]{"/LANG="+language});
                using(var process=Process.Start(new ProcessStartInfo(setup,String.Join(" ",forwarded.Select(Quote))){UseShellExecute=false})) {process.WaitForExit();return process.ExitCode;}
            }catch(Exception ex) {
                bool korean=language=="korean";
                if(!args.Any(a=>a.Equals("/VERYSILENT",StringComparison.OrdinalIgnoreCase)))MessageBox.Show((korean?"설치를 시작하지 못했습니다.\r\n":"Could not start Setup.\r\n")+ex.Message,"Font Pocket",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return 1;
            }finally {
                // Only the unique directory created above belongs to this process.
                if(directory!=null)try {Directory.Delete(directory,true);}catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
    }
}
