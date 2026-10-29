import SwiftUI
import AppKit
import UniformTypeIdentifiers
import FontPocketCore

enum Page: String, CaseIterable, Identifiable { case manager, install, backup, activity; var id: String { rawValue } }
struct ActivityEntry: Identifiable, Codable { var id = UUID(); let date: Date; let message: String }
struct DownloadRequest: Identifiable { let id = UUID(); let candidates: [DownloadCandidate] }

@MainActor final class AppModel: ObservableObject {
    @Published var page: Page = .manager
    @Published var fonts: [FontFace] = []
    @Published var queue: [FontFace] = []
    @Published var selection = Set<String>()
    @Published var checked = Set<String>()
    @Published var queueSelection = Set<String>()
    @Published var search = ""
    @Published var showSystem = false
    @Published var onlyFavorites = false
    @Published var links = ""
    @Published var previewText = "다람쥐 헌 쳇바퀴에 타고파 0123456789\nThe quick brown fox jumps over the lazy dog."
    @Published var previewSize = 32.0
    @Published var busy = false
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
    @Published var favorites: Set<String> { didSet { UserDefaults.standard.set(Array(favorites), forKey: "favorites") } }
    let workspace: URL
    let repository: FontRepository
    let importer: FontImporter
    private var job: Task<Void, Never>?
    private var chooser: CheckedContinuation<[DownloadCandidate], Never>?
    private var pendingFiles: [URL] = []

