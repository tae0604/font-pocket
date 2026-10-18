using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace FontPocket {
    static class ModernInput {
        internal static Control Frame(Control input){return new InputFrame(input);}
        internal sealed class InputFrame : Panel {
            readonly Control input;
            internal InputFrame(Control child){input=child;DoubleBuffered=true;Dock=child.Dock;Anchor=child.Anchor;Margin=child.Margin;Location=child.Location;Size=child.Size;TabIndex=child.TabIndex;TabStop=false;BackColor=PocketTheme.Paper;MinimumSize=new Size(30,0);
                var text=child as TextBox;if(text!=null)text.BorderStyle=BorderStyle.None;var number=child as NumericUpDown;if(number!=null)number.BorderStyle=BorderStyle.None;
                child.Dock=DockStyle.None;child.Margin=Padding.Empty;child.TabIndex=0;Controls.Add(child);child.GotFocus+=delegate{Invalidate();};child.LostFocus+=delegate{Invalidate();};child.EnabledChanged+=delegate{Invalidate();};Resize+=delegate{Arrange();};Click+=delegate{input.Focus();};Arrange();
            }
            void Arrange(){var text=input as TextBox;bool multiline=text!=null&&text.Multiline;int pad=multiline?4:8;int height=multiline?Math.Max(1,Height-pad*2):input.PreferredSize.Height;input.SetBounds(10,multiline?pad:Math.Max(1,(Height-height)/2),Math.Max(1,Width-20),height);}
            protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?PocketTheme.Canvas:Parent.BackColor);using(var p=Shape.Round(new RectangleF(.5f,.5f,Width-1,Height-1),6))using(var b=new SolidBrush(PocketTheme.Paper))using(var pen=new Pen(input.ContainsFocus?PocketTheme.Accent:PocketTheme.Border,input.ContainsFocus?1.5f:1)){g.FillPath(b,p);g.DrawPath(pen,p);}}
        }
    }
    class PocketComboBox : ComboBox {
        bool hover;
        public PocketComboBox(){DrawMode=DrawMode.OwnerDrawFixed;DropDownStyle=ComboBoxStyle.DropDownList;ItemHeight=24;FlatStyle=FlatStyle.Flat;BackColor=PocketTheme.Paper;ForeColor=PocketTheme.Ink;}
        protected override void OnDrawItem(DrawItemEventArgs e){if(e.Bounds.Height<=0)return;bool popup=(e.State&DrawItemState.ComboBoxEdit)==0;Color back=popup&&(e.State&DrawItemState.Selected)!=0?PocketTheme.AccentSoft:PocketTheme.Paper;using(var brush=new SolidBrush(back))e.Graphics.FillRectangle(brush,e.Bounds);string text=e.Index<0?Text:GetItemText(Items[e.Index]);var r=e.Bounds;r.X+=9;r.Width-=12;TextRenderer.DrawText(e.Graphics,text,Font,r,Enabled?PocketTheme.Ink:PocketTheme.Muted,TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
        void PaintField(Graphics g){g.SmoothingMode=SmoothingMode.AntiAlias;using(var backdrop=new SolidBrush(Parent==null?PocketTheme.Canvas:Parent.BackColor))g.FillRectangle(backdrop,ClientRectangle);using(var path=Shape.Round(new RectangleF(.5f,.5f,Width-1,Height-1),6))using(var brush=new SolidBrush(PocketTheme.Paper))using(var pen=new Pen(Focused?PocketTheme.Accent:hover?Color.FromArgb(192,201,213):PocketTheme.Border)){g.FillPath(brush,path);g.DrawPath(pen,path);}TextRenderer.DrawText(g,Text,Font,new Rectangle(9,0,Math.Max(1,Width-31),Height),Enabled?PocketTheme.Ink:PocketTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);int x=Width-14,y=Height/2;using(var pen=new Pen(Enabled?PocketTheme.Muted:PocketTheme.Border,1.5f))g.DrawLines(pen,new[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});}
        protected override void WndProc(ref Message m){base.WndProc(ref m);if(Width<4||Height<4)return;if(m.Msg==0xf){using(var g=Graphics.FromHwnd(Handle))PaintField(g);}else if((m.Msg==0x317||m.Msg==0x318)&&m.WParam!=IntPtr.Zero){using(var g=Graphics.FromHdc(m.WParam))PaintField(g);}}

    }
    static class ModernList {
        sealed class Style {internal int Hover=-1;}
        static readonly ConditionalWeakTable<ListView,Style> styled=new ConditionalWeakTable<ListView,Style>();
        internal static void Apply(ListView list){Style existing;if(styled.TryGetValue(list,out existing)||list.View!=View.Details||list.OwnerDraw)return;var state=new Style();styled.Add(list,state);list.OwnerDraw=true;
            list.DrawColumnHeader+=delegate(object sender,DrawListViewColumnHeaderEventArgs e){if(e.Bounds.Width<=0)return;using(var brush=new SolidBrush(Color.FromArgb(247,248,250)))e.Graphics.FillRectangle(brush,e.Bounds);using(var pen=new Pen(PocketTheme.Border))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);var r=e.Bounds;r.X+=12;r.Width-=18;TextRenderer.DrawText(e.Graphics,e.Header.Text,list.Font,r,PocketTheme.Muted,TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);};
            list.DrawItem+=delegate(object sender,DrawListViewItemEventArgs e){if(list.View!=View.Details)e.DrawDefault=true;};
            list.DrawSubItem+=delegate(object sender,DrawListViewSubItemEventArgs e){if(e.Bounds.Width<=0||e.Header.Width==0)return;Color back=e.Item.Selected?PocketTheme.AccentSoft:e.ItemIndex==state.Hover?Color.FromArgb(247,249,252):PocketTheme.Paper;using(var brush=new SolidBrush(back))e.Graphics.FillRectangle(brush,e.Bounds);using(var pen=new Pen(Color.FromArgb(243,245,248)))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);
                int padding=12;if(e.ColumnIndex==0){if(e.Item.Selected)using(var brush=new SolidBrush(PocketTheme.Accent))e.Graphics.FillRectangle(brush,e.Bounds.Left,e.Bounds.Top+7,3,Math.Max(1,e.Bounds.Height-14));if(list.CheckBoxes){var check=e.Item.Checked?CheckBoxState.CheckedNormal:CheckBoxState.UncheckedNormal;Size size=CheckBoxRenderer.GetGlyphSize(e.Graphics,check);CheckBoxRenderer.DrawCheckBox(e.Graphics,new Point(e.Bounds.Left+4,e.Bounds.Top+(e.Bounds.Height-size.Height)/2),check);padding=24;}}
                var r=e.Bounds;r.X+=padding;r.Width=Math.Max(1,r.Width-padding-8);Color ink=e.Item.ForeColor.IsEmpty?PocketTheme.Ink:e.Item.ForeColor;var flags=TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix;if(e.Header.TextAlign==HorizontalAlignment.Right)flags|=TextFormatFlags.Right;TextRenderer.DrawText(e.Graphics,e.SubItem.Text,list.Font,r,ink,flags);
                if(e.Item.Focused&&list.Focused&&e.ColumnIndex==0)using(var pen=new Pen(PocketTheme.Accent))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Top+5,e.Bounds.Left,e.Bounds.Bottom-5);
            };
            list.MouseMove+=delegate(object sender,MouseEventArgs e){var item=list.GetItemAt(e.X,e.Y);int index=item==null?-1:item.Index;if(index==state.Hover)return;int old=state.Hover;state.Hover=index;if(old>=0&&old<list.Items.Count)list.Invalidate(list.Items[old].Bounds);if(item!=null)list.Invalidate(item.Bounds);};list.MouseLeave+=delegate{int old=state.Hover;state.Hover=-1;if(old>=0&&old<list.Items.Count)list.Invalidate(list.Items[old].Bounds);};
        }
    }
}
