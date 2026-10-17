using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FontPocket {
    static class PocketTheme {
        public static readonly Color Canvas=Color.FromArgb(248,249,251), Paper=Color.White, Ink=Color.FromArgb(35,40,48), Muted=Color.FromArgb(108,116,129), Border=Color.FromArgb(226,230,236), Accent=Color.FromArgb(43,96,163), AccentSoft=Color.FromArgb(232,239,248);
        static readonly Font UIFont=new Font("맑은 고딕",9.5f);
        public static Font Body() { return UIFont; }
        [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr handle,string app,string list);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr handle,uint message,IntPtr param,string text);
        public static void Apply(Form form) {
            PocketBrand.Apply(form);
            UiLanguage.Apply(form);
            form.BackColor=Canvas;form.ForeColor=Ink;form.Font=Body();form.AutoScaleMode=AutoScaleMode.Dpi;form.AutoScaleDimensions=new SizeF(96,96);
            form.Load+=delegate { StyleTree(form); };
            form.HandleCreated+=delegate { try { int color=0x00fbf9f8;DwmSetWindowAttribute(form.Handle,35,ref color,4);int corners=2;DwmSetWindowAttribute(form.Handle,33,ref corners,4); }catch(DllNotFoundException) {}catch(EntryPointNotFoundException) {} };
        }
        public static void Cue(TextBox box,string hint) { box.HandleCreated+=delegate { SendMessage(box.Handle,0x1501,new IntPtr(1),UiLanguage.Text(hint)); };if(box.IsHandleCreated) SendMessage(box.Handle,0x1501,new IntPtr(1),UiLanguage.Text(hint)); }
        public static void StyleTree(Control root) {
            foreach(Control c in root.Controls) {
                var list=c as ListView;
                if(list!=null) { list.BorderStyle=BorderStyle.None;list.BackColor=Paper;list.ForeColor=Ink;list.Font=Body();list.GridLines=false;
                    if(list.SmallImageList==null && list.View==View.Details) {var rows=new ImageList {ImageSize=new Size(1,30),ColorDepth=ColorDepth.Depth32Bit};var pixel=new Bitmap(1,30);rows.Images.Add(pixel);list.SmallImageList=rows;list.Disposed+=delegate {rows.Dispose();pixel.Dispose();};}
                    if(list.IsHandleCreated) SetWindowTheme(list.Handle,"Explorer",null);else list.HandleCreated+=delegate {SetWindowTheme(list.Handle,"Explorer",null);};
                }
                var box=c as ListBox;if(box!=null) {box.BorderStyle=BorderStyle.None;box.BackColor=Paper;box.ForeColor=Ink;box.IntegralHeight=false;box.Font=Body();box.ItemHeight=28;}
                var text=c as TextBox;if(text!=null) {text.BackColor=Paper;text.ForeColor=Ink;text.BorderStyle=text.ReadOnly && text.Multiline?BorderStyle.None:BorderStyle.FixedSingle;}
                var button=c as Button;if(button!=null && !(button is PocketNavigationButton)) {button.FlatStyle=FlatStyle.System;button.UseVisualStyleBackColor=true;}
                var number=c as NumericUpDown;if(number!=null) {number.BorderStyle=BorderStyle.FixedSingle;number.BackColor=Paper;}
                if(c is CheckBox || c is RadioButton) c.ForeColor=Ink;
                var menu=c as ToolStrip;if(menu!=null) {menu.Font=Body();menu.RenderMode=ToolStripRenderMode.System;}
                if(c.ContextMenuStrip!=null) {c.ContextMenuStrip.Font=Body();c.ContextMenuStrip.RenderMode=ToolStripRenderMode.System;}
                StyleTree(c);
            }
        }
    }
    class PocketTabs : TabControl {
        public PocketTabs() {DrawMode=TabDrawMode.Normal;Padding=new Point(12,6);}
    }
}
