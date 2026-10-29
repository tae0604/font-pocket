import SwiftUI
import AppKit
import FontPocketCore

struct AdvancedDetailsView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.dismiss) var dismiss
    let face: FontFace
    @State private var report: FontInspection?
    @State private var index: CharacterIndex?
    @State private var matching: [UInt32] = []
    @State private var problem = ""
    @State private var query = ""
    @State private var range = ""
    @State private var page = 0
    @State private var axes: [Int: Double] = [:]
    @State private var copied = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text(face.title).font(.title2.bold()).lineLimit(2)
                Spacer()
                Button(model.t("인쇄·PDF", "Print / PDF")) { FontPrinting.printFonts([face], text: model.previewText) }
                Button(model.t("닫기", "Done")) { dismiss() }.keyboardShortcut(.cancelAction)
            }
            SectionSwitcher(options: [
                .init(id: "preview", label: model.t("미리보기", "Preview")),
                .init(id: "characters", label: model.t("문자표", "Characters")),
                .init(id: "coverage", label: model.t("지원 범위", "Coverage")),
                .init(id: "information", label: model.t("정보", "Information")),
                .init(id: "inspection", label: model.t("검사", "Inspection"))
            ], selection: $model.detailsTab)
            Group {
                switch model.detailsTab {
                case "characters": characterMap
                case "coverage": coverage
                case "information": information
                case "inspection": inspection
                default: preview
                }
            }.frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            HStack {
                Menu(model.t("웹폰트 내보내기", "Export web font")) {
                    Button(model.t("원본·CSS·HTML 저장", "Original / CSS / HTML")) { model.exportWeb(face, woff2: false) }
                    Button(model.t("WOFF2 저장", "WOFF2")) { model.exportWeb(face, woff2: true) }
                    Button(model.t("WOFF2·CSS·HTML 저장", "WOFF2 / CSS / HTML")) { model.exportWeb(face, woff2: true, package: true) }
                }.disabled(model.busy)
                Spacer()
                if report == nil && problem.isEmpty { ProgressView().controlSize(.small) }
                Text(problem).font(.caption).foregroundStyle(.red).lineLimit(2)
            }
            if model.busy { TaskControls().environmentObject(model) }
        }
        .padding(22).frame(width: 900, height: 620)
        .task(id: face) {
            let task = Task.detached { () throws -> (FontInspection, CharacterIndex) in
                let result = try FontAnalysis.inspect(face)
                return (result, CharacterIndex(result.characters))
            }
            do {
                let result = try await withTaskCancellationHandler(operation: { try await task.value }, onCancel: { task.cancel() })
                try Task.checkCancellation()
                report = result.0
                index = result.1
            } catch is CancellationError { }
            catch { problem = error.localizedDescription }
        }
        .task(id: "\(report?.metadata["SHA256"] ?? "")|\(query)|\(range)") {
            guard let index else { return }
            let query = query, range = range
            do {
                if !query.isEmpty { try await Task.sleep(nanoseconds: 100_000_000) }
                let task = Task.detached { try index.matching(query, script: range) }
                let values = try await withTaskCancellationHandler(operation: { try await task.value }, onCancel: { task.cancel() })
                try Task.checkCancellation()
                matching = values
                page = 0
            } catch is CancellationError { }
            catch { problem = error.localizedDescription }
        }
    }

    private var preview: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                PreviewPane(face: face) { EmptyView() }.frame(height: 240)
                if let report, !report.axes.isEmpty {
                    Text(model.t("가변 축 조절", "Variable axes")).font(.headline)
                    ForEach(report.axes) { axis in
                        HStack {
                            Text(axis.name).frame(width: 100, alignment: .leading)
                            Slider(value: Binding(get: { axes[axis.id] ?? axis.defaultValue }, set: { axes[axis.id] = $0 }),
                                   in: axis.minimum...max(axis.minimum, axis.maximum))
                            Text(String(format: "%.1f", axes[axis.id] ?? axis.defaultValue)).monospacedDigit().frame(width: 65)
                        }.disabled(axis.minimum == axis.maximum)
                    }
                    Text(model.previewText).font(Font(face.variedFont(size: model.previewSize, values: axes)))
                        .frame(maxWidth: .infinity, alignment: .leading).padding(.vertical)
                    Button(model.t("기본값 복원", "Reset axes")) { axes = [:] }
                }
            }
        }
    }

    private var characterMap: some View {
        VStack {
            HStack {
                TextField(model.t("문자 또는 U+AC00 검색", "Character or U+AC00"), text: $query).textFieldStyle(.roundedBorder)
                Picker(model.t("범위", "Range"), selection: $range) {
                    Text(model.t("전체", "All")).tag("")
                    ForEach(FontAnalysis.ranges, id: \.0) { Text($0.0).tag($0.0) }
                }.frame(width: 210)
            }
            ScrollView {
                let font = Font(face.previewFont(size: 28))
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 64))], spacing: 10) {
                    ForEach(Array(matching.dropFirst(page * 256).prefix(256)), id: \.self) { value in
                        Button {
                            let text = Unicode.Scalar(value).map(String.init) ?? ""
                            NSPasteboard.general.clearContents()
                            NSPasteboard.general.setString(text, forType: .string)
                            copied = String(format: "U+%04X", value)
                        } label: {
                            VStack {
                                Text(Unicode.Scalar(value).map(String.init) ?? "").font(font)
                                Text(String(format: "%04X", value)).font(.caption2).foregroundStyle(.secondary)
                            }.frame(width: 60, height: 60)
                        }.buttonStyle(.bordered)
                    }
                }
            }
            HStack {
                Button("←") { page = max(0, page - 1) }.disabled(page == 0)
                Text("\(page + 1) / \(max(1, (matching.count + 255) / 256)) · \(matching.count)")
                Button("→") { page += 1 }.disabled((page + 1) * 256 >= matching.count)
                Spacer()
                Text(copied).font(.caption)
            }
            Button(model.t("문자표 인쇄·PDF", "Print character map / PDF")) {
                FontPrinting.printFonts([face], text: model.previewText, characters: true)
            }
        }
    }

    private var coverage: some View {
        VStack(alignment: .leading) {
            List {
                ForEach(FontAnalysis.ranges, id: \.0) { item in
                    HStack {
                        Text(item.0)
                        Spacer()
                        Text("\(index?.coverage[item.0] ?? 0) / \(Int(item.1.upperBound - item.1.lowerBound) + 1)").monospacedDigit()
                    }
                }
            }
            Text(model.t("문자 포함 여부는 언어의 모든 조합·문맥 표현 지원을 보장하지 않습니다.",
                         "Character coverage does not guarantee every language shaping or contextual behavior."))
                .font(.caption).foregroundStyle(.secondary)
        }
    }

    private var information: some View {
        List {
            if let report {
                ForEach(report.metadata.keys.sorted(), id: \.self) { key in
                    VStack(alignment: .leading, spacing: 5) {
                        Text(key).font(.headline)
                        Text(report.metadata[key] ?? "").textSelection(.enabled)
                    }
                }
            }
        }
    }

    private var inspection: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                if let report {
                    Text(model.t("글리프 \(report.glyphs)개 · 문자 \(report.characters.count)개",
                                 "\(report.glyphs) glyphs · \(report.characters.count) characters"))
                    Text("Tables: " + report.tables.sorted().joined(separator: ", ")).textSelection(.enabled)
                    Text(report.issues.isEmpty ? model.t("테이블 범위·체크섬 검사 통과", "Table bounds and checksums passed") : report.issues.joined(separator: "\n"))
                        .foregroundStyle(report.issues.isEmpty ? Color.primary : Color.red)
                }
                Text(model.t("모든 렌더링 문제나 악성 코드를 검사하는 기능은 아닙니다.",
                             "This does not detect every rendering issue or malicious font.")).font(.caption).foregroundStyle(.secondary)
            }.frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

