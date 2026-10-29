import SwiftUI
import AppKit
import UniformTypeIdentifiers
import CoreText
import FontPocketCore

enum Page: String, CaseIterable, Identifiable { case manager, install, backup, activity; var id: String { rawValue } }
struct ActivityEntry: Identifiable, Codable { var id = UUID(); let date: Date; let message: String }
struct DownloadRequest: Identifiable { let id = UUID(); let candidates: [DownloadCandidate];var navigation=false }

@MainActor final class AppModel: ObservableObject {
    @Published var page: Page = .manager
    @Published var fonts: [FontFace] = [] {didSet{invalidateFontFilter()}}
    @Published var queue: [FontFace] = []
    @Published var selection = Set<String>() {didSet{cachedSelected=nil}}
    @Published var checked = Set<String>()
    @Published var queueSelection = Set<String>()
    @Published var search = "" {didSet{invalidateFontFilter()}}
    @Published var showSystem = false
    @Published var onlyFavorites = false {didSet{invalidateFontFilter()}}
    @Published var personal = PersonalDocument() {didSet{invalidateFontFilter()}}
    @Published var activeCollection: UUID? {didSet{invalidateFontFilter()}}
    @Published var formatFilter = "" {didSet{invalidateFontFilter()}}
    @Published var stateFilter = "" {didSet{invalidateFontFilter()}}
    @Published var languageFilter = "" {didSet{invalidateFontFilter()}}
    @Published var variableOnly = false {didSet{if variableOnly{fixedOnly=false};invalidateFontFilter()}}
    @Published var fixedOnly = false {didSet{if fixedOnly{variableOnly=false};invalidateFontFilter()}}
    @Published var weightFilter = "" {didSet{invalidateFontFilter()}}
    @Published var groupFamilies = false {didSet{invalidateFontFilter()}}
    @Published var importFormat = "TTF"
    @Published var importKind = "all"
    @Published var includeTTC = false
    @Published var automaticUpdates = UserDefaults.standard.object(forKey:"automaticUpdates") as? Bool ?? true { didSet{UserDefaults.standard.set(automaticUpdates,forKey:"automaticUpdates")} }
    @Published var quickSearch = false
    @Published var tools = false
    @Published var hasUnsavedLibraryEdits = false
    @Published var toolsTab = "notes"
    @Published var details = false
    @Published var detailsTab = "preview"
    let personalStore: PersonalLibrary
    let vault: VersionVault
    @Published var links = ""
    @Published var previewText = "다람쥐 헌 쳇바퀴에 타고파 0123456789\nThe quick brown fox jumps over the lazy dog."
    @Published var previewSize = 32.0
    @Published var busy = false
    @Published var workProgress: WorkProgress?
    @Published var stopping = false
    @Published var taskName = ""
    @Published var status = ""
    @Published var error: String?
    @Published var activity: [ActivityEntry] = []
    @Published var downloadRequest: DownloadRequest?
    // Only used when launching the disposable CI UI verification mode.
    @Published var testReduceTransparency = false
    @Published var testReduceMotion = false
    @Published var testAppearance: ColorScheme?
    @Published var language: String { didSet { UserDefaults.standard.set(language, forKey: "language") } }
    let dataRoot: URL
    let workspace: URL
    let repository: FontRepository
    let importer: FontImporter
    private var cachedVisible:[FontFace]?
    private var cachedSelected:[FontFace]?
    private func invalidateFontFilter(){cachedVisible=nil;cachedSelected=nil}
    private var job: Task<Void, Never>?
    private var workID: UUID?
    private var chooser: CheckedContinuation<[DownloadCandidate], Never>?
    private var pendingFiles: [URL] = []

