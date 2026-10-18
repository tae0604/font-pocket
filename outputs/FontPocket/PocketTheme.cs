using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FontPocket {
    static class PocketTheme {
        public static Color Canvas {get{return Color.FromArgb(248,249,251);}}
        public static Color Paper {get{return Color.White;}}
        public static Color Sidebar {get{return Color.FromArgb(242,244,247);}}
        public static Color Ink {get{return Color.FromArgb(35,40,48);}}
        public static Color Muted {get{return Color.FromArgb(108,116,129);}}
        public static Color Border {get{return Color.FromArgb(226,230,236);}}
        public static Color Accent {get{return Color.FromArgb(43,96,163);}}
        public static Color AccentSoft {get{return Color.FromArgb(232,239,248);}}
        public static Color Hover {get{return Color.FromArgb(239,242,246);}}
        public static Color Pressed {get{return Color.FromArgb(227,232,239);}}
        static readonly Font UIFont=new Font("맑은 고딕",9.5f);
        public static Font Body() { return UIFont; }
        [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr handle,string app,string list);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr handle,uint message,IntPtr param,string text);
        public static void Apply(Form form) {
            PocketBrand.Apply(form);
            UiLanguage.Apply(form);
            form.BackColor=Canvas;form.ForeColor=Ink;form.Font=Body();form.AutoScaleMode=AutoScaleMode.Dpi;form.AutoScaleDimensions=new SizeF(96,96);
            ThemeManager.Register(form);
        }
        public static void Cue(TextBox box,string hint) { box.HandleCreated+=delegate { SendMessage(box.Handle,0x1501,new IntPtr(1),UiLanguage.Text(hint)); };if(box.IsHandleCreated) SendMessage(box.Handle,0x1501,new IntPtr(1),UiLanguage.Text(hint)); }
        public static void StyleTree(Control root) {ThemeManager.StyleTree(root);}
    }
    class PocketTabs : TabControl {
        public PocketTabs() {DrawMode=TabDrawMode.Normal;Padding=new Point(12,6);}
    }
}

