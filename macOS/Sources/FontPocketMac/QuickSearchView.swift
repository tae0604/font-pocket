import SwiftUI
import AppKit
import FontPocketCore

struct QuickSearchView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.dismiss) var dismiss
    @State private var query=""
    @State private var selection: String?
    @State private var detail: FontFace?
    @FocusState private var focused: Bool
    private var results: [FontFace] {
        model.fonts.filter { face in
            let note=model.personal.annotations[face.id] ?? FontAnnotation()
            return query.isEmpty || (face.title+" "+face.version+" "+face.format+" "+face.file.lastPathComponent+" "+note.note+" "+note.tags.joined(separator:" ")).localizedCaseInsensitiveContains(query)
        }
    }
    private var selected: FontFace? { results.first { $0.id == selection } }
    var body: some View {
        VStack(spacing:12) {
            HStack {
                Image(systemName:"magnifyingglass")
                TextField(model.t("이름·메모·태그 검색", "Search name, notes or tags"),text:$query).textFieldStyle(.plain).font(.title3).focused($focused)
                    .onSubmit { if let face=selected ?? results.first { detail=face } }
                Button(model.t("닫기", "Done")) { dismiss() }.keyboardShortcut(.cancelAction)
            }
            List(selection:$selection) {
                ForEach(results.prefix(200)) { face in
                    HStack {
                        Text(face.title)
                        Spacer()
                        Text(face.state == .disabled ? model.t("사용 중지", "Disabled") : model.t("사용 중", "In use")).foregroundStyle(.secondary)
                        Text(face.format).foregroundStyle(.secondary)
                    }.tag(face.id).padding(.vertical,5)
                }
            }.contextMenu(forSelectionType:String.self) { ids in
                if let face=results.first(where:{ids.contains($0.id)}) {
                    Button(model.t("상세 정보", "Details")) { detail=face }
                    Button(model.t("관리 화면에서 보기", "Show in manager")) { model.showInManager(face); dismiss() }
                }
            } primaryAction: { ids in detail=results.first { ids.contains($0.id) } }
            HStack {
                Text(model.t("검색 결과 \(results.count)개", "\(results.count) results")).font(.caption).foregroundStyle(.secondary)
                Spacer()
                Button(model.t("사용", "Enable")) { change(true) }.disabled(selected?.canManage != true || selected?.state != .disabled || model.busy)
                Button(model.t("사용 중지", "Disable")) { change(false) }.disabled(selected?.canManage != true || selected?.state != .enabled || model.busy)
                Button(model.t("관리 화면", "Show in manager")) { if let face=selected { model.showInManager(face); dismiss() } }.disabled(selected == nil)
                Button(model.t("상세 정보", "Details")) { detail=selected }.disabled(selected == nil)
            }
            if results.count>200 { Text(model.t("처음 200개를 표시합니다. 검색어를 더 입력하세요.","Showing the first 200 results. Refine your search to see more.")).font(.caption).foregroundStyle(.secondary) }
            if model.busy { TaskControls() }
        }.padding(20).frame(width:760,height:500)
            .onAppear { focused=true; selection=results.first?.id }
            .onChange(of:query) { _ in selection=results.first?.id }
            .sheet(item:$detail) { face in DetailsView(face:face).environmentObject(model) }
    }
    private func change(_ enabled: Bool) {
        guard let face=selected else { return }
        model.showInManager(face)
        model.changeEnabled(enabled)
    }
}
