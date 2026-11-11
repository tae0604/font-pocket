import SwiftUI
import AppKit
import FontPocketCore

/// Context menus receive the row IDs from Table. Right-clicking an unselected
/// row must act on that row, rather than silently deleting the previous selection.
struct FontContextMenu: View {
    @EnvironmentObject var model: AppModel
    let ids: Set<String>
    let compare: () -> Void
    private var faces: [FontFace] { model.fonts.filter { ids.contains($0.id) } }
    private func act(_ action: () -> Void) { model.selection=ids; action() }
    var body: some View {
        Group {
            Button(model.t("상세 정보", "Details")) { act { model.details=true } }.disabled(faces.isEmpty)
            Button(model.t("비교", "Compare")) { act(compare) }.disabled(!FontOrdering.comparisonAllowed(faces))
            Button(model.t("즐겨찾기 전환", "Toggle favorite")) { act { model.toggleFavorites() } }.disabled(faces.isEmpty || model.busy)
            Button(model.t("메모·태그", "Notes / tags")) { act { model.openTools("notes") } }.disabled(faces.isEmpty)
            Button(model.t("버전 관리", "Versions")) { act { model.openTools("versions") } }.disabled(faces.isEmpty)
            Button(model.t("버전으로 보관", "Archive version")) { act { model.archiveSelection() } }.disabled(faces.isEmpty || model.busy)
            Button(model.t("파일 위치 열기", "Show in Finder")) { NSWorkspace.shared.activateFileViewerSelecting(faces.map(\.file)) }.disabled(faces.isEmpty)
            Divider()
            Button(model.t("기존 사용자 폰트 관리…", "Manage existing user fonts…")) { act { model.adoptSelected() } }.disabled(!faces.contains(where: \.canAdopt) || model.busy)
            Button(model.t("사용", "Enable")) { act { model.changeEnabled(true) } }.disabled(!faces.contains { $0.canManage && $0.state == .disabled } || model.busy)
            Button(model.t("사용 중지", "Disable")) { act { model.changeEnabled(false) } }.disabled(!faces.contains { $0.canManage && $0.state == .enabled } || model.busy)
            Button(model.t("선택 백업", "Back up selection")) { act { model.backupSelected() } }.disabled(faces.isEmpty || model.busy)
            Button(model.t("삭제…", "Delete…"), role: .destructive) { act { model.deleteSelected() } }.disabled(!faces.contains(where: \.canManage) || model.busy)
            Divider()
            Button(model.t("표시된 항목 선택", "Select visible fonts")) { model.selection=Set(model.visibleFonts.map(\.id)) }
            Button(model.t("선택 해제", "Clear selection")) { model.selection=[] }.disabled(model.selection.isEmpty)
            Menu(model.t("라이브러리", "Library")) {
                Button(model.t("모음 관리", "Collections")) { model.openTools("collections") }
                Button(model.t("현재 모음 사용", "Enable current collection")) { model.selection=Set(model.visibleFonts.map(\.id));model.changeEnabled(true) }.disabled(model.activeCollection == nil && !model.onlyFavorites)
                Button(model.t("현재 모음 사용 중지", "Disable current collection")) { model.selection=Set(model.visibleFonts.map(\.id));model.changeEnabled(false) }.disabled(model.activeCollection == nil && !model.onlyFavorites)
                Button(model.t("중복 정리", "Duplicates")) { model.openTools("duplicates") }
                Button(model.t("전체 견본·인쇄", "Catalog / print")) { model.openTools("catalog") }
                Button(model.t("폰트 파일 검사", "Inspect font files")) { model.openTools("inspect") }
                Divider()
                Button(model.t("환경 백업", "Back up environment")) { model.profileBackup() }
                Button(model.t("환경 복원", "Restore environment")) { model.profileRestore() }
                Button(model.t("삭제 폰트 복원", "Restore removed fonts")) { model.restoreDeleted() }
            }.disabled(model.busy)
        }
    }
}
