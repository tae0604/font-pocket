using System.Drawing;
using System.Windows.Forms;

namespace FontPocket {
    sealed class PocketMenuRenderer : ToolStripProfessionalRenderer {
        sealed class Colors : ProfessionalColorTable {
            public override Color ToolStripDropDownBackground {get{return PocketTheme.Paper;}}
            public override Color ImageMarginGradientBegin {get{return PocketTheme.Paper;}}
            public override Color ImageMarginGradientMiddle {get{return PocketTheme.Paper;}}
            public override Color ImageMarginGradientEnd {get{return PocketTheme.Paper;}}
            public override Color MenuBorder {get{return PocketTheme.Border;}}
            public override Color MenuItemBorder {get{return PocketTheme.AccentSoft;}}
            public override Color MenuItemSelected {get{return PocketTheme.AccentSoft;}}
            public override Color MenuItemSelectedGradientBegin {get{return PocketTheme.AccentSoft;}}
            public override Color MenuItemSelectedGradientEnd {get{return PocketTheme.AccentSoft;}}
            public override Color CheckBackground {get{return PocketTheme.AccentSoft;}}
            public override Color CheckSelectedBackground {get{return PocketTheme.AccentSoft;}}
        }
        internal PocketMenuRenderer():base(new Colors()){RoundedEdges=false;}
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
            using(var b=new SolidBrush(e.Item.Enabled&&e.Item.Selected?PocketTheme.AccentSoft:PocketTheme.Paper))e.Graphics.FillRectangle(b,new Rectangle(0,0,e.Item.Width,e.Item.Height));
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {e.TextColor=e.Item.Enabled?PocketTheme.Ink:Color.FromArgb(127,135,146);base.OnRenderItemText(e);}
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) {e.ArrowColor=e.Item.Enabled?PocketTheme.Ink:Color.FromArgb(127,135,146);base.OnRenderArrow(e);}
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) {using(var p=new Pen(PocketTheme.Border))e.Graphics.DrawLine(p,8,e.Item.Height/2,e.Item.Width-8,e.Item.Height/2);}
    }
}
