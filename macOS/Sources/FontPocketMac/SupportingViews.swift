import SwiftUI
import AppKit
import FontPocketCore

struct TaskControls:View{
    @EnvironmentObject var model:AppModel
    var body:some View{HStack(spacing:12){ProgressView().controlSize(.small);Text(model.status).font(.caption).lineLimit(2);Spacer();Button(model.t("중지","Stop")){model.cancel()}}.padding(.horizontal,16).padding(.vertical,10).pocketFloatingSurface()}
}

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
            ScrollViewReader { proxy in
                ScrollView(.vertical) {
                    if let face {
                        Text(model.previewText).font(Font(face.previewFont(size: model.previewSize)))
                            .textSelection(.enabled).multilineTextAlignment(.leading)
                            .frame(maxWidth: .infinity, alignment: .topLeading)
                            .fixedSize(horizontal: false, vertical: true).padding(.vertical, 4).id("preview-top")
                    }
                }
                .onAppear { proxy.scrollTo("preview-top", anchor: .top) }
                .onChange(of: face?.id) { _ in proxy.scrollTo("preview-top", anchor: .top) }
            }.frame(maxWidth: .infinity, maxHeight: .infinity)
        }.padding(16).background(Color(nsColor: .textBackgroundColor), in: RoundedRectangle(cornerRadius: 12))
    }
}

struct InstallView: View {
    @EnvironmentObject var model: AppModel
    @State private var details = false
    @State private var compare = false
    private var compareFonts:[FontFace]{let checked=model.queue.filter{model.checked.contains($0.id)};return checked.isEmpty ? model.selectedQueue:checked}
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
                Picker(model.t("형식","Format"),selection:$model.importFormat){Text("TTF").tag("TTF");Text("OTF").tag("OTF");Text("TTF + OTF").tag("ALL")}.frame(width:170)
                Toggle("TTC",isOn:$model.includeTTC)
                Button(model.t("파일·ZIP·폴더 추가", "Add files, ZIP or folder")) { model.openFiles() }.disabled(model.busy)
                Spacer()
            }
            HStack{
                Picker(model.t("종류","Kind"),selection:$model.importKind){Text(model.t("전체","All")).tag("all");Text(model.t("고정","Static")).tag("static");Text(model.t("가변","Variable")).tag("variable")}.frame(width:190)
                Button(model.t("전체 선택", "Select all")) { model.checked = Set(model.queue.map(\.id)) }.disabled(model.busy)
                Button(model.t("선택 해제", "Clear checks")) { model.checked = [] }.disabled(model.busy)
                Spacer()
                Button(model.t("선택 제거","Remove selected")){let ids=model.queueSelection;model.queue.removeAll{ids.contains($0.id)};model.checked.subtract(ids);model.queueSelection=[]}.disabled(model.busy || model.queueSelection.isEmpty)
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
                Text(model.t("\(model.checked.count)개 선택 / \(model.queue.count)개", "\(model.checked.count) selected / \(model.queue.count) \(model.queue.count == 1 ? "font" : "fonts")")).font(.caption).foregroundStyle(.secondary)
                Spacer()
                Button(model.t("비교","Compare")){compare=true}.disabled(compareFonts.count<2 || compareFonts.count>12 || compareFonts.reduce(Int64(0)){$0+$1.bytes}>256*1024*1024)
                Button(model.t("버전으로 보관","Archive versions")){model.archiveSelection()}.disabled(model.busy || model.checked.isEmpty)
                Button(model.t("선택한 폰트 설치", "Install checked fonts")) { model.installChecked() }.pocketPrimary().disabled(model.busy || model.checked.isEmpty)
            }
            PreviewPane(face: model.preview) {
                Button(model.t("상세 정보", "Details")) { details = true }.disabled(model.preview == nil)
            }.frame(minHeight: 180, idealHeight: 200, maxHeight: 250)
        }
        .sheet(isPresented: $details) { if let face = model.preview { DetailsView(face: face).environmentObject(model) } }
        .sheet(isPresented:$compare){ComparisonView(faces:compareFonts).environmentObject(model)}
        .onChange(of:model.importFormat){_ in model.applyImportPreferences()}
        .onChange(of:model.includeTTC){_ in model.applyImportPreferences()}
        .onChange(of:model.importKind){_ in model.applyImportPreferences()}
    }
}

struct DownloadPicker: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.dismiss) var dismiss
    let request: DownloadRequest
    @State private var checked = Set<String>()
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(request.navigation ? model.t("관련 다운로드 페이지 선택","Choose a related download page"):model.t("다운로드할 파일 선택", "Choose files to download")).font(.title2.bold())
            Text(request.navigation ? model.t("자동으로 파일을 찾지 못했습니다. 폰트가 있는 페이지를 골라 계속하세요.","No file was found automatically. Choose the relevant page to continue."):model.t("필요한 형식과 스타일만 선택하세요. 폰트 라이선스는 제작사 안내를 확인해 주세요.", "Choose the formats and styles you need. Check the creator's font license.")).foregroundStyle(.secondary)
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
                Button(request.navigation ? model.t("계속","Continue"):model.t("선택 파일 받기", "Download selected")) {
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
    var body: some View { AdvancedDetailsView(face:face).environmentObject(model) }

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
                Toggle(model.t("하루 한 번 업데이트 확인","Check for updates once a day"),isOn:$model.automaticUpdates)
                Section {
                    Picker(model.t("언어", "Language"), selection: $model.language) {
                        Text("한국어").tag("ko"); Text("English").tag("en")
                    }
                }
                Section(model.t("폰트 보관", "Font storage")) {
                    Text(model.t("폰트는 현재 사용자 계정에 설치됩니다. 기존 사용자 폰트는 관리 메뉴에서 가져올 수 있습니다. 시스템 폰트는 보호됩니다.", "Fonts are installed for the current user. Existing user fonts can be adopted in Manage. System fonts remain protected.")).font(.callout).foregroundStyle(.secondary)
                }
                Section {
                    Link(model.t("업데이트·다운로드 확인", "Check releases and downloads"), destination: URL(string: "https://github.com/tae0604/font-pocket/releases")!)
                    Text("Font Pocket for macOS · 0.2.0").font(.caption).foregroundStyle(.secondary)
                }
            }.formStyle(.grouped)
            HStack {
                Spacer()
                Button(model.t("닫기", "Done")) { dismiss() }.pocketGlassButton().keyboardShortcut(.cancelAction)
            }
        }.padding(22)
    }
}
