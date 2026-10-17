using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace FontPocket {
    public partial class MainWindow {
        bool adjustingChecks;
        Dictionary<string, DownloadLink> downloadOrigins = new Dictionary<string, DownloadLink>(StringComparer.OrdinalIgnoreCase);
        bool Confirm(string message) { return LocalizedMessage.Show(this, message, "폰트 선택", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes; }
        void HandleItemCheck(object sender, ItemCheckEventArgs e) {
            if (adjustingChecks) return;
            var item = (FontItem)list.Items[e.Index].Tag;
            if (e.NewValue == CheckState.Checked && !Selection.AcceptOverride(item, FormatMode, includeTtc.Checked, Confirm)) { e.NewValue = CheckState.Unchecked; return; }
            item.ForceSelected = e.NewValue == CheckState.Checked;
            if (!item.ForceSelected) item.FormatOverride = false;
            else list.Items[e.Index].SubItems[4].Text = item.FormatOverride ? "확인 후 개별 선택" : "직접 선택";
        }
        void ConfirmMissingFormat(List<FontItem> added) {
            var valid = added.Where(x => x.Metadata != null).ToList();
            if (cancelled || valid.Count == 0 || valid.Any(x => Selection.CanInstall(x, FormatMode, includeTtc.Checked))) return;
            string formats = String.Join(" / ", valid.Select(x => Selection.Extension(x.Path).TrimStart('.').ToUpperInvariant()).Distinct().ToArray());
            if (Confirm("새로 추가한 폰트는 " + formats + " 형식입니다.\r\n현재 형식 설정과 다릅니다. 이 폰트도 선택할까요?\r\n\r\n다른 폰트의 선택 설정은 유지됩니다."))
                foreach (var item in valid) item.FormatOverride = true;
        }
        bool ConfirmLinkOverride(DownloadLink link, IWin32Window owner) {
            if (Selection.LinkAllowed(link, FormatMode, includeTtc.Checked) || link.FormatOverride) return true;
            int f = Selection.LinkFormats(link); string label = f == 1 ? "TTF" : f == 2 ? "OTF" : f == 4 ? "TTC" : "다른";
            if (LocalizedMessage.Show(owner, "이 파일은 " + label + " 형식입니다.\r\n현재 설정과 관계없이 이 파일도 선택할까요?\r\n\r\n" + link.FileName, "다운로드 선택", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
            link.FormatOverride = true; return true;
        }
    }
}
