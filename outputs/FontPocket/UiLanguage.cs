using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FontPocket {
    public static class UiLanguage {
        public static readonly string Code=Load();
        static Dictionary<string,string> translations;
        static KeyValuePair<string,string>[] phrases;
        static readonly Timer refresh=new Timer {Interval=250};
        static bool started;
        static readonly HashSet<Control> hooked=new HashSet<Control>();
        static readonly HashSet<Control> changing=new HashSet<Control>();
        static UiLanguage() { }
        public static string PreferencePath {get {return Path.Combine(DataPaths.Root,"ui-language.txt");}}
        static string Load() {
            if(Environment.GetEnvironmentVariable("FONTPOCKET_TEST")=="1")return "ko";
            try {
                string value=File.Exists(PreferencePath)?File.ReadAllText(PreferencePath).Trim():Convert.ToString(Registry.GetValue(@"HKEY_CURRENT_USER\Software\FontPocket","UILanguage",null));
                if(value=="en" || value=="ko")return value;
            }catch(IOException){}catch(UnauthorizedAccessException){}
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="ko"?"ko":"en";
        }
        public static void Save(string code) {
            if(code!="ko" && code!="en")throw new ArgumentException("Unsupported language");
            Directory.CreateDirectory(DataPaths.Root);string temp=PreferencePath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {File.WriteAllText(temp,code,new UTF8Encoding(false));if(File.Exists(PreferencePath))File.Replace(temp,PreferencePath,null);else File.Move(temp,PreferencePath);}
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
        public static string English(string text) {
            if(String.IsNullOrEmpty(text))return text;
            if(translations==null)using(var stream=typeof(UiLanguage).Assembly.GetManifestResourceStream("FontPocket.English"))using(var reader=new StreamReader(stream,Encoding.UTF8)) {
                translations=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(reader.ReadToEnd());phrases=translations.OrderByDescending(p=>p.Key.Length).ToArray();
            }
            if(!text.Any(c=>c>='가' && c<='힣'))return text;
            string exact;if(translations.TryGetValue(text,out exact))return exact;
            foreach(var pair in phrases)text=text.Replace(pair.Key,pair.Value);return text;
        }
        public static string Text(string text) {return Code=="en"?English(text):text;}
        static void Menu(ToolStripItemCollection items) {foreach(ToolStripItem item in items){item.Text=Text(item.Text);item.ToolTipText=Text(item.ToolTipText);var drop=item as ToolStripDropDownItem;if(drop!=null)Menu(drop.DropDownItems);}}
        public static void Translate(Control root) {
            if(Code!="en")return;
            var edit=root as TextBox;
            bool editable=(edit!=null && !edit.ReadOnly) || Object.Equals(root.Tag,"OriginalMetadata");
            if(!editable && !(root is ComboBox) && !(root is ListBox) && !(root is ListView) && !(root is NumericUpDown) && hooked.Add(root)) {
                root.TextChanged+=delegate {if(!root.IsDisposed && changing.Add(root))try{string translated=Text(root.Text);if(translated!=root.Text)root.Text=translated;}finally{changing.Remove(root);}};
                root.Disposed+=delegate {hooked.Remove(root);changing.Remove(root);};
            }
            if(!(root is ComboBox) && !(root is ListBox) && !(root is ListView) && !(root is NumericUpDown) && !editable) {string text=Text(root.Text);if(text!=root.Text)root.Text=text;}
            var combo=root as ComboBox;if(combo!=null && combo.DataSource==null)for(int i=0;i<combo.Items.Count;i++)if(combo.Items[i] is string) {
                string original=(string)combo.Items[i],translated;if(translations==null)English(" ");if(translations.TryGetValue(original,out translated) && original!=translated)combo.Items[i]=translated;
            }
            var list=root as ListView;if(list!=null) {
                foreach(ColumnHeader column in list.Columns)column.Text=Text(column.Text);
                foreach(ListViewItem row in list.Items)for(int i=1;i<row.SubItems.Count;i++){string value=row.SubItems[i].Text,translated;if(translations==null)English(" ");if(translations.TryGetValue(value,out translated) && value!=translated)row.SubItems[i].Text=translated;}
            }
            if(root.ContextMenuStrip!=null)Menu(root.ContextMenuStrip.Items);
            var strip=root as ToolStrip;if(strip!=null)Menu(strip.Items);
            foreach(Control child in root.Controls)Translate(child);
        }
        public static void Apply(Form form) {
            if(Code!="en")return;
            form.Load+=delegate {Translate(form);};
            if(!started){started=true;refresh.Tick+=delegate {foreach(Form open in Application.OpenForms.Cast<Form>().ToArray())if(!open.IsDisposed)Translate(open);};refresh.Start();Application.ApplicationExit+=delegate {refresh.Stop();refresh.Dispose();};}
        }
    }
    static class LocalizedMessage {
        public static DialogResult Show(IWin32Window owner,string text,string caption="",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1) {
            return MessageBox.Show(owner,UiLanguage.Text(text),UiLanguage.Text(caption),buttons,icon,defaultButton);
        }
        public static DialogResult Show(string text,string caption="",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None) {
            return MessageBox.Show(UiLanguage.Text(text),UiLanguage.Text(caption),buttons,icon);
        }
    }
    public partial class MainWindow {
        void ShowLanguageSettings() {
            if(busy)return;
            using(var form=DialogUI.Form("언어 설정",470,230)) {
                var layout=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));form.Controls.Add(layout);
                var choice=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};choice.Items.AddRange(new object[]{"한국어","English"});choice.SelectedIndex=UiLanguage.Code=="en"?1:0;layout.Controls.Add(choice);
                layout.Controls.Add(new Label {Text="언어 변경은 앱을 다시 시작하면 적용됩니다.",Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0)});
                var apply=DialogUI.Button("적용",delegate {form.DialogResult=DialogResult.OK;});layout.Controls.Add(apply);
                if(form.ShowDialog(this)!=DialogResult.OK)return;
                string code=choice.SelectedIndex==1?"en":"ko";if(code==UiLanguage.Code)return;
                try {
                    UiLanguage.Save(code);
                    if(LocalizedMessage.Show(this,"설정을 저장했습니다. 지금 앱을 다시 시작할까요?","언어 설정",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes) {
                        Process.Start(new ProcessStartInfo(Application.ExecutablePath){UseShellExecute=true});Close();
                    }
                }catch(Exception ex){DialogUI.Error(this,ex);}
            }
        }
    }
}
