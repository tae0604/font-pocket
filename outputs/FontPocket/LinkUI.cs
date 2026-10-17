using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    public partial class MainWindow {
        TextBox links = new TextBox();
        RadioButton ttfOnly = new RadioButton(), otfOnly = new RadioButton(), bothFormats = new RadioButton();
        CheckBox includeTtc = new CheckBox();
        FontFormat FormatMode { get { return bothFormats.Checked ? FontFormat.Both : otfOnly.Checked ? FontFormat.OtfOnly : FontFormat.TtfOnly; } }
        Control CreateFormatPanel() {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };
            panel.Controls.Add(new Label { Text = "기본 파일 형식", AutoSize = true, Margin = new Padding(0, 4, 20, 0) });
            ttfOnly.Text = "TTF만"; otfOnly.Text = "OTF만"; bothFormats.Text = "TTF + OTF 둘 다";
            ttfOnly.Checked = true;
            foreach (var radio in new RadioButton[] { ttfOnly, otfOnly, bothFormats }) {
                radio.AutoSize = true; radio.Margin = new Padding(0, 3, 22, 0);
                radio.CheckedChanged += delegate(object sender, EventArgs e) { if (((RadioButton)sender).Checked && !busy) ApplyRecommendations(); };
                panel.Controls.Add(radio); controls.Add(radio);
            }
            includeTtc.Text = "TTC도 포함"; includeTtc.AutoSize = true; includeTtc.Margin = new Padding(12, 3, 0, 0);
            includeTtc.CheckedChanged += delegate { if (!busy) ApplyRecommendations(); }; panel.Controls.Add(includeTtc); controls.Add(includeTtc);
            return panel;
        }
        Control CreateLinkPanel() {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var title = new Label { Text = "폰트 주소", AutoSize = true, Margin = Padding.Empty }; panel.Controls.Add(title, 0, 0); panel.SetColumnSpan(title, 3);
            links.Multiline = true; links.ScrollBars = ScrollBars.Vertical; links.Dock = DockStyle.Fill; links.Margin = new Padding(0, 0, 12, 8); links.Font = new Font("Segoe UI", 10);
            panel.Controls.Add(links, 0, 1); panel.SetRowSpan(links, 2); controls.Add(links);
            var fetch = MakeButton("대기열에 추가", 150, async delegate { await FromLinks(false); }, true); fetch.Dock = DockStyle.Fill; panel.Controls.Add(fetch, 1, 1);
            var install = MakeButton("바로 설치", 155, async delegate { await FromLinks(true); }, false); install.Dock = DockStyle.Fill; panel.Controls.Add(install, 2, 1);
            var hint = new Label { Text = "눈누 · 공식 사이트 · 파일 주소\r\n여러 주소는 줄바꿈해서 붙여넣기", AutoSize = true, ForeColor = Color.FromArgb(115, 124, 138), Font = new Font(Font.FontFamily, 8.5f), Margin = new Padding(0, 2, 0, 0) }; panel.Controls.Add(hint, 1, 2); panel.SetColumnSpan(hint, 2);
            return panel;
        }
        async Task FromLinks(bool install) {
            if (busy) return;
            string[] addresses = links.Text.Split(new char[] { '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
            if (addresses.Length == 0) { status.Text = "눈누, 공식 폰트 사이트 또는 ZIP / 폰트 다운로드 주소를 붙여넣어 주세요."; links.Focus(); return; }
            if (addresses.Length > 30) { status.Text = "주소는 한 번에 30개까지 넣어 주세요."; return; }
            var downloaded = new List<string>(); SetBusy(true); bool wasCancelled = false;
            try {
                foreach (string address in addresses) {
                    if (cancelled) break;
                    DownloadPlan plan = null;
                    try {
                        plan = await Task.Run(() => NoonnuDownload.Resolve(address, () => cancelled, s => UI(delegate { status.Text = s; })));
                        Log("입력: " + plan.Input + " → 다운로드 페이지: " + plan.Source);
                        for (int step = 0; plan.Files.Count == 0 && step < 10 && !cancelled; step++) {
                            Log("설치용 파일을 찾지 못했습니다: " + plan.Source);
                            string nextPage = ChooseNextPage(plan);
                            if (nextPage == null) break;
                            plan = await Task.Run(() => NoonnuDownload.Resolve(nextPage, () => cancelled, s => UI(delegate { status.Text = s; })));
                            Log("추가 탐색: " + plan.Source);
                        }
                        if (plan.Files.Count == 0 || cancelled) continue;
                        List<DownloadLink> chosen = plan.Files.Count == 1 && Selection.LinkAllowed(plan.Files[0], FormatMode, includeTtc.Checked) ? plan.Files : ChooseDownloads(plan);
                        if (chosen == null) continue;
                        foreach (var link in chosen) {
                            if (cancelled) break;
                            DateTime lastUpdate = DateTime.MinValue;
                            status.Text = "다운로드 중 · " + link.Title;
                            Log("다운로드 시작: " + link.ToString());
                            string file = await Task.Run(() => NoonnuDownload.Download(link, Path.Combine(tempRoot, "downloads"), () => cancelled, (done, total) => {
                                if ((DateTime.UtcNow - lastUpdate).TotalMilliseconds < 200) return; lastUpdate = DateTime.UtcNow;
                                UI(delegate { status.Text = "다운로드 " + (done / 1048576.0).ToString("N1") + " MB · " + link.Title; if (total > 0) progress.Value = (int)Math.Min(100, done * 100 / total); });
                            }));
                            downloaded.Add(file); downloadOrigins[file] = link; Log("다운로드 완료: " + link.ToString() + " · " + link.Url);
                        }
                    } catch (OperationCanceledException) { cancelled = true; break; }
                    catch (Exception ex) { Log(address + ": " + ex.Message + (plan == null ? "" : " · 제작사: " + plan.Source)); }
                }
                wasCancelled = cancelled;
                status.Text = cancelled ? "다운로드를 중단했습니다." : downloaded.Count == 0 ? "다운로드된 파일이 없습니다 · 작업 기록을 확인하세요." : "다운로드 완료 · " + downloaded.Count + "개 파일";
            } finally { SetBusy(false); }
            if (downloaded.Count == 0) return;
            await AddPaths(downloaded.ToArray());
            if (!install && !wasCancelled && !cancelled) status.Text = "대기열에 추가했습니다. 설치할 폰트를 선택하세요.";
            if (install && !wasCancelled && !cancelled) await InstallAll();
        }
        string ChooseNextPage(DownloadPlan plan) {
            using (var dialog = new Form { Text = "다운로드 페이지 더 찾기", Size = new Size(840, 520), MinimumSize = new Size(700, 430), StartPosition = FormStartPosition.CenterParent, Font = Font, BackColor = Color.FromArgb(247, 248, 250) }) {
                PocketTheme.Apply(dialog);
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, Padding = new Padding(18) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                layout.Controls.Add(new Label { Text = "파일을 찾지 못했어요. 아래에서 다음 페이지를 고르거나 더 안쪽 페이지 주소를 넣어 주세요.\r\n로그인·동의나 동적 버튼이 필요하면 원본 사이트에서 확인할 수 있습니다.", Dock = DockStyle.Fill });
                var origin = new LinkLabel { Text = "원본 사이트 열기: " + plan.Source, Dock = DockStyle.Fill, AutoEllipsis = true };
                origin.LinkClicked += delegate { Process.Start(new ProcessStartInfo(plan.Source) { UseShellExecute = true }); }; layout.Controls.Add(origin);
                var pages = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
                foreach (var link in plan.Pages) pages.Items.Add(link);
                layout.Controls.Add(pages);
                layout.Controls.Add(new Label { Text = "찾아볼 주소 (목록 선택 또는 직접 붙여넣기)", Dock = DockStyle.Fill });
                var address = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10) }; layout.Controls.Add(address);
                pages.SelectedIndexChanged += delegate { if (pages.SelectedItem != null) address.Text = ((DownloadLink)pages.SelectedItem).Url; };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 7, 0, 0) };
                var ok = new PocketButton { Text = "이 주소에서 다시 찾기", Primary = true, Width = 180, Height = 36 };
                ok.Click += delegate {
                    try { address.Text = NoonnuDownload.InputUri(address.Text).AbsoluteUri; dialog.DialogResult = DialogResult.OK; }
                    catch (Exception ex) { LocalizedMessage.Show(dialog, ex.Message, "주소 확인"); }
                };
                var back = new PocketButton { Text = "건너뛰기", DialogResult = DialogResult.Cancel, Width = 95, Height = 36 };
                buttons.Controls.Add(ok); buttons.Controls.Add(back); layout.Controls.Add(buttons); dialog.Controls.Add(layout); dialog.AcceptButton = ok; dialog.CancelButton = back;
                return dialog.ShowDialog(this) == DialogResult.OK ? address.Text : null;
            }
        }
        internal Form CreateDownloadDialog(DownloadPlan plan, out CheckedListBox choices) {
            var dialog = new Form { Text = "다운로드할 폰트 선택", Size = new Size(940, 480), MinimumSize = new Size(800, 380), StartPosition = FormStartPosition.CenterParent, Font = Font, BackColor = Color.FromArgb(247, 248, 250) };
            PocketTheme.Apply(dialog);
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(18) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                string modeLabel = FormatMode == FontFormat.TtfOnly ? "TTF만" : FormatMode == FontFormat.OtfOnly ? "OTF만" : "TTF + OTF 둘 다";
                layout.Controls.Add(new Label { Text = "기본 형식: " + modeLabel + " · 정적 / 가변을 확인해 선택하세요. 다른 형식도 확인 후 개별 선택할 수 있습니다.", Dock = DockStyle.Fill });
                layout.Controls.Add(new Label { Text = "제작사: " + plan.Source, Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.DimGray });
                var check = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true, BorderStyle = BorderStyle.None, Font = new Font(Font.FontFamily, 11) };
                var defaults = Selection.DefaultDownloads(plan.Files, FormatMode, includeTtc.Checked);
                for (int i = 0; i < plan.Files.Count; i++) check.Items.Add(plan.Files[i], defaults.Contains(plan.Files[i].Url));
                check.ItemCheck += delegate(object sender, ItemCheckEventArgs e) {
                    if (e.NewValue == CheckState.Checked && !ConfirmLinkOverride((DownloadLink)check.Items[e.Index], dialog)) {
                        e.NewValue = CheckState.Unchecked;
                    }
                };
                layout.Controls.Add(check);
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 7, 0, 0) };
                var ok = new PocketButton { Text = "선택 파일 받기", Primary = true, DialogResult = DialogResult.OK, Width = 145, Height = 36 };
                var back = new PocketButton { Text = "취소", DialogResult = DialogResult.Cancel, Width = 90, Height = 36 };
                buttons.Controls.Add(ok); buttons.Controls.Add(back); layout.Controls.Add(buttons); dialog.Controls.Add(layout); dialog.AcceptButton = ok; dialog.CancelButton = back;
                choices = check; return dialog;
            }
        }
        List<DownloadLink> ChooseDownloads(DownloadPlan plan) {
            CheckedListBox check;
            using (var dialog = CreateDownloadDialog(plan, out check))
                return dialog.ShowDialog(this) == DialogResult.OK ? check.CheckedItems.Cast<DownloadLink>().ToList() : null;
        }
    }
}