    init() throws {
        language = UserDefaults.standard.string(forKey: "language") ?? (Locale.preferredLanguages.first?.hasPrefix("ko") == true ? "ko" : "en")
        favorites = Set(UserDefaults.standard.stringArray(forKey: "favorites") ?? [])
        workspace = FileManager.default.temporaryDirectory.appendingPathComponent("FontPocket-Mac-\(UUID().uuidString)")
        if let root = ProcessInfo.processInfo.environment["FONTPOCKET_MAC_TEST_ROOT"] {
            let folder = URL(fileURLWithPath: root)
            repository = try FontRepository(paths: LibraryPaths(root: folder.appendingPathComponent("Catalog"), fonts: folder.appendingPathComponent("Fonts")))
        } else { repository = try FontRepository() }
        importer = try FontImporter(workspace: workspace)
        if language == "en" { previewText = "The quick brown fox jumps over the lazy dog.\nA typeface for every idea." }
    }
    func t(_ ko: String, _ en: String) -> String { language == "ko" ? ko : en }
    var visibleFonts: [FontFace] {
        fonts.filter { (search.isEmpty || $0.title.localizedCaseInsensitiveContains(search) || $0.format.localizedCaseInsensitiveContains(search)) && (!onlyFavorites || favorites.contains($0.id)) }
    }
    var selectedFonts: [FontFace] { fonts.filter { selection.contains($0.id) } }
    var selectedQueue: [FontFace] { queue.filter { queueSelection.contains($0.id) } }
    var preview: FontFace? { page == .install ? selectedQueue.first ?? queue.first : selectedFonts.first }
    func record(_ text: String) {
        activity.insert(ActivityEntry(date: Date(), message: text), at: 0)
        if activity.count > 200 { activity.removeLast(activity.count-200) }
        status = text
    }
    private func perform(_ title: String, action: @escaping @MainActor () async throws -> Void) {
        guard !busy else { return }
        busy = true; taskName = title; status = title
        job = Task {
            defer {
                busy = false; taskName = ""; job = nil
                if !pendingFiles.isEmpty { let files = pendingFiles; pendingFiles = []; addFiles(files) }
            }
            do { try await action() }
            catch is CancellationError { record(t("작업을 중지했습니다.", "Task stopped.")) }
            catch { self.error = error.localizedDescription; record(error.localizedDescription) }
        }
    }
    func cancel() {
        pendingFiles = []
        finishChoices([])
        job?.cancel()
        status = t("현재 파일 처리를 정리하는 중…", "Finishing the current file operation…")
    }
    func cancelAndWait() async { let running = job; cancel(); await running?.value }
    func cleanup() { try? FileManager.default.removeItem(at: workspace) }
    func refresh() { perform(t("폰트 목록 읽는 중…", "Reading fonts…")) { try await self.reload() } }
    private func reload() async throws {
        fonts = try await repository.library(includeSystem: showSystem)
        selection.formIntersection(Set(fonts.map(\.id)))
        status = t("폰트 \(fonts.count)개", "\(fonts.count) \(fonts.count == 1 ? "font" : "fonts")")
    }
    func openFiles() {
        guard !busy else { return }
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true; panel.canChooseDirectories = true
        panel.allowedContentTypes = ["ttf","otf","ttc","otc","zip"].compactMap { UTType(filenameExtension: $0) }
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
    private func importIntoQueue(_ urls: [URL]) async throws {
        let imported = try await importer.read(urls)
        let known = Set(queue.map(\.id))
        let added = imported.filter { !known.contains($0.id) }
        queue += added
        // Prefer one file format for a PostScript name, avoiding duplicate TTF/OTF installation.
        var names = Set(queue.filter { checked.contains($0.id) }.map(\.postScript))
        for face in added.sorted(by: { ($0.format == "TTF" ? 0 : 1) < ($1.format == "TTF" ? 0 : 1) }) {
            if names.insert(face.postScript).inserted { checked.insert(face.id) }
        }
        if queueSelection.isEmpty, let first = queue.first { queueSelection = [first.id] }
        record(t("폰트 \(added.count)개를 대기열에 추가했습니다.", "Added \(added.count) fonts to the queue."))
    }
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
                    let choices = try await FontDownloads.resolve(url)
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
    private func choose(_ choices: [DownloadCandidate]) async -> [DownloadCandidate] {
        await withCheckedContinuation { continuation in
            chooser = continuation; downloadRequest = DownloadRequest(candidates: choices)
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
            var count = 0, failures: [String] = []
            for (index, file) in files.enumerated() {
                try Task.checkCancellation()
                self.status = self.t("설치 중", "Installing") + " \(index+1) / \(files.count)"
                do {
                    if try await self.repository.install(file) { count += 1 }
                    let done = Set(self.queue.filter { $0.file == file }.map(\.id))
                    self.queue.removeAll { done.contains($0.id) }; self.checked.subtract(done); self.queueSelection.subtract(done)
                } catch is CancellationError { throw CancellationError() }
                catch { failures.append(error.localizedDescription) }
            }
            try await self.reload()
            self.record(self.t("폰트 파일 \(count)개 설치 완료", "Installed \(count) font files"))
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
    private func reloadAfterMutation() async {
        await Task { try? await self.reload() }.value
    }
    func deleteSelected() {
        let keys = Set(selectedFonts.compactMap(\.managedKey))
        guard !keys.isEmpty else { return }
        let alert = NSAlert()
        alert.messageText = t("폰트 파일 \(keys.count)개를 삭제할까요?", "Move \(keys.count) font files to Trash?")
        alert.informativeText = t("Font Pocket이 설치한 파일만 휴지통으로 옮깁니다. 필요하면 먼저 백업하세요.", "Only files installed by Font Pocket will be moved to Trash. Back them up first if needed.")
        alert.addButton(withTitle: t("휴지통으로 이동", "Move to Trash")); alert.addButton(withTitle: t("취소", "Cancel"))
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        perform(t("폰트 삭제 중…", "Removing fonts…")) {
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
            try await FontBackup.create(files: files, destination: destination)
            self.record(self.t("백업을 저장했습니다.", "Backup saved."))
            NSWorkspace.shared.activateFileViewerSelecting([destination])
        }
    }
    func toggleFavorites() {
        for face in selectedFonts { if favorites.contains(face.id) { favorites.remove(face.id) } else { favorites.insert(face.id) } }
    }
}