    init() throws {
        language = UserDefaults.standard.string(forKey: "language") ?? (Locale.preferredLanguages.first?.hasPrefix("ko") == true ? "ko" : "en")
        workspace = FileManager.default.temporaryDirectory.appendingPathComponent("FontPocket-Mac-\(UUID().uuidString)")
        dataRoot = ProcessInfo.processInfo.environment["FONTPOCKET_MAC_TEST_ROOT"].map{URL(fileURLWithPath:$0).appendingPathComponent("Personal")} ?? LibraryPaths.live.root
        personalStore = try PersonalLibrary(root:dataRoot)
        vault = try VersionVault(root:dataRoot.appendingPathComponent("Versions"))
        if let root = ProcessInfo.processInfo.environment["FONTPOCKET_MAC_TEST_ROOT"] {
            let folder = URL(fileURLWithPath: root)
            repository = try FontRepository(paths: LibraryPaths(root: folder.appendingPathComponent("Catalog"), fonts: folder.appendingPathComponent("Fonts")))
        } else { repository = try FontRepository() }
        importer = try FontImporter(workspace: workspace, converter: { url,folder in try await WebFontCodec.prepare(url,folder:folder) })
        if let data = try? Data(contentsOf:dataRoot.appendingPathComponent("activity.json")),let saved = try? JSONDecoder().decode([ActivityEntry].self,from:data){activity=saved}
        if language == "en" { previewText = "The quick brown fox jumps over the lazy dog.\nA typeface for every idea." }
    }
    func t(_ ko: String, _ en: String) -> String { language == "ko" ? ko : en }
    var visibleFonts: [FontFace] {
        if let cachedVisible{return cachedVisible}
        let filtered = fonts.filter { face in
            let annotation=personal.annotations[face.id] ?? FontAnnotation()
            let terms=face.title+" "+face.version+" "+face.format+" "+face.file.lastPathComponent+" "+annotation.note+" "+annotation.tags.joined(separator:" ")
            if !search.isEmpty && !terms.localizedCaseInsensitiveContains(search){return false}
            if onlyFavorites && !annotation.favorite{return false}
            if !formatFilter.isEmpty && face.format != formatFilter{return false}
            if !stateFilter.isEmpty && !(stateFilter == "enabled" ? [.enabled,.external].contains(face.state) : face.state.rawValue == stateFilter){return false}
            if variableOnly && !face.isVariable{return false}
            if fixedOnly && face.isVariable{return false}
            if weightFilter == "light" && face.weight >= -0.2{return false}
            if weightFilter == "regular" && (face.weight < -0.2 || face.weight > 0.25){return false}
            if weightFilter == "bold" && face.weight <= 0.25{return false}
            if let id=activeCollection,let collection=personal.collections.first(where:{$0.id==id}),!collection.contains(face,annotation:annotation){return false}
            if !languageFilter.isEmpty && !face.scripts.contains(languageFilter){return false}
            return true
        }
        let result = groupFamilies ? filtered.sorted{$0.family==$1.family ? $0.weight < $1.weight : $0.family.localizedStandardCompare($1.family) == .orderedAscending} : filtered
        cachedVisible=result
        return result
    }
    var selectedFonts: [FontFace] {if let cachedSelected{return cachedSelected};let result=visibleFonts.filter{selection.contains($0.id)};cachedSelected=result;return result}
    var selectedQueue: [FontFace] { queue.filter { queueSelection.contains($0.id) } }
    var preview: FontFace? { page == .install ? selectedQueue.first ?? queue.first : selectedFonts.first }
    func record(_ text: String) {
        activity.insert(ActivityEntry(date: Date(), message: text), at: 0)
        if activity.count > 200 { activity.removeLast(activity.count-200) }
        if let data=try? JSONEncoder().encode(activity){try? data.write(to:dataRoot.appendingPathComponent("activity.json"),options:.atomic)}
        status = text
    }
    func perform(_ title: String, action: @escaping @MainActor () async throws -> Void) {
        guard !busy else { return }
        workID=UUID();workProgress=nil;stopping=false
        busy = true; taskName = title; status = title
        job = Task {
            defer {
                workID=nil;workProgress=nil;stopping=false
                busy = false; taskName = ""; job = nil
                if !pendingFiles.isEmpty { let files = pendingFiles; pendingFiles = []; addFiles(files) }
            }
            do { try await action() }
            catch is CancellationError { record(t("작업을 중지했습니다.", "Task stopped.")) }
            catch is LibraryEditConflict { self.error=t("다른 작업에서 같은 항목이 변경됐습니다. 현재 편집 내용은 유지했습니다. 닫을 때 변경을 버리고 최신 정보를 다시 열어 주세요.","The same field changed in another operation. Your edits are still here. Discard them and reopen the latest library to resolve the conflict.");record(self.error!) }
            catch { self.error = error.localizedDescription; record(error.localizedDescription) }
        }
    }
    func cancel() {
        stopping=true
        pendingFiles = []
        finishChoices([])
        job?.cancel()
        status = t("현재 파일 처리를 정리하는 중…", "Finishing the current file operation…")
    }
    func cancelAndWait() async { let running = job; cancel(); await running?.value }
    func cleanup() { try? FileManager.default.removeItem(at: workspace) }
    func refresh() { perform(t("폰트 목록 읽는 중…", "Reading fonts…")) { try await self.reload() } }
    private func reload() async throws {
        personal = try await personalStore.read()
        if let legacy=UserDefaults.standard.stringArray(forKey:"favorites"),!legacy.isEmpty{
            for id in legacy{var entry=personal.annotations[id] ?? FontAnnotation();entry.favorite=true;personal.annotations[id]=entry}
            try await personalStore.save(personal);UserDefaults.standard.removeObject(forKey:"favorites")
        }
        fonts = try await repository.library(includeSystem: showSystem)
        selection.formIntersection(Set(fonts.map(\.id)))
        status = t("폰트 \(fonts.count)개", "\(fonts.count) \(fonts.count == 1 ? "font" : "fonts")")
    }
    func openFiles() {
        guard !busy else { return }
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true; panel.canChooseDirectories = true
        panel.allowedContentTypes = ["ttf","otf","ttc","otc","zip","woff","woff2","sfnt"].compactMap { UTType(filenameExtension: $0) }
        panel.prompt = t("추가", "Add")
        if panel.runModal() == .OK { addFiles(panel.urls) }
    }
    func addFiles(_ urls: [URL]) {
        if busy { pendingFiles += urls; return }
        page = .install
        perform(t("폰트 불러오는 중…", "Importing fonts…")) {
            try await self.importIntoQueue(urls)
        }
    }
    @discardableResult private func importIntoQueue(_ urls: [URL]) async throws -> [FontFace] {
        let imported = try await importer.read(urls)
        let known = Set(queue.map(\.id))
        let added = imported.filter { !known.contains($0.id) }
        queue += added
        // Prefer one file format for a PostScript name, avoiding duplicate TTF/OTF installation.
        var names = Set(queue.filter { checked.contains($0.id) }.map(\.postScript))
        for face in added.sorted(by: { ($0.format == "TTF" ? 0 : 1) < ($1.format == "TTF" ? 0 : 1) }) {
            if preferredImport(face),names.insert(face.postScript).inserted { checked.insert(face.id) }
        }
        if queueSelection.isEmpty, let first = queue.first { queueSelection = [first.id] }
        record(t("폰트 \(added.count)개를 대기열에 추가했습니다.", "Added \(added.count) fonts to the queue."))
        return imported
    }
    func preferredImport(_ face:FontFace)->Bool{
        if ["TTC","OTC"].contains(face.format){if !includeTTC{return false}}
        else if importFormat != "ALL" && face.format != importFormat{return false}
        return importKind == "all" || (importKind == "variable" ? face.isVariable:!face.isVariable)
    }
    func applyImportPreferences(){checked=Set(queue.filter{preferredImport($0)}.map(\.id))}
    func downloadLinks() {
        let lines = links.components(separatedBy: .whitespacesAndNewlines).filter { !$0.isEmpty }
        guard !lines.isEmpty, lines.count <= 30, lines.allSatisfy({ URL(string: $0)?.scheme?.lowercased() == "https" }) else {
            error = t("HTTPS 주소를 한 줄에 하나씩, 최대 30개 입력하세요.", "Enter up to 30 HTTPS addresses, one per line."); return
        }
        perform(t("폰트 다운로드 중…", "Downloading fonts…")) {
            var failures: [String] = []
            var seen = Set<String>()
            for line in lines where seen.insert(line).inserted {
                try Task.checkCancellation()
                guard let url = URL(string: line) else { continue }
                self.status = self.t("다운로드 파일 찾는 중 · ", "Finding downloads · ") + (url.host ?? "")
                do {
                    let choices = try await self.resolveDownloads(url)
                    guard !choices.isEmpty else { throw PocketError("No desktop font files were found.") }
                    let selected = await self.choose(choices)
                    try Task.checkCancellation()
                    for choice in selected {
                        self.status = self.t("다운로드 중 · ", "Downloading · ") + choice.name
                        let file = try await FontDownloads.download(choice, into: self.workspace.appendingPathComponent("Downloads"))
                        try await self.importIntoQueue([file])
                    }
                } catch is CancellationError { throw CancellationError() }
                catch { failures.append("\(url.host ?? line): \(error.localizedDescription)"); self.record(failures.last!) }
            }
            if !failures.isEmpty { throw PocketError(failures.joined(separator: "\n")) }
            self.record(self.t("다운로드 완료 · 설치할 폰트를 선택하세요.", "Downloads complete · choose fonts to install."))
        }
    }
    private func resolveDownloads(_ url:URL,depth:Int=0) async throws->[DownloadCandidate]{
        do{return try await FontDownloads.resolve(url)}
        catch is CancellationError{throw CancellationError()}
        catch{
            let original=error
            guard depth<3 else{throw original}
            let pages=try await FontDownloads.relatedPages(url)
            guard !pages.isEmpty else{throw original}
            let selected=await choose(pages,navigation:true);try Task.checkCancellation()
            guard !selected.isEmpty else{throw CancellationError()}
            var result:[DownloadCandidate]=[]
            for page in selected{result += try await resolveDownloads(page.url,depth:depth+1)}
            return result
        }
    }
    private func choose(_ choices: [DownloadCandidate],navigation:Bool=false) async -> [DownloadCandidate] {
        await withCheckedContinuation { continuation in
            chooser = continuation; downloadRequest = DownloadRequest(candidates: choices,navigation:navigation)
        }
    }
    func finishChoices(_ choices: [DownloadCandidate]) {
        let waiting = chooser; chooser = nil; downloadRequest = nil; waiting?.resume(returning: choices)
    }
    func installChecked() {
        let selected = queue.filter { checked.contains($0.id) }
        let files = Array(Set(selected.map(\.file))).sorted { $0.path < $1.path }
        guard !files.isEmpty else { return }
        perform(t("폰트 설치 중…", "Installing fonts…")) {
            var count = 0, archived = 0, failures: [String] = []
            let installed = try await self.repository.library(includeSystem: true)
            var active = Set(installed.filter { [.enabled, .external].contains($0.state) }.map(\.postScript))
            do {
                for (index, file) in files.enumerated() {
                    try Task.checkCancellation()
                    self.workProgress=WorkProgress(.installing,completed:index,total:files.count)
                    self.status = self.t("설치 중", "Installing") + " \(index + 1) / \(files.count)"
                    do {
                        let versions = try await self.vault.capture(file)
                        guard let hash = versions.first?.hash else { throw PocketError("The font has no readable faces") }
                        let names = Set(versions.map(\.postScript))
                        if active.isDisjoint(with: names) {
                            let added = try await self.repository.install(file)
                            if await self.repository.managedState(hash) == .disabled {
                                try await self.repository.setEnabled(hash, enabled: true)
                            }
                            if added { count += 1 }
                            active.formUnion(names)
                        } else { archived += 1 }
                        // Finish the current committed file before honoring cancellation.
                        // Rekey notes AND manual collection memberships to the installed ID.
                        let baseline = self.personal
                        var edited = baseline
                        for face in self.queue where face.file == file {
                            edited.remap(face.id, to: hash + "|" + face.postScript)
                        }
                        self.personal = try await self.personalStore.saveChanges(edited, baseline: baseline)
                        let done = Set(self.queue.filter { $0.file == file }.map(\.id))
                        self.queue.removeAll { done.contains($0.id) }
                        self.checked.subtract(done)
                        self.queueSelection.subtract(done)
                        self.workProgress=WorkProgress(.installing,completed:index+1,total:files.count)
                    } catch is CancellationError { throw CancellationError() }
                    catch { failures.append(error.localizedDescription) }
                }
            } catch {
                await self.reloadAfterMutation()
                throw error
            }
            try await self.reload()
            self.record(self.t("폰트 파일 \(count)개 설치 · 동일 이름 \(archived)개는 버전 보관함에 저장",
                               "Installed \(count) font files · \(archived) conflicting versions archived"))
            if !failures.isEmpty { throw PocketError(failures.joined(separator: "\n")) }
        }
    }
    func changeEnabled(_ enabled: Bool) {
        let keys = Set(selectedFonts.compactMap(\.managedKey))
        guard !keys.isEmpty else { return }
        perform(t("폰트 사용 설정 변경 중…", "Changing font activation…")) {
            do { for key in keys { try await self.repository.setEnabled(key, enabled: enabled) } }
            catch { await self.reloadAfterMutation(); throw error }
            try await self.reload()
            self.record(self.t("사용 설정을 변경했습니다.", "Font activation updated."))
        }
    }
    func reloadAfterMutation() async {
        await Task { try? await self.reload() }.value
    }
    func deleteSelected() {
        let selected=selectedFonts.filter(\.canManage)
        let keys = Set(selected.compactMap(\.managedKey))
        guard !keys.isEmpty else { return }
        let alert = NSAlert()
        alert.messageText = t("폰트 파일 \(keys.count)개를 삭제할까요?", "Move \(keys.count) font files to Trash?")
        alert.informativeText = t("복구 ZIP을 먼저 저장한 뒤 관리 중인 파일을 휴지통으로 옮깁니다. TTC·OTC는 같은 파일의 모든 서체가 함께 이동합니다.", "A recovery ZIP is saved before managed files move to Trash. All faces in a TTC/OTC file move together.")
        alert.addButton(withTitle: t("휴지통으로 이동", "Move to Trash")); alert.addButton(withTitle: t("취소", "Cancel"))
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        perform(t("폰트 삭제 중…", "Removing fonts…")) {
            let recovery=self.dataRoot.appendingPathComponent("Removed")
            try FileManager.default.createDirectory(at:recovery,withIntermediateDirectories:true)
            try await FontBackup.create(files:selected.map(\.file),destination:recovery.appendingPathComponent(UUID().uuidString+".zip"),progress:self.progressHandler())
            do { for key in keys { try await self.repository.trash(key) } }
            catch { await self.reloadAfterMutation(); throw error }
            try await self.reload()
            self.record(self.t("휴지통으로 옮겼습니다.", "Moved to Trash."))
        }
    }
    func backupSelected() {
        let files = Array(Set(selectedFonts.map(\.file)))
        guard !files.isEmpty, !busy else { return }
        let panel = NSSavePanel(); panel.allowedContentTypes = [.zip]
        panel.nameFieldStringValue = "FontPocket-Backup-" + Date().formatted(.iso8601.year().month().day().dateSeparator(.dash)) + ".zip"
        guard panel.runModal() == .OK, let destination = panel.url else { return }
        perform(t("폰트 백업 중…", "Backing up fonts…")) {
            try await FontBackup.create(files: files, destination: destination, progress:self.progressHandler())
            self.record(self.t("백업을 저장했습니다.", "Backup saved."))
            NSWorkspace.shared.activateFileViewerSelecting([destination])
        }
    }
    func toggleFavorites() {
        var updated=personal
        for face in selectedFonts {var entry=updated.annotations[face.id] ?? FontAnnotation();entry.favorite.toggle();updated.annotations[face.id]=entry}
        savePersonal(updated)
    }
    func progressHandler() -> ProgressHandler {
        let identifier=workID
        return {[weak self] value in
            Task{@MainActor in
                guard let self,self.busy,!self.stopping,self.workID==identifier else{return}
                if let current=self.workProgress{
                    guard value.phaseOrder>=current.phaseOrder else{return}
                    if value.phase==current.phase && value.completed<current.completed{return}
                }
                self.workProgress=value
            }
        }
    }
    var progressStatus: String {
        guard !stopping,let progress=workProgress else{return status}
        switch progress.phase {
        case .preparing:return t("백업 파일 확인 중…","Preparing backup…")
        case .archiving:return t("백업 압축 중…","Compressing backup…")
        case .verifying:return t("백업 검사·저장 중…","Verifying and saving backup…")
        case .installing:return t("폰트 설치 중…","Installing fonts…")
        case .complete:return t("완료","Complete")
        }
    }
    func openFontBackup() {
        guard !busy else{return}
        let panel=NSOpenPanel();panel.allowedContentTypes=[.zip]
        panel.message=t("폰트 백업 ZIP을 선택하세요. 설치 화면에서 확인한 뒤 설치합니다.","Choose a font backup ZIP. Review its fonts on the installation screen before installing.")
        panel.prompt=t("백업 불러오기","Open backup")
        if panel.runModal() == .OK,let url=panel.url{loadFontBackup(url)}
    }
    func loadFontBackup(_ url: URL) {
        perform(t("백업한 폰트 읽는 중…","Reading font backup…")) {
            let inspection=Task.detached{try ArchivePolicy.validate(Data(contentsOf:url,options:.mappedIfSafe))}
            let names=try await withTaskCancellationHandler(operation:{try await inspection.value},onCancel:{inspection.cancel()})
            guard !names.contains("fontpocketenvironment/manifest.json") else{throw PocketError(self.t("환경 백업입니다. 도구의 환경 복원을 사용하세요.","This is an environment backup. Use Restore environment in Library tools."))}
            let imported=try await self.importIntoQueue([url])
            self.checked=Set(imported.map(\.id))
            self.queueSelection=Set(imported.prefix(1).map(\.id))
            self.page = .install
            self.record(self.t("백업 폰트 \(imported.count)개를 불러왔습니다. 확인 후 선택한 폰트 설치를 누르세요.","Loaded \(imported.count) backed-up fonts. Review them, then choose Install checked fonts."))
        }
    }
}
