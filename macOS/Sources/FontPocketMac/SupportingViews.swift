import SwiftUI
import AppKit
import FontPocketCore

struct PreviewPane<Actions: View>: View {
    @EnvironmentObject var model: AppModel
    let face: FontFace?
    @ViewBuilder let actions: () -> Actions
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text(face?.title ?? model.t("폰트를 선택하면 미리 볼 수 있어요.", "Select a font to preview it.")).fontWeight(.semibold)
                Spacer()
                GlassActions { HStack(spacing: 8) { actions() }.pocketGlassButton() }
            }
            HStack(alignment: .top) {
                TextEditor(text: $model.previewText).font(.system(size: 12)).frame(height: 40).padding(5)
                    .overlay(RoundedRectangle(cornerRadius: 5).stroke(Color.secondary.opacity(0.2)))
                VStack(alignment: .trailing, spacing: 4) {
                    Text("\(Int(model.previewSize)) pt").font(.caption).foregroundStyle(.secondary)
                    Stepper("", value: $model.previewSize, in: 10...120, step: 2).labelsHidden()
                }.frame(width: 70)
            }
            ScrollView([.vertical, .horizontal]) {
                if let face { Text(model.previewText).font(Font(face.previewFont(size: model.previewSize))).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }
            }.frame(maxWidth: .infinity, maxHeight: .infinity)
        }.padding(16).background(Color(nsColor: .textBackgroundColor), in: RoundedRectangle(cornerRadius: 12))
    }
}

struct InstallView: View {
    @EnvironmentObject var model: AppModel
    @State private var details = false
    var body: some View {
        VStack(spacing: 14) {
            HStack(alignment: .top, spacing: 12) {
                TextEditor(text: $model.links).font(.system(size: 13)).frame(height: 65).padding(8)
                    .background(Color(nsColor: .textBackgroundColor), in: RoundedRectangle(cornerRadius: 6))
                    .overlay(RoundedRectangle(cornerRadius: 6).stroke(Color.secondary.opacity(0.18)))
                    .accessibilityLabel(model.t("폰트 주소 · 한 줄에 하나씩", "Font URLs · one per line"))
                VStack(alignment: .leading, spacing: 8) {
                    Button(model.t("대기열에 추가", "Add to queue")) { model.downloadLinks() }.pocketPrimary().disabled(model.busy || model.links.isEmpty)
                    Text("Noonnu · Google Fonts\nHTTPS · ZIP · TTF · OTF").font(.caption).foregroundStyle(.secondary)
                }.frame(width: 180, alignment: .leading)
            }
            HStack {
                Button(model.t("파일·ZIP·폴더 추가", "Add files, ZIP or folder")) { model.openFiles() }.disabled(model.busy)
                Button(model.t("전체 선택", "Select all")) { model.checked = Set(model.queue.map(\.id)) }.disabled(model.busy)
                Button(model.t("선택 해제", "Clear checks")) { model.checked = [] }.disabled(model.busy)
                Spacer()
                Button(model.t("대기열 비우기", "Clear queue")) { model.queue = []; model.checked = []; model.queueSelection = [] }.disabled(model.busy)
            }
            Table(model.queue, selection: $model.queueSelection) {
                TableColumn("") { face in
                    Toggle("", isOn: Binding(get: { model.checked.contains(face.id) }, set: { if $0 { model.checked.insert(face.id) } else { model.checked.remove(face.id) } }))
                        .labelsHidden().disabled(model.busy).accessibilityLabel(face.title)
                }.width(24)
                TableColumn(model.t("글꼴 · 스타일", "Font · style"), value: \.title).width(min: 220, ideal: 380)
                TableColumn(model.t("형식", "Format"), value: \.format).width(60)
                TableColumn(model.t("크기", "Size")) { face in Text(ByteCountFormatter.string(fromByteCount: face.bytes, countStyle: .file)) }.width(80)
            }.clipShape(RoundedRectangle(cornerRadius: 6))
            HStack {
                Text(model.t("\(model.checked.count)개 선택 / \(model.queue.count)개", "\(model.checked.count) selected / \(model.queue.count) fonts")).font(.caption).foregroundStyle(.secondary)
                Spacer()
                Button(model.t("선택한 폰트 설치", "Install checked fonts")) { model.installChecked() }.pocketPrimary().disabled(model.busy || model.checked.isEmpty)
            }
            PreviewPane(face: model.preview) {
                Button(model.t("상세 정보", "Details")) { details = true }.disabled(model.preview == nil)
            }.frame(minHeight: 180, idealHeight: 200, maxHeight: 250)
        }
        .sheet(isPresented: $details) { if let face = model.preview { DetailsView(face: face).environmentObject(model) } }
    }
}

