using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    public partial class MainWindow {
        CheckBox showDisabledFonts = new CheckBox();
        Control CreateActivationTools() {
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            tools.Controls.Add(MakeButton("활성화", 100, async delegate { await ToggleManagedFonts(true); }, false));
            tools.Controls.Add(MakeButton("비활성화", 110, async delegate { await ToggleManagedFonts(false); }, false));
            tools.Controls.Add(MakeButton("선택 폰트 비교", 135, delegate { CompareManagedFonts(); }, false));
            tools.Controls.Add(new Label { Text = "활성화 전환은 내 계정 폰트에만 적용", AutoSize = true, ForeColor = Color.FromArgb(115, 122, 134), Margin = new Padding(12, 8, 0, 0) });
            return tools;
        }
        List<ManagedFont> ChosenManagedFonts() {
            return managerList.SelectedItems.Cast<ListViewItem>().Select(x => (ManagedFont)x.Tag).ToList();
        }
        async Task ToggleManagedFonts(bool enable,List<ManagedFont> targets=null) {
            if (busy) return;
            var chosen = (targets??ChosenManagedFonts()).Where(x => x.CanRemove && x.Disabled == enable).ToList();
            if (chosen.Count == 0) { status.Text = enable ? "활성화할 비활성 폰트를 선택하세요." : "비활성화할 내 계정 폰트를 선택하세요."; return; }
            if (!enable && LocalizedMessage.Show(this, chosen.Count + "개 폰트를 비활성화할까요?\r\n폰트 파일과 등록 정보를 보관해 다시 활성화할 수 있습니다.\r\n열려 있는 앱에는 재실행 후 반영될 수 있습니다.", "폰트 비활성화", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            ClearFontPreview(); SetBusy(true); int changed = 0, failed = 0;
            try {
                await Task.Run(delegate {
                    var repository = ActivationRepository.Default; var store = new WindowsFontActivationStore();
                    foreach (var font in chosen) {
                        if (cancelled) break;
                        try {
                            if (enable) FontActivation.Enable(font.DisabledRecord, store, repository); else FontActivation.Disable(font, store, repository);
                            changed++; UI(delegate { Log(font.Name + (enable ? " · 활성화 완료" : " · 비활성화 완료")); });
                        } catch (Exception ex) { failed++; UI(delegate { Log(font.Name + " · 전환 실패: " + ex.Message); }); }
                        UI(delegate { progress.Value = (changed + failed) * 100 / chosen.Count; status.Text = (changed + failed) + " / " + chosen.Count + (enable ? " 활성화 중" : " 비활성화 중"); });
                    }
                    if (changed > 0) Engine.Notify();
                });
                status.Text = (enable ? "활성화" : "비활성화") + " 완료 · 변경 " + changed + " / 실패 " + failed; Log(status.Text);
            } finally { SetBusy(false); if (changed > 0) { managerLoaded = false; backupLoaded = false; foreach (var f in managedFonts) f.Checked = false; } }
            if (changed > 0) await LoadManagedFonts();
        }
        List<ManagedFont> ReadDisabledFonts(List<ManagedFont> activeFonts) {
            var result = new List<ManagedFont>(); var repository = ActivationRepository.Default;
            foreach (var record in repository.ReadAll(s => UI(delegate { Log("비활성 폰트 상태: " + s); }))) {
                // A pending operation may already be fully registered after a prior interruption.
                if (activeFonts.Any(f => f.UserEntries.Count > 0 && record.Entries.All(e => f.UserEntries.Any(a => a.Name == e.Name && a.Data == e.Data && a.Kind == e.Kind)))) continue;
                result.Add(new ManagedFont { Path = repository.FontFile(record), Name = record.Name, Format = Path.GetExtension(record.FileName).TrimStart('.').ToUpperInvariant(), Disabled = true, DisabledRecord = record, UserEntries = record.Entries });
            }
            return result;
        }
    }
}
