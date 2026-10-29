import AppKit
import Foundation
import FontPocketCore

@MainActor enum SmokeTest {
    static func run(_ model: AppModel) async {
        do {
            guard let fixture = ProcessInfo.processInfo.environment["FONTPOCKET_TEST_FONT"],
                  let output = ProcessInfo.processInfo.environment["FONTPOCKET_MAC_SMOKE_OUTPUT"],
                  ProcessInfo.processInfo.environment["FONTPOCKET_MAC_TEST_ROOT"] != nil else { throw NSError(domain: "Smoke test configuration", code: 1) }
            model.language = "ko"
            model.links = "https://noonnu.cc/font_page/694\nhttps://noonnu.cc/font_page/845\nhttps://fonts.google.com/specimen/Poppins"
            model.addFiles([URL(fileURLWithPath: fixture)])
            try await wait(model)
            guard !model.busy, model.queue.count > 0 else { throw NSError(domain: "Queue did not load", code: 2) }
            try FileManager.default.createDirectory(atPath: output, withIntermediateDirectories: true)
            for (language, filename) in [("ko", "install-ko.png"), ("en", "install-en.png")] {
                model.language = language
                try await Task.sleep(nanoseconds: 600_000_000)
                try await capture(filename, output: output)
            }
            model.installChecked()
            try await wait(model)
            guard let installed = model.fonts.first(where: { $0.canManage && $0.state == .enabled }) else { throw NSError(domain: "Installation did not persist", code: 5) }
            model.selection = [installed.id]
            model.changeEnabled(false); try await wait(model)
            guard model.fonts.contains(where: { $0.id == installed.id && $0.state == .disabled }) else { throw NSError(domain: "Disable failed", code: 6) }
            model.changeEnabled(true); try await wait(model)
            guard model.fonts.contains(where: { $0.id == installed.id && $0.state == .enabled }) else { throw NSError(domain: "Enable failed", code: 7) }
            model.page = .manager
            for (language, filename) in [("ko", "manager-ko.png"), ("en", "manager-en.png")] {
                model.language = language
                try await Task.sleep(nanoseconds: 700_000_000)
                try await capture(filename, output: output)
            }
            try await FontBackup.create(files: [installed.file], destination: URL(fileURLWithPath: output).appendingPathComponent("smoke-backup.zip"))
            if let key = installed.managedKey { try await model.repository.setEnabled(key, enabled: false) }
            print("PASS: Native app launched; real import, user-wide install, disable, re-enable and backup passed; Korean and English windows rendered.")
            NSApp.terminate(nil)
        } catch { fputs("Smoke test failed: \(error)\n", stderr); exit(1) }
    }
    private static func wait(_ model: AppModel) async throws {
        let deadline = Date().addingTimeInterval(50)
        while model.busy && Date() < deadline { try await Task.sleep(nanoseconds: 100_000_000) }
        guard !model.busy, model.error == nil else { throw NSError(domain: model.error ?? "Operation timed out", code: 8) }
    }
    private static func capture(_ name: String, output: String) async throws {
        guard let window = NSApp.windows.first(where: { $0.title == "Font Pocket" || $0.isKeyWindow }), let view = window.contentView else { throw NSError(domain: "No app window", code: 3) }
        let destination = URL(fileURLWithPath: output).appendingPathComponent(name)
        // Capture the compositor so native sidebar / toolbar Liquid Glass is visible.
        // Fall back to the view renderer on runners without screen-capture access.
        do {
            _ = try await ProcessRunner.run("/usr/sbin/screencapture", ["-x", "-l", String(window.windowNumber), destination.path])
            if let bytes = try? destination.resourceValues(forKeys: [.fileSizeKey]).fileSize, bytes > 10000 { return }
        } catch {}
        guard let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { throw NSError(domain: "No window bitmap", code: 4) }
        view.cacheDisplay(in: view.bounds, to: bitmap)
        guard let data = bitmap.representation(using: .png, properties: [:]), data.count > 10000 else { throw NSError(domain: "Empty window image", code: 4) }
        try data.write(to: destination)
    }
}
