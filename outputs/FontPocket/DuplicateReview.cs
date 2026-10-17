using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FontPocket {
    public class DuplicateGroup {
        public List<ManagedFont> Fonts;
        public ManagedFont Keeper;
        public bool Exact, Selected;
        public bool Readable { get { return Fonts.All(x => !String.IsNullOrEmpty(x.Hash)); } }
        public List<ManagedFont> Removals { get { return Fonts.Where(x => x.CanRemove && x != Keeper).ToList(); } }
    }
    public static class DuplicateReview {
        public static List<DuplicateGroup> Groups(List<ManagedFont> fonts, bool preferOtf) {
            var parent = new Dictionary<ManagedFont, ManagedFont>(); foreach (var f in fonts) parent[f] = f;
            Func<ManagedFont, ManagedFont> root = null;
            root = x => parent[x] == x ? x : (parent[x] = root(parent[x]));
            Action<IEnumerable<ManagedFont>> join = values => { var list = values.ToList(); for (int i = 1; i < list.Count; i++) parent[root(list[i])] = root(list[0]); };
            foreach (var g in fonts.Where(x => !String.IsNullOrEmpty(x.Hash)).GroupBy(x => x.Hash).Where(x => x.Count() > 1)) join(g);
            foreach (var g in fonts.Where(x => x.Identity != null).GroupBy(x => x.Identity).Where(x => x.Count() > 1)) join(g);
            var result = new List<DuplicateGroup>();
            foreach (var g in fonts.GroupBy(root).Where(x => x.Count() > 1)) {
                var members = g.ToList();
                var keeper = members.OrderBy(x => x.CanRemove ? 1 : 0).ThenBy(x => Selection.Extension(x.Path) == (preferOtf ? ".otf" : ".ttf") ? 0 : 1).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).First();
                bool exact = members.All(x => !String.IsNullOrEmpty(x.Hash)) && members.Select(x => x.Hash).Distinct().Count() == 1;
                var group = new DuplicateGroup { Fonts = members, Keeper = keeper, Exact = exact };
                group.Selected = exact && group.Removals.Count > 0; result.Add(group);
            }
            return result.OrderBy(x => x.Keeper.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
    public partial class MainWindow {
        List<DuplicateGroup> ChooseDuplicateCleanup(List<DuplicateGroup> groups) {
            using (var dialog = new Form { Text = "중복 폰트 정리", Size = new Size(1020, 560), MinimumSize = new Size(900, 420), StartPosition = FormStartPosition.CenterParent, Font = Font, BackColor = BackColor }) {
                PocketTheme.Apply(dialog);
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                layout.Controls.Add(new Label { Text = "정리할 그룹을 체크하고 남길 폰트를 확인하세요. 더블클릭하면 남길 파일을 바꿀 수 있습니다.\r\n내용이 완전히 같은 그룹만 기본 체크합니다. 형식·버전이 다르면 직접 체크해야 하며, 시스템 폰트는 남깁니다.", Dock = DockStyle.Fill });
                var list = new ListView { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false, ShowItemToolTips = true };
                list.Columns.Add("그룹", 300); list.Columns.Add("판정", 135); list.Columns.Add("남길 폰트", 350); list.Columns.Add("삭제", 90);
                Action<ListViewItem, DuplicateGroup> update = (row, group) => {
                    string number = System.Text.RegularExpressions.Regex.Match(group.Keeper.Version ?? "", @"\d+(?:\.\d+)+").Value;
                    row.SubItems[2].Text = group.Keeper.Name + " [" + group.Keeper.Format + " " + number + "]";
                    row.SubItems[3].Text = group.Removals.Count + "개"; row.ToolTipText = "남길 파일: " + group.Keeper.Path;
                };
                foreach (var group in groups) {
                    var row = new ListViewItem(group.Keeper.Name + " · " + group.Fonts.Count + "개") { Tag = group, Checked = group.Selected };
                    row.SubItems.Add(!group.Readable ? "읽기 실패 · 제외" : group.Exact ? "완전 동일" : "이름 겹침 · 확인 필요"); row.SubItems.Add(""); row.SubItems.Add(""); update(row, group); list.Items.Add(row);
                }
                list.ItemCheck += delegate(object sender, ItemCheckEventArgs e) {
                    var group = (DuplicateGroup)list.Items[e.Index].Tag;
                    if (e.NewValue == CheckState.Checked && (!group.Readable || group.Removals.Count == 0)) { e.NewValue = CheckState.Unchecked; return; }
                    group.Selected = e.NewValue == CheckState.Checked;
                };
                list.DoubleClick += delegate { if (list.SelectedItems.Count > 0) { var row = list.SelectedItems[0]; var group = (DuplicateGroup)row.Tag; ChooseKeeper(group, dialog); update(row, group); } };
                layout.Controls.Add(list);
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
                var confirm = new PocketButton { Text = "선택한 그룹 정리", Primary = true, Width = 160, Height = 34, DialogResult = DialogResult.OK };
                var cancel = new PocketButton { Text = "취소", Width = 85, Height = 34, DialogResult = DialogResult.Cancel };
                var change = new PocketButton { Text = "남길 폰트 변경", Width = 145, Height = 34 };
                change.Click += delegate { if (list.SelectedItems.Count > 0) { var row = list.SelectedItems[0]; var group = (DuplicateGroup)row.Tag; ChooseKeeper(group, dialog); update(row, group); } };
                actions.Controls.Add(confirm); actions.Controls.Add(cancel); actions.Controls.Add(change); layout.Controls.Add(actions); dialog.Controls.Add(layout); dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.OK ? groups.Where(x => x.Selected && x.Readable).ToList() : null;
            }
        }
        void ChooseKeeper(DuplicateGroup group, IWin32Window owner) {
            using (var dialog = new Form { Text = "남길 폰트 선택", Size = new Size(840, 400), StartPosition = FormStartPosition.CenterParent, Font = Font, BackColor = BackColor }) {
                PocketTheme.Apply(dialog);
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                layout.Controls.Add(new Label { Text = "형식과 버전을 보고 남길 파일을 선택하세요. 시스템 폰트가 있으면 시스템 폰트를 남겨야 합니다.", Dock = DockStyle.Fill });
                var choices = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
                foreach (var font in group.Fonts) choices.Items.Add(font.Name + " · " + font.Format + " · " + font.Version + " · " + font.Scope + " · " + System.IO.Path.GetFileName(font.Path));
                choices.SelectedIndex = group.Fonts.IndexOf(group.Keeper); layout.Controls.Add(choices);
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
                var ok = new PocketButton { Text = "이 폰트 남기기", Primary = true, Width = 145, Height = 34 };
                ok.Click += delegate {
                    if (choices.SelectedIndex < 0) return;
                    var selected = group.Fonts[choices.SelectedIndex];
                    if (group.Fonts.Any(x => !x.CanRemove) && selected.CanRemove) { LocalizedMessage.Show(dialog, "시스템 폰트는 삭제할 수 없어 시스템 폰트를 남겨야 합니다.", "보호된 폰트"); return; }
                    group.Keeper = selected; dialog.DialogResult = DialogResult.OK;
                };
                var cancel = new PocketButton { Text = "취소", DialogResult = DialogResult.Cancel, Width = 85, Height = 34 }; actions.Controls.Add(ok); actions.Controls.Add(cancel); layout.Controls.Add(actions); dialog.Controls.Add(layout); dialog.CancelButton = cancel; dialog.ShowDialog(owner);
            }
        }
    }
}
