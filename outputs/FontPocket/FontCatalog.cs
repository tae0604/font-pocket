using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    class CatalogCanvas : ScrollableControl {
        public List<ManagedFont> Fonts=new List<ManagedFont>(); public string Sample=FontSamples.Default; public int Columns=1; public float SampleSize=24; public Action<ManagedFont> Open;
        Dictionary<string,PreviewFont> cache=new Dictionary<string,PreviewFont>(StringComparer.OrdinalIgnoreCase);Dictionary<string,long> cacheSizes=new Dictionary<string,long>(); Queue<string> order=new Queue<string>(); int rowHeight=175;long cachedBytes;string layoutKey;
        public CatalogCanvas() { AutoScroll=true; DoubleBuffered=true; BackColor=Color.White; SetStyle(ControlStyles.ResizeRedraw,true); }
        int ContentWidth {get {return Math.Max(100,Width-SystemInformation.VerticalScrollBarWidth);}}
        public void RefreshLayout() {string key=Sample+"|"+Columns+"|"+SampleSize+"|"+ContentWidth+"|"+Fonts.Count;if(layoutKey!=key) {layoutKey=key;rowHeight=175;}AutoScrollMinSize=new Size(0,((Fonts.Count+Columns-1)/Columns)*rowHeight); Invalidate(); }
        public void SetSize(float size) { SampleSize=size; foreach(var font in cache.Values) if(font!=null) font.Dispose(); cache.Clear();cacheSizes.Clear();cachedBytes=0; order.Clear(); RefreshLayout(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); RefreshLayout(); }
        PreviewFont FontFor(string path) { PreviewFont f; if(cache.TryGetValue(path,out f)) return f;long bytes=0; try { f=new PreviewFont(path,SampleSize);bytes=new FileInfo(path).Length; } catch { f=null; } cache[path]=f;cacheSizes[path]=bytes;cachedBytes+=bytes; order.Enqueue(path); while(order.Count>1 && (order.Count>20 || cachedBytes>64L*1024*1024)) { string oldest=order.Dequeue(); if(cache[oldest]!=null) cache[oldest].Dispose();cachedBytes-=cacheSizes[oldest];cacheSizes.Remove(oldest); cache.Remove(oldest); } return f; }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit; int width=Math.Max(100,ContentWidth/Columns),offset=AutoScrollPosition.Y,first=Math.Max(0,-offset/rowHeight),end=Math.Min((Fonts.Count+Columns-1)/Columns,first+ClientSize.Height/rowHeight+2);
            int measured=rowHeight;
            for(int row=first;row<end;row++) for(int col=0;col<Columns;col++) {int index=row*Columns+col;if(index>=Fonts.Count) break;var preview=FontFor(Fonts[index].Path);if(preview!=null) measured=Math.Max(measured,(int)Math.Ceiling(e.Graphics.MeasureString(Sample,preview.Font,Math.Max(40,width-28)).Height)+64);}
            if(measured!=rowHeight) {int anchor=-offset/rowHeight;int within=-offset%rowHeight;rowHeight=measured;AutoScrollMinSize=new Size(0,((Fonts.Count+Columns-1)/Columns)*rowHeight);AutoScrollPosition=new Point(0,anchor*rowHeight+within);offset=AutoScrollPosition.Y;first=Math.Max(0,-offset/rowHeight);end=Math.Min((Fonts.Count+Columns-1)/Columns,first+ClientSize.Height/rowHeight+2);}
            using(var border=new Pen(Color.FromArgb(232,235,239))) using(var ink=new SolidBrush(Color.FromArgb(32,38,48))) {
                for(int row=first;row<end;row++) for(int col=0;col<Columns;col++) { int index=row*Columns+col; if(index>=Fonts.Count) break; var f=Fonts[index]; var r=new Rectangle(col*width,row*rowHeight+offset,width,rowHeight); e.Graphics.DrawRectangle(border,r); TextRenderer.DrawText(e.Graphics,f.Name+(f.Disabled?" · 비활성":""),Font,new Rectangle(r.X+14,r.Y+10,r.Width-28,24),Color.DimGray,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine); var p=FontFor(f.Path); if(p!=null) e.Graphics.DrawString(Sample,p.Font,ink,new RectangleF(r.X+14,r.Y+42,r.Width-28,r.Height-54)); else TextRenderer.DrawText(e.Graphics,"미리보기를 지원하지 않는 폰트입니다.",Font,new Point(r.X+14,r.Y+55),Color.Gray); }
            }
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); int width=Math.Max(100,ContentWidth/Columns),index=((e.Y-AutoScrollPosition.Y)/rowHeight)*Columns+Math.Min(Columns-1,e.X/width); if(index>=0 && index<Fonts.Count && Open!=null) Open(Fonts[index]); }
        protected override void Dispose(bool disposing) { if(disposing) foreach(var f in cache.Values) if(f!=null) f.Dispose(); base.Dispose(disposing); }
    }
    public class FontCatalogDialog : Form {
        public FontCatalogDialog(List<ManagedFont> fonts) {
            Text="전체 폰트 견본"; Size=new Size(1100,800); MinimumSize=new Size(860,560); StartPosition=FormStartPosition.CenterParent;Padding=new Padding(18);PocketTheme.Apply(this);
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=3,ColumnCount=1 }; layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); Controls.Add(layout);
            var sample=new TextBox { Text=FontSamples.Default,Multiline=true,Dock=DockStyle.Fill }; layout.Controls.Add(sample);
            var tools=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false }; var search=new TextBox { Width=200 }; tools.Controls.Add(new Label { Text="검색",AutoSize=true,Margin=new Padding(0,5,6,0) }); tools.Controls.Add(search);
            var view=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=120 }; view.Items.AddRange(new object[]{"문장 견본","두 열 견본","격자 보기","패밀리 순서"}); view.SelectedIndex=0; tools.Controls.Add(view);
            var size=new NumericUpDown { Minimum=12,Maximum=60,Value=24,Width=60 }; tools.Controls.Add(size); var count=new Label { AutoSize=true,Margin=new Padding(12,5,0,0) }; tools.Controls.Add(count); layout.Controls.Add(tools);
            var canvas=new CatalogCanvas { Dock=DockStyle.Fill,Fonts=fonts }; layout.Controls.Add(canvas); canvas.Open=delegate(ManagedFont f) { try { using(var detail=new FontDetailsDialog(f.Path,fonts)) detail.ShowDialog(this); } catch(Exception ex) { DialogUI.Error(this,ex); } };
            Action refresh=delegate { canvas.Fonts=FontOrdering.Order(fonts.Where(f=>f.Matches(search.Text))).ToList(); canvas.Columns=view.SelectedIndex==2?3:view.SelectedIndex==1?2:1; canvas.Sample=sample.Text; canvas.AutoScrollPosition=Point.Empty; count.Text=canvas.Fonts.Count+"개 · 더블클릭으로 상세 정보"; canvas.RefreshLayout(); };
            search.TextChanged+=delegate { refresh(); }; sample.TextChanged+=delegate { refresh(); }; view.SelectedIndexChanged+=delegate { refresh(); }; size.ValueChanged+=delegate { canvas.SetSize((float)size.Value); }; refresh();
        }
    }
    static class FontReports {
        public static async Task Print(IWin32Window owner,List<ManagedFont> fonts) {
            if(fonts.Count==0) return;
            using(var form=DialogUI.Form("폰트 견본 인쇄",560,245)) {
                var layout=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));form.Controls.Add(layout);
                var label=new Label { Text="인쇄할 견본 종류를 선택하세요.\r\nWindows 인쇄 창에서 미리 확인한 뒤 인쇄하거나 PDF로 저장합니다.",Dock=DockStyle.Fill }; layout.Controls.Add(label);
                var mode=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top }; mode.Items.AddRange(new object[]{"목록 견본","여러 크기 견본","전체 문자표"}); mode.SelectedIndex=0; layout.Controls.Add(mode);
                var go=DialogUI.Button("인쇄",delegate { form.DialogResult=DialogResult.OK; },true);layout.Controls.Add(go); if(form.ShowDialog(owner)!=DialogResult.OK) return;
                await NativePrintSession.Show((Control)owner,fonts,mode.SelectedIndex);
            }
        }
    }
}
