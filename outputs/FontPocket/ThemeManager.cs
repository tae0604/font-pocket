using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
namespace FontPocket {
    static class ThemeManager {
        sealed class Original {public Color Back,Fore;public int BackRole,ForeRole;public int LastColumnMinimum;public bool Fitting;}
        static readonly ConditionalWeakTable<Control,Original> originals=new ConditionalWeakTable<Control,Original>();
        public static void Register(Form form){form.Load+=delegate{StyleTree(form);};}
        static void NativeControl(Control c){if(c.IsHandleCreated&&c is ListView)NativeMessage(c.Handle,0x1036,new IntPtr(0x10000),new IntPtr(0x10000));}
        [DllImport("user32.dll",EntryPoint="SendMessageW")]static extern IntPtr NativeMessage(IntPtr handle,uint message,IntPtr param,IntPtr value);
        static bool Neutral(Color c){return Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))<36;}
        static Original Remember(Control c){
            var o=new Original{Back=c.BackColor,Fore=c.ForeColor};
            if(c.BackColor.A==0)o.BackRole=0;
            else if(c is Form)o.BackRole=1;
            else if(c.Name=="AppSidebar")o.BackRole=3;
            else if(c is Surface||c is TextBoxBase||c is ListView||c is ListBox||c is ComboBox||c is NumericUpDown||c is TabPage||c is FontPreviewTile||c is CharacterGrid||c is CatalogCanvas)o.BackRole=2;
            else if(c.BackColor==PocketTheme.Paper)o.BackRole=2;
            else if(c.BackColor==PocketTheme.Sidebar)o.BackRole=3;
            else if(c.BackColor==PocketTheme.Canvas||(Neutral(c.BackColor)&&c.BackColor.R>185))o.BackRole=1;
            if(c.ForeColor==PocketTheme.Muted||(Neutral(c.ForeColor)&&c.ForeColor.R>=85&&c.ForeColor.R<190))o.ForeRole=2;
            else if(c.ForeColor==PocketTheme.Accent||(c.ForeColor.B>c.ForeColor.R*1.35&&c.ForeColor.B>100))o.ForeRole=3;
            else if(c.ForeColor==PocketTheme.Ink||Neutral(c.ForeColor))o.ForeRole=1;
            c.HandleCreated+=delegate{NativeControl(c);};
            var list=c as ListView;if(list!=null){
                o.LastColumnMinimum=list.Columns.Count==0?80:Math.Max(60,list.Columns[list.Columns.Count-1].Width);
                list.Resize+=delegate {FitColumns(list);};list.ColumnWidthChanged+=delegate {FitColumns(list);};
            }
            c.ControlAdded+=delegate(object sender,ControlEventArgs args){var form=c.FindForm();if(form!=null&&form.Visible&&form.IsHandleCreated)try{form.BeginInvoke((Action)delegate{if(!args.Control.IsDisposed)StyleTree(args.Control);});}catch(InvalidOperationException){}};
            return o;
        }
        public static void FitColumns(ListView list){Original o;if(!originals.TryGetValue(list,out o)||o.Fitting||list.View!=View.Details||list.Columns.Count==0||list.ClientSize.Width<1)return;o.Fitting=true;try {int used=0;for(int i=0;i<list.Columns.Count-1;i++)used+=list.Columns[i].Width;int width=Math.Max(o.LastColumnMinimum,list.ClientSize.Width-used-1);if(list.Columns[list.Columns.Count-1].Width!=width)list.Columns[list.Columns.Count-1].Width=width;}finally{o.Fitting=false;}}
        static void StyleMenu(ToolStrip menu){menu.Font=PocketTheme.Body();menu.BackColor=PocketTheme.Paper;menu.ForeColor=PocketTheme.Ink;menu.Renderer=new PocketMenuRenderer();var popup=menu as ToolStripDropDown;if(popup!=null)popup.ShowItemToolTips=false;foreach(ToolStripItem item in menu.Items){item.ForeColor=PocketTheme.Ink;if(item is ToolStripMenuItem)item.Padding=new Padding(5,3,5,3);var drop=item as ToolStripDropDownItem;if(drop!=null&&drop.HasDropDownItems)StyleMenu(drop.DropDown);}}
        public static void StyleTree(Control root){
            var o=originals.GetValue(root,Remember);
            root.BackColor=o.BackRole==1?PocketTheme.Canvas:o.BackRole==2?PocketTheme.Paper:o.BackRole==3?PocketTheme.Sidebar:o.Back;
            root.ForeColor=o.ForeRole==1?PocketTheme.Ink:o.ForeRole==2?PocketTheme.Muted:o.ForeRole==3?PocketTheme.Accent:o.Fore;
            var list=root as ListView;if(list!=null){list.BorderStyle=BorderStyle.None;list.BackColor=PocketTheme.Paper;list.ForeColor=PocketTheme.Ink;list.Font=PocketTheme.Body();list.GridLines=false;ModernList.Apply(list);if(list.SmallImageList==null&&list.View==View.Details){var rows=new ImageList{ImageSize=new Size(1,34),ColorDepth=ColorDepth.Depth32Bit};var pixel=new Bitmap(1,34);rows.Images.Add(pixel);list.SmallImageList=rows;list.Disposed+=delegate{rows.Dispose();pixel.Dispose();};}}
            var box=root as ListBox;if(box!=null){box.BorderStyle=BorderStyle.None;box.BackColor=PocketTheme.Paper;box.ForeColor=PocketTheme.Ink;box.IntegralHeight=false;box.Font=PocketTheme.Body();box.ItemHeight=28;}
            var text=root as TextBox;if(text!=null){text.BackColor=PocketTheme.Paper;text.ForeColor=PocketTheme.Ink;text.BorderStyle=text.Parent is ModernInput.InputFrame||text.ReadOnly&&text.Multiline?BorderStyle.None:BorderStyle.FixedSingle;}
            var button=root as Button;if(button!=null&&!(button is PocketNavigationButton)){if(!(button is PocketButton)){button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=1;button.FlatAppearance.BorderColor=PocketTheme.Border;button.BackColor=PocketTheme.Paper;button.UseVisualStyleBackColor=false;}}
            var combo=root as ComboBox;if(combo!=null){if(!(combo is PocketComboBox))combo.DrawMode=DrawMode.Normal;combo.BackColor=PocketTheme.Paper;combo.ForeColor=PocketTheme.Ink;}
            if(list!=null)FitColumns(list);
            var number=root as NumericUpDown;if(number!=null){number.BorderStyle=number.Parent is ModernInput.InputFrame?BorderStyle.None:BorderStyle.FixedSingle;number.BackColor=PocketTheme.Paper;number.ForeColor=PocketTheme.Ink;}
            if(root is CheckBox||root is RadioButton)root.ForeColor=PocketTheme.Ink;
            var page=root as TabPage;if(page!=null)page.UseVisualStyleBackColor=false;
            var strip=root as ToolStrip;if(strip!=null)StyleMenu(strip);if(root.ContextMenuStrip!=null)StyleMenu(root.ContextMenuStrip);
            NativeControl(root);foreach(Control child in root.Controls)StyleTree(child);root.Invalidate();
        }
    }
}
