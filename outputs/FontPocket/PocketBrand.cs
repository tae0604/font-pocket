using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FontPocket {
    // Align the visible artwork and letter outlines, rather than the padded
    // PictureBox and Label rectangles. Ratios follow the supplied logo lockup.
    sealed class PocketBrandLockup : Control {
        readonly Bitmap logo=(Bitmap)PocketBrand.CreateLogo();
        readonly GraphicsPath lettering=new GraphicsPath();
        readonly Rectangle artwork;
        public PocketBrandLockup() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor,true);
            BackColor=Color.Transparent; AccessibleName="Font Pocket"; TabStop=false;
            int left=logo.Width,top=logo.Height,right=0,bottom=0;
            for(int y=0;y<logo.Height;y++) for(int x=0;x<logo.Width;x++) {
                if(logo.GetPixel(x,y).A<8) continue;
                left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);
            }
            artwork=Rectangle.FromLTRB(left,top,right,bottom);
            using(var font=PocketBrand.CreateTitleFont())
                lettering.AddString("Font Pocket",font.FontFamily,(int)FontStyle.Regular,32,PointF.Empty,StringFormat.GenericTypographic);
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            var bounds=lettering.GetBounds();
            float iconWidth=27f,iconHeight=iconWidth*artwork.Height/artwork.Width;
            float textHeight=iconHeight*(229.688f/293f),gap=8.1f;
            float textScale=textHeight/bounds.Height;
            float scale=Math.Min(1f,Math.Min(Width/(iconWidth+gap+bounds.Width*textScale),Height/iconHeight));
            float y=(Height-iconHeight*scale)/2;
            e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(logo,new RectangleF(0,y,iconWidth*scale,iconHeight*scale),artwork,GraphicsUnit.Pixel);
            using(var path=(GraphicsPath)lettering.Clone())
            using(var matrix=new Matrix(textScale*scale,0,0,textScale*scale,
                (iconWidth+gap-bounds.X*textScale)*scale,
                y+(iconHeight-(323f-308.438f)*iconWidth/389f-textHeight-bounds.Y*textScale)*scale))
            using(var brush=new SolidBrush(Color.FromArgb(32,38,48))) {
                path.Transform(matrix);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.FillPath(brush,path);
            }
        }
        protected override void Dispose(bool disposing) {
            if(disposing) {lettering.Dispose();logo.Dispose();} base.Dispose(disposing);
        }
    }
    static class PocketBrand {
        // Shared for the process lifetime, just like SystemIcons. A dialog never
        // disposes an HICON that Windows may still be using for another window.
        static readonly Icon ApplicationIcon=CreateIcon();
        static PrivateFontCollection titleFonts;
        static IntPtr titleMemory;
        public static Font CreateTitleFont() {
            if(titleFonts==null) {
                using(var stream=typeof(PocketBrand).Assembly.GetManifestResourceStream("FontPocket.PretendardSemiBold")) {
                    var bytes=new byte[checked((int)stream.Length)];int offset=0,read;
                    while(offset<bytes.Length && (read=stream.Read(bytes,offset,bytes.Length-offset))>0) offset+=read;
                    if(offset!=bytes.Length) throw new System.IO.EndOfStreamException("제목 글꼴을 읽지 못했습니다.");
                    var memory=Marshal.AllocHGlobal(bytes.Length);var collection=new PrivateFontCollection();
                    try {Marshal.Copy(bytes,0,memory,bytes.Length);collection.AddMemoryFont(memory,bytes.Length);titleMemory=memory;titleFonts=collection;}
                    catch {collection.Dispose();Marshal.FreeHGlobal(memory);throw;}
                }
            }
            return new Font(titleFonts.Families[0],13f,FontStyle.Regular,GraphicsUnit.Point);
        }
        public static Icon CreateIcon() {
            using(var stream=typeof(PocketBrand).Assembly.GetManifestResourceStream("FontPocket.Icon"))
            using(var icon=new Icon(stream)) return (Icon)icon.Clone();
        }
        public static Image CreateLogo() {
            using(var stream=typeof(PocketBrand).Assembly.GetManifestResourceStream("FontPocket.Logo"))
            using(var image=Image.FromStream(stream)) return new Bitmap(image);
        }
        public static void Apply(Form form) {
            form.Icon=ApplicationIcon;
        }
    }
}
