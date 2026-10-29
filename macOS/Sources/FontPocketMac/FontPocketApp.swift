import SwiftUI
import AppKit

@main @MainActor struct FontPocketApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate
    @StateObject private var model: AppModel
    init() {
        do { let initialModel = try AppModel(); _model = StateObject(wrappedValue: initialModel) }
        catch {
            // Show a useful recovery error instead of silently replacing a damaged catalog.
            let alert = NSAlert(); alert.messageText = "Font Pocket"; alert.informativeText = error.localizedDescription
            alert.runModal(); exit(1)
        }
    }
    var body: some Scene {
        WindowGroup("Font Pocket", id: "main") {
            MainView().environmentObject(model)
                .frame(minWidth: 960, minHeight: 680)
                .background(WindowHook(delegate: delegate, model: model))
                .onOpenURL { model.addFiles([$0]) }
                .task {
                    delegate.model = model
                    if ProcessInfo.processInfo.arguments.contains("--smoke-test") { await SmokeTest.run(model) }
                    else { model.refresh() }
                }
        }
        .defaultSize(width: 1180, height: 820)
        .commands {
            CommandGroup(replacing: .newItem) {
                Button(model.t("폰트 추가…", "Add Fonts…")) { model.openFiles() }.keyboardShortcut("o").disabled(model.busy)
            }
        }
        Settings { SettingsView().environmentObject(model).frame(width: 460, height: 300) }
    }
}

@MainActor final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    weak var model: AppModel?
    private var exiting = false
    func applicationDidFinishLaunching(_ notification: Notification) { NSApp.setActivationPolicy(.regular); NSApp.activate(ignoringOtherApps: true) }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    private func confirm(_ model: AppModel) -> Bool {
        let alert = NSAlert()
        alert.messageText = model.t("작업이 진행 중입니다", "A task is in progress")
        alert.informativeText = model.taskName + "\n\n" + model.t("작업을 중지하고 종료할까요? 현재 파일 처리를 정리한 뒤 종료합니다.", "Stop the task and quit? The current file operation will be cleaned up first.")
        alert.addButton(withTitle: model.t("계속 사용", "Keep Working"))
        alert.addButton(withTitle: model.t("중지하고 종료", "Stop and Quit"))
        return alert.runModal() == .alertSecondButtonReturn
    }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard let model, model.busy else { return .terminateNow }
        if exiting { return .terminateCancel }
        guard confirm(model) else { return .terminateCancel }
        exiting = true
        Task { await model.cancelAndWait(); NSApp.reply(toApplicationShouldTerminate: true) }
        return .terminateLater
    }
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        guard let model, model.busy else { return true }
        guard !exiting, confirm(model) else { return false }
        exiting = true
        Task { await model.cancelAndWait(); NSApp.terminate(nil) }
        return false
    }
    func applicationWillTerminate(_ notification: Notification) { model?.cleanup() }
}

struct WindowHook: NSViewRepresentable {
    let delegate: AppDelegate
    let model: AppModel
    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async { delegate.model = model; view.window?.delegate = delegate }
        return view
    }
    func updateNSView(_ view: NSView, context: Context) {}
}
