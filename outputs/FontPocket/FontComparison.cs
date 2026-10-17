using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FontPocket {
    public static class FontSamples {
        public const string Default = "다람쥐 헌 쳇바퀴에 타고파  0123456789\r\nThe quick brown fox jumps over the lazy dog.";
    }
    public class FontSource {
        public string Path, Name, Origin, Family;
        public FontStyle? Style;
    }
    class FontPreviewCanvas : Panel {
        public FontPreviewCanvas() {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
    class FontPreviewTile : UserControl {
        FontSource source; PreviewFont session; Panel canvas = new FontPreviewCanvas(); Label title = new Label();
        string sample, error;
        public FontPreviewTile(FontSource font, string text, float size) {
            source = font; sample = text; BackColor = Color.White; Padding = new Padding(12); Margin = new Padding(0, 0, 10, 10);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
            title.Tag="OriginalMetadata"; title.Dock = DockStyle.Fill; title.AutoEllipsis = true; title.Text = font.Name + "  [" + System.IO.Path.GetExtension(font.Path).TrimStart('.').ToUpperInvariant() + "]"; layout.Controls.Add(title);
            canvas.Dock = DockStyle.Fill; canvas.Paint += delegate(object sender, PaintEventArgs e) {
                e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                if (session != null) using (var brush = new SolidBrush(Color.FromArgb(32, 38, 48))) e.Graphics.DrawString(sample, session.Font, brush, canvas.ClientRectangle);
                else TextRenderer.DrawText(e.Graphics, "미리보기 지원 안 됨 · " + error, Font, canvas.ClientRectangle, Color.DimGray, TextFormatFlags.WordBreak);
            }; layout.Controls.Add(canvas);
            layout.Controls.Add(new Label { Text = font.Origin + " · " + System.IO.Path.GetFileName(font.Path), Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.FromArgb(115, 121, 133), Font = new Font("맑은 고딕", 8) }); Controls.Add(layout);
            LoadFont(size);
        }
        public void SetSample(string value) { sample = value; canvas.Invalidate(); }
        public void LoadFont(float size) {
            if (session != null) { session.Dispose(); session = null; }
            try { session = new PreviewFont(source.Path, size, source.Family,source.Style); error = null; } catch (Exception ex) { error = ex.Message; }
            canvas.Invalidate();
        }
        public int PreferredHeight(int width) {
            if (session == null) return 180;
            using (var bitmap = new Bitmap(1, 1)) {
                bitmap.SetResolution(DeviceDpi,DeviceDpi);
                using(var g = Graphics.FromImage(bitmap)) {
                var size = g.MeasureString(sample, session.Font, new SizeF(Math.Max(150, width - 35), 20000));
                return Math.Max(190, Math.Min(6000, (int)Math.Ceiling(size.Height) + (int)Math.Ceiling(90*DeviceDpi/96.0)));
                }
            }
        }
        protected override void Dispose(bool disposing) { if (disposing && session != null) { session.Dispose(); session = null; } base.Dispose(disposing); }
    }
    public class FontComparison : Form {
        List<FontPreviewTile> tiles = new List<FontPreviewTile>();
        Panel scroll = new Panel(); TableLayoutPanel grid = new TableLayoutPanel(); TextBox text = new TextBox(); NumericUpDown size = new NumericUpDown();
        RadioButton oneColumn = new RadioButton(), twoColumns = new RadioButton(); bool ready, reflowing;
        public FontComparison(List<FontSource> fonts, string sample, string caption) {
            Text = caption; Size = new Size(fonts.Count == 1 ? 1050 : 1180, 720); MinimumSize = new Size(850, 520); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(250, 250, 250); Font = new Font("맑은 고딕", 9.5f);
            PocketTheme.Apply(this);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(18) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(layout);
            var input = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            text.Multiline = true; text.Dock = DockStyle.Fill; text.Text = sample; input.Controls.Add(text);
            size.Minimum = 12; size.Maximum = 72; size.Value = 24; size.Dock = DockStyle.Top; size.Margin = new Padding(12, 2, 0, 0); input.Controls.Add(size); layout.Controls.Add(input);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            oneColumn.Text = "한 열"; oneColumn.AutoSize = true; twoColumns.Text = "두 열"; twoColumns.AutoSize = true;
            if (fonts.Count == 1) oneColumn.Checked = true; else twoColumns.Checked = true;
            tools.Controls.Add(oneColumn); tools.Controls.Add(twoColumns); tools.Controls.Add(new Label { Text = fonts.Count + "개 폰트", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(15, 5, 0, 0) }); layout.Controls.Add(tools);
            scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; grid.Location = Point.Empty; grid.Margin = Padding.Empty; scroll.Controls.Add(grid); layout.Controls.Add(scroll);
            foreach (var font in fonts) { var tile = new FontPreviewTile(font, sample, 24); tiles.Add(tile); grid.Controls.Add(tile); }
            text.TextChanged += delegate { foreach (var tile in tiles) tile.SetSample(text.Text); Reflow(); };
            size.ValueChanged += delegate { foreach (var tile in tiles) tile.LoadFont((float)size.Value); Reflow(); };
            oneColumn.CheckedChanged += delegate { if (oneColumn.Checked) Reflow(); }; twoColumns.CheckedChanged += delegate { if (twoColumns.Checked) Reflow(); };
            scroll.Resize += delegate { Reflow(); }; Shown += delegate { Reflow(); }; ready = true;
        }
        void Reflow() {
            if (!ready || reflowing) return;
            reflowing = true;
            int columns = twoColumns.Checked ? 2 : 1, width = Math.Max(300, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 3);
            int rows = (tiles.Count + columns - 1) / columns, tileWidth = width / columns;
            grid.SuspendLayout();
            try {
                grid.ColumnCount = columns; grid.RowCount = rows; grid.ColumnStyles.Clear(); grid.RowStyles.Clear();
                for (int c = 0; c < columns; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
                int height = 0;
                for (int r = 0; r < rows; r++) {
                    int rowHeight = tiles.Skip(r * columns).Take(columns).Max(x => x.PreferredHeight(tileWidth));
                    grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight)); height += rowHeight;
                }
                for (int i = 0; i < tiles.Count; i++) { grid.SetCellPosition(tiles[i], new TableLayoutPanelCellPosition(i % columns, i / columns)); tiles[i].Dock = DockStyle.Fill; }
                grid.Size = new Size(width, height);
            } finally { grid.ResumeLayout(true); reflowing = false; }
            // Resizing a preview changes every line wrap. Repaint the whole subtree,
            // including areas Windows would otherwise preserve from the old layout.
            grid.PerformLayout();
            scroll.Invalidate(true);
            scroll.Update();
        }
    }
    public partial class MainWindow {
        void OpenComparison(List<FontSource> fonts, string title, string sample) {
            fonts = fonts.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
            if (fonts.Count == 0) { status.Text = "미리 볼 폰트를 선택하세요."; return; }
            if (fonts.Count > 12) { LocalizedMessage.Show(this, "한 번에 최대 12개 폰트를 비교할 수 있습니다. 체크한 폰트를 줄여 주세요.", "폰트 비교"); return; }
            long bytes = 0; foreach (var font in fonts) { try { bytes += new FileInfo(font.Path).Length; } catch { } }
            if (bytes > 256L * 1024 * 1024) { LocalizedMessage.Show(this, "선택한 파일의 총 용량이 256MB를 넘습니다. 비교할 폰트를 줄여 주세요.", "폰트 비교"); return; }
            using (var dialog = new FontComparison(fonts, sample, title)) dialog.ShowDialog(this);
        }
        void CompareManagedFonts() {
            OpenComparison(ChosenManagedFonts().Select(x => new FontSource { Path = x.Path, Name = x.Name, Origin = x.Disabled ? "비활성 폰트" : "설치된 폰트" }).ToList(), "폰트 비교", previewText.Text);
        }
        void PreviewQueuedFonts(bool compare) {
            var rows = (compare ? list.CheckedItems.Cast<ListViewItem>() : list.SelectedItems.Cast<ListViewItem>()).ToList();
            if (rows.Count == 0 && !compare) rows = list.CheckedItems.Cast<ListViewItem>().Take(1).ToList();
            var selected = rows.Select(x => (FontItem)x.Tag); if (!compare) selected = selected.Take(1);
            OpenComparison(selected.Select(x => new FontSource { Path = x.Path, Name = x.Metadata == null ? x.Name : x.Metadata.Display, Origin = "설치 대기열" }).ToList(), compare ? "설치 전 폰트 비교" : "설치 전 미리보기", FontSamples.Default);
        }
        internal FontComparison CreateComparisonPreview() {
            var fonts = items.Where(x => x.Metadata != null && !x.Metadata.Variable && Selection.Extension(x.Path) == ".ttf").Take(4).Select(x => new FontSource { Path = x.Path, Name = x.Metadata.Display, Origin = "설치 대기열 · 미설치 파일 미리보기" }).ToList();
            return new FontComparison(fonts, FontSamples.Default, "설치 전 폰트 비교");
        }
    }
}
