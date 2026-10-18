using System.Drawing;
using System.Windows.Forms;

namespace FontPocket {
    // Keep native selection, checkboxes, keyboard input and accessibility.
    // A mouse click on the caption selects a row without changing its check state.
    class CheckBoxOnlyListBox : CheckedListBox {
        bool mouseMessage,checkBoxHit;
        bool HitCheckBox(Point point) {
            int index=IndexFromPoint(point);if(index<0)return false;
            var row=GetItemRectangle(index);
            int width;
            using(var graphics=CreateGraphics())width=CheckBoxRenderer.GetGlyphSize(graphics,System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal).Width+4;
            var glyph=new Rectangle(RightToLeft==RightToLeft.Yes?ClientSize.Width-width:row.Left,row.Top,width,row.Height);
            return glyph.Contains(point);
        }
        protected override void WndProc(ref Message message) {
            if(message.Msg!=0x0201&&message.Msg!=0x0202&&message.Msg!=0x0203){base.WndProc(ref message);return;}
            long value=message.LParam.ToInt64();bool previousMessage=mouseMessage,previousHit=checkBoxHit;
            mouseMessage=true;checkBoxHit=HitCheckBox(new Point((short)(value&0xffff),(short)((value>>16)&0xffff)));
            try{base.WndProc(ref message);}finally{mouseMessage=previousMessage;checkBoxHit=previousHit;}
        }
        protected override void OnItemCheck(ItemCheckEventArgs e) {
            if(mouseMessage&&!checkBoxHit)e.NewValue=e.CurrentValue;
            base.OnItemCheck(e);
        }
    }
}
