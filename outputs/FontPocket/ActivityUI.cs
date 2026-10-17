using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FontPocket {
    public partial class MainWindow {
        ListBox recentLog = new ListBox();
        Label activeTask = new Label();
        Control CreateActivityPanel() {
            var panel = new Surface { Dock = DockStyle.Fill, Padding = new Padding(10, 5, 10, 5), Margin = Padding.Empty };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 19)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 19)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            header.Controls.Add(new Label { Text = "최근 작업", AutoSize = true, Font = new Font(Font.FontFamily, 8.5f), Margin = Padding.Empty });
            var full = new LinkLabel { Text = "전체 기록", AutoSize = true, Font = new Font(Font.FontFamily, 8.5f), Margin = Padding.Empty, LinkColor = Color.FromArgb(65, 90, 125) };
            full.LinkClicked += delegate { ShowPage(2); }; header.Controls.Add(full); layout.Controls.Add(header);
            activeTask.Dock = DockStyle.Fill; activeTask.AutoEllipsis = true; activeTask.Margin = Padding.Empty; activeTask.Font = new Font(Font.FontFamily, 8.5f);
            activeTask.ForeColor = Color.FromArgb(55, 70, 90); activeTask.Text = "대기 중"; layout.Controls.Add(activeTask);
            recentLog.Dock = DockStyle.Fill; recentLog.Margin = Padding.Empty; recentLog.BorderStyle = BorderStyle.None; recentLog.BackColor = Color.White;
            recentLog.Font = new Font(Font.FontFamily, 8.25f); recentLog.ForeColor = Color.FromArgb(110, 115, 125); recentLog.SelectionMode = SelectionMode.None; recentLog.IntegralHeight = false;
            layout.Controls.Add(recentLog); panel.Controls.Add(layout);
            status.TextChanged += delegate { activeTask.Text = status.Text; };
            return panel;
        }
        void AppendRecent(string message) {
            if (message.StartsWith("입력:") || message.StartsWith("추가 탐색:")) return;
            string compact = Regex.Replace(message, @"https?://\S+", "").Replace("\r", " ").Replace("\n", " ").Trim(' ', '·', ':', '→');
            if (compact.Length == 0) return;
            if (compact.Length > 160) compact = compact.Substring(0, 157) + "…";
            recentLog.BeginUpdate();
            try {
                recentLog.Items.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + compact);
                while (recentLog.Items.Count > 3) recentLog.Items.RemoveAt(0);
                recentLog.TopIndex = 0;
            } finally { recentLog.EndUpdate(); }
        }
    }
}