struct QuickSearchView:View{
    @EnvironmentObject var model:AppModel
    @Environment(\.dismiss) var dismiss
    @State private var query=""
    @FocusState private var focused:Bool
    var body:some View{VStack{
        HStack{Image(systemName:"magnifyingglass");TextField(model.t("이름·메모·태그 검색","Search name, notes or tags"),text:$query).textFieldStyle(.plain).font(.title3).focused($focused);Button(model.t("닫기","Done")){dismiss()}.keyboardShortcut(.cancelAction)}.padding()
        List(model.fonts.filter{face in let note=model.personal.annotations[face.id] ?? FontAnnotation();return query.isEmpty || (face.title+" "+face.version+" "+face.format+" "+note.note+" "+note.tags.joined(separator:" ")).localizedCaseInsensitiveContains(query)}.prefix(100)){face in Button{model.search="";model.activeCollection=nil;model.onlyFavorites=false;model.formatFilter="";model.stateFilter="";model.languageFilter="";model.weightFilter="";model.variableOnly=false;model.fixedOnly=false;model.selection=[face.id];model.page = .manager;dismiss()}label:{HStack{Text(face.title);Spacer();Text(face.format).foregroundStyle(.secondary)}}.buttonStyle(.plain).padding(.vertical,5)}
    }.frame(width:660,height:480).onAppear{focused=true}}
}
