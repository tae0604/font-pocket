import SwiftUI
import AppKit
import UniformTypeIdentifiers
import FontPocketCore

private let brand = Color(red: 43/255, green: 96/255, blue: 163/255)

struct MainView: View {
    @EnvironmentObject var model: AppModel
    @State private var showSettings = false
    @State private var showComparison = false
    var body: some View {
        NavigationSplitView {
            sidebar
                .navigationSplitViewColumnWidth(min: 200, ideal: 215, max: 270)
        } detail: {
            VStack(alignment: .leading, spacing: 18) {
                heading
                if model.page == .install { InstallView() }
                else if model.page == .activity { ActivityView() }
                else { manager }
                footer
            }.padding(24).frame(maxWidth: .infinity, maxHeight: .infinity)
                .navigationTitle(title(model.page))
                .toolbar { nativeToolbar }
        }
        .navigationSplitViewStyle(.balanced)
        .tint(brand)
        .sheet(isPresented: $showSettings) { SettingsView().environmentObject(model).frame(width: 520, height: 520) }
        .sheet(isPresented: $model.details) { if let face = model.preview { DetailsView(face: face).environmentObject(model) } }
        .sheet(isPresented: $showComparison) { ComparisonView(faces: Array(model.selectedFonts.prefix(12))).environmentObject(model) }
        .sheet(isPresented:$model.quickSearch){QuickSearchView().environmentObject(model)}
        .sheet(isPresented:$model.tools){LibraryToolsView().environmentObject(model)}
        .sheet(item: $model.downloadRequest) { request in DownloadPicker(request: request).environmentObject(model).interactiveDismissDisabled() }
        .alert(model.t("작업을 완료하지 못했습니다", "Could not complete the task"), isPresented: Binding(get: { model.error != nil }, set: { if !$0 { model.error = nil } })) {
            Button(model.t("확인", "OK")) { model.error = nil }
        } message: { Text(model.error ?? "") }
        .onDrop(of: [UTType.fileURL.identifier], isTargeted: nil) { providers in
            for provider in providers {
                provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier, options: nil) { item, _ in
                    let url = (item as? URL) ?? (item as? Data).flatMap { URL(dataRepresentation: $0, relativeTo: nil) }
                    if let url { Task { @MainActor in model.addFiles([url]) } }
                }
            }
            return !providers.isEmpty
        }
    }
    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 9) {
                if let url = Bundle.main.url(forResource: "FontPocket-logo", withExtension: "png") ?? Bundle.module.url(forResource: "FontPocket-logo", withExtension: "png"), let image = NSImage(contentsOf: url) {
                    Image(nsImage: image).resizable().scaledToFit().frame(width: 28, height: 28)
                }
                Text("Font Pocket").font(.system(size: 20, weight: .semibold))
            }.padding(.horizontal, 18).padding(.top, 18).padding(.bottom, 14)
            List(selection: Binding<Page?>(get: { model.page }, set: { if let page = $0 { model.page = page } })) {
                ForEach(Page.allCases) { page in
                    Label(title(page), systemImage: icon(page)).tag(page).padding(.vertical, 3)
                }
            }
            .listStyle(.sidebar)
            Spacer()
            Button { showSettings = true } label: {
                Label(model.t("설정", "Settings"), systemImage: "gearshape").frame(maxWidth: .infinity, alignment: .leading)
            }.buttonStyle(.plain).padding(.horizontal, 20).padding(.vertical, 12)
            Text("macOS 0.3.0").font(.caption).foregroundStyle(.secondary).padding(.horizontal, 20).padding(.bottom, 18)
        }
    }
    private func title(_ page: Page) -> String {
        switch page { case .manager: return model.t("폰트 관리", "Font manager")
        case .install: return model.t("폰트 설치", "Install fonts")
        case .backup: return model.t("백업", "Backup")
        case .activity: return model.t("작업 기록", "Activity") }
    }
    private func icon(_ page: Page) -> String {
        switch page { case .manager: return "textformat"; case .install: return "arrow.down.to.line"; case .backup: return "archivebox"; case .activity: return "clock.arrow.circlepath" }
    }
    private var heading: some View {
        HStack(alignment: .top) {
            Text(subtitle).font(.callout).foregroundStyle(.secondary)
            Spacer()
        }
    }
    private var subtitle: String {
        switch model.page {
        case .manager: return model.t("설치된 폰트를 찾고, 미리 보고, 정리하세요.", "Find, preview and organize your installed fonts.")
        case .install: return model.t("링크·ZIP·폰트 파일을 한곳에 모아 설치하세요.", "Gather links, ZIP archives and font files in one queue.")
        case .backup: return model.t("필요한 폰트를 선택해 ZIP으로 보관하세요.", "Select fonts and save them in a ZIP archive.")
        case .activity: return model.t("최근 진행한 작업과 오류를 확인하세요.", "Review recent tasks and errors.")
        }
    }
    private var manager: some View {
        VStack(spacing: 12) {
            HStack {
                Text(model.t("폰트 \(model.visibleFonts.count)개 · 선택 \(model.selectedFonts.count)개", "\(model.visibleFonts.count) \(model.visibleFonts.count == 1 ? "font" : "fonts") · \(model.selectedFonts.count) selected")).font(.caption).foregroundStyle(.secondary)
                Spacer()
                if model.page == .backup {
                    Button{model.openFontBackup()}label:{Label(model.t("백업한 폰트 설치","Install backed-up fonts"),systemImage:"archivebox")}
                        .pocketGlassButton().disabled(model.busy).accessibilityIdentifier("install-font-backup")
                    Button(model.t("선택한 폰트 백업", "Back up selected fonts")) { model.backupSelected() }
                        .pocketPrimary().disabled(model.selectedFonts.isEmpty || model.busy).accessibilityIdentifier("backup-selected-fonts")
                }
            }
            Table(model.visibleFonts, selection: $model.selection) {
                TableColumn(model.t("글꼴 · 스타일", "Font · style")) { face in
                    VStack(alignment: .leading, spacing: 3) {
                        Text(face.family).fontWeight(.medium)
                        Text(face.style).font(.caption).foregroundStyle(.secondary)
                    }.padding(.vertical, 4)
                }.width(min: 240, ideal: 340)
                TableColumn(model.t("형식", "Format"), value: \.format).width(55)
                TableColumn(model.t("상태", "Status")) { face in Text(state(face)).foregroundStyle(.secondary) }.width(min: 90, ideal: 140)
                TableColumn(model.t("크기", "Size")) { face in Text(ByteCountFormatter.string(fromByteCount: face.bytes, countStyle: .file)).foregroundStyle(.secondary) }.width(80)
            }
            .contextMenu(forSelectionType: String.self) { ids in
                FontContextMenu(ids: ids, compare: { showComparison=true })
            } primaryAction: { ids in
                model.selection=ids
                if model.preview != nil { model.details=true }
            }
            if model.page != .backup {
                PreviewPane(face: model.preview) {
                    Button(model.t("상세 정보", "Details")) { model.details = true }.disabled(model.preview == nil)
                    Button(model.t("비교", "Compare")) { showComparison = true }.disabled(!FontOrdering.comparisonAllowed(model.selectedFonts))
                }.frame(minHeight: 190, idealHeight: 230, maxHeight: 270)
            }
        }.searchable(text: $model.search, placement: .toolbar, prompt: model.t("폰트 검색", "Search fonts"))
    }
    private func state(_ face: FontFace) -> String {
        switch face.state { case .enabled: return model.t("사용 중", "In use"); case .disabled: return model.t("사용 중지", "Disabled")
        case .external: return face.canAdopt ? model.t("설치됨 · 사용자 폰트","Installed · user font"):model.t("설치됨 · 읽기 전용", "Installed · read-only"); case .available: return model.t("설치 대기", "Queued") }
    }
    private var actions: some View { Menu(model.t("관리", "Manage")) { contextActions }.disabled(model.selection.isEmpty || model.busy) }
    @ToolbarContentBuilder private var nativeToolbar: some ToolbarContent {
        ToolbarItemGroup(placement: .primaryAction) {
            if model.page == .manager || model.page == .backup {
                Button { model.refresh() } label: { Label(model.t("새로고침", "Refresh"), systemImage: "arrow.clockwise") }.disabled(model.busy)
                Menu {
                    Picker(model.t("형식","Format"),selection:$model.formatFilter){Text(model.t("전체","All")).tag("");ForEach(["TTF","OTF","TTC","OTC"],id:\.self){Text($0).tag($0)}}
                    Picker(model.t("상태","State"),selection:$model.stateFilter){Text(model.t("전체","All")).tag("");Text(model.t("사용 중","Enabled")).tag("enabled");Text(model.t("사용 중지","Disabled")).tag("disabled")}
                    Picker(model.t("문자 범위","Script"),selection:$model.languageFilter){Text(model.t("전체","All")).tag("");ForEach(FontAnalysis.ranges,id:\.0){Text($0.0).tag($0.0)}}
                    Toggle(model.t("가변만","Variable only"),isOn:$model.variableOnly)
                    Toggle(model.t("고정 폰트만","Static only"),isOn:$model.fixedOnly)
                    Picker(model.t("굵기","Weight"),selection:$model.weightFilter){Text(model.t("전체","All")).tag("");Text("Light").tag("light");Text("Regular / Medium").tag("regular");Text("Bold").tag("bold")}
                    Picker(model.t("모음","Collection"),selection:Binding(get:{model.activeCollection?.uuidString ?? ""},set:{model.activeCollection=UUID(uuidString:$0)})){Text(model.t("전체","All")).tag("");ForEach(model.personal.collections){Text($0.name).tag($0.id.uuidString)}}
                    Toggle(model.t("패밀리 순서","Group families"),isOn:$model.groupFamilies)
                    Toggle(model.t("시스템 폰트 표시", "Show system fonts"), isOn: $model.showSystem)
                    Toggle(model.t("즐겨찾기만", "Favorites only"), isOn: $model.onlyFavorites)
                    Toggle(model.t("중복만", "Duplicates only"), isOn: $model.duplicatesOnly)
                    Divider()
                    Button(model.t("필터 초기화", "Reset filters")) { model.resetFilters() }
                } label: { Label(model.t("필터", "Filters"), systemImage: "line.3.horizontal.decrease") }
                .onChange(of: model.showSystem) { _ in model.refresh() }
                Menu { contextActions } label: { Label(model.t("관리", "Manage"), systemImage: "ellipsis") }.disabled(model.busy)
            }
            Button { model.page = .install; model.openFiles() } label: { Label(model.t("폰트 추가", "Add fonts"), systemImage: "plus") }.disabled(model.busy)
            Button{model.tools=true}label:{Label(model.t("도구","Tools"),systemImage:"slider.horizontal.3")}
        }
    }
    private var contextActions: some View {
        FontContextMenu(ids:Set(model.selectedFonts.map(\.id)), compare:{showComparison=true})
    }
    private var footer: some View {
        HStack {
            if model.busy {
                TaskControls().environmentObject(model)
            } else { Text(model.status).font(.caption).foregroundStyle(.secondary).lineLimit(2) }
            Spacer()
        }.frame(minHeight: 26)
    }
}