struct DownloadPicker: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.dismiss) var dismiss
    let request: DownloadRequest
    @State private var checked = Set<String>()
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(model.t("다운로드할 파일 선택", "Choose files to download")).font(.title2.bold())
            Text(model.t("필요한 형식과 스타일만 선택하세요. 폰트 라이선스는 제작사 안내를 확인해 주세요.", "Choose the formats and styles you need. Check the creator's font license.")).foregroundStyle(.secondary)
            List(request.candidates) { candidate in
                Toggle(isOn: Binding(get: { checked.contains(candidate.id) }, set: { if $0 { checked.insert(candidate.id) } else { checked.remove(candidate.id) } })) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(candidate.name).lineLimit(2)
                        Text(candidate.url.host ?? "").font(.caption).foregroundStyle(.secondary)
                    }.padding(.vertical, 4)
                }
            }
            HStack {
                Button(model.t("전체 선택", "Select all")) { checked = Set(request.candidates.map(\.id)) }
                Button(model.t("선택 해제", "Clear")) { checked = [] }
                Spacer()
                Button(model.t("취소", "Cancel")) { model.finishChoices([]); dismiss() }.keyboardShortcut(.cancelAction)
                Button(model.t("선택 파일 받기", "Download selected")) {
                    model.finishChoices(request.candidates.filter { checked.contains($0.id) }); dismiss()
                }.pocketPrimary().keyboardShortcut(.defaultAction).disabled(checked.isEmpty)
            }
        }.padding(24).frame(width: 700, height: 520)
    }
}

struct DetailsView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.dismiss) var dismiss
    let face: FontFace
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(face.family).font(.title2.bold())
                    Text(face.style + " · " + face.format + " · " + ByteCountFormatter.string(fromByteCount: face.bytes, countStyle: .file)).foregroundStyle(.secondary)
                }
                Spacer(); Button(model.t("닫기", "Done")) { dismiss() }.keyboardShortcut(.cancelAction)
            }
            PreviewPane(face: face) { EmptyView() }.frame(maxHeight: .infinity)
            LabeledContent("PostScript", value: face.postScript).textSelection(.enabled)
            Text(face.file.path).font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
            Button(model.t("Finder에서 보기", "Show in Finder")) { NSWorkspace.shared.activateFileViewerSelecting([face.file]) }
        }.padding(24).frame(width: 800, height: 580)
    }
}

struct ComparisonView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.dismiss) var dismiss
    let faces: [FontFace]
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack {
                Text(model.t("폰트 비교", "Compare fonts")).font(.title2.bold())
                Spacer(); Button(model.t("닫기", "Done")) { dismiss() }.keyboardShortcut(.cancelAction)
            }
            TextField(model.t("미리보기 문구", "Preview text"), text: $model.previewText).textFieldStyle(.roundedBorder)
            Slider(value: $model.previewSize, in: 12...96) { Text(model.t("글자 크기", "Font size")) }
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    ForEach(faces) { face in
                        VStack(alignment: .leading, spacing: 8) {
                            Text(face.title).font(.headline)
                            Text(model.previewText).font(Font(face.previewFont(size: model.previewSize))).frame(maxWidth: .infinity, alignment: .leading)
                        }.padding(16).background(Color(nsColor: .textBackgroundColor), in: RoundedRectangle(cornerRadius: 6))
                    }
                }
            }
        }.padding(24).frame(width: 820, height: 640)
    }
}

struct ActivityView: View {
    @EnvironmentObject var model: AppModel
    var body: some View {
        List(model.activity) { item in
            HStack(alignment: .top, spacing: 16) {
                Text(item.date, style: .time).monospacedDigit().foregroundStyle(.secondary)
                Text(item.message).textSelection(.enabled)
            }.padding(.vertical, 6)
        }.overlay {
            if model.activity.isEmpty { Text(model.t("아직 진행한 작업이 없습니다.", "No tasks in this session yet.")).foregroundStyle(.secondary) }
        }
    }
}

struct SettingsView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.dismiss) var dismiss
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(model.t("설정", "Settings")).font(.title2.bold())
            Form {
                Section {
                    Picker(model.t("언어", "Language"), selection: $model.language) {
                        Text("한국어").tag("ko"); Text("English").tag("en")
                    }
                }
                Section(model.t("폰트 보관", "Font storage")) {
                    Text(model.t("폰트는 현재 사용자 계정에 설치됩니다. 사용 중지·삭제는 Font Pocket이 설치한 폰트만 지원합니다.", "Fonts are installed for the current user. Only fonts installed by Font Pocket can be disabled or deleted here.")).font(.callout).foregroundStyle(.secondary)
                }
                Section {
                    Link(model.t("업데이트·다운로드 확인", "Check releases and downloads"), destination: URL(string: "https://github.com/tae0604/font-pocket/releases")!)
                    Text("Font Pocket for macOS · 0.1.0 Preview").font(.caption).foregroundStyle(.secondary)
                }
            }.formStyle(.grouped)
            HStack {
                Spacer()
                Button(model.t("닫기", "Done")) { dismiss() }.pocketGlassButton().keyboardShortcut(.cancelAction)
            }
        }.padding(22)
    }
}
