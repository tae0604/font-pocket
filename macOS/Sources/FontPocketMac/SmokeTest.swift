import AppKit
import SwiftUI
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
                model.previewText = language == "ko" ? "다람쥐 헌 쳇바퀴에 타고파 0123456789\nThe quick brown fox jumps over the lazy dog." : "The quick brown fox jumps over the lazy dog.\nA typeface for every idea."
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
                model.previewText = language == "ko" ? "다람쥐 헌 쳇바퀴에 타고파 0123456789\nThe quick brown fox jumps over the lazy dog." : "The quick brown fox jumps over the lazy dog.\nA typeface for every idea."
                try await Task.sleep(nanoseconds: 700_000_000)
                try await capture(filename, output: output)
            }
            for (appearance, reduced, name) in [(ColorScheme.light, false, "manager-light.png"), (ColorScheme.dark, false, "manager-dark.png"), (ColorScheme.light, true, "manager-accessible.png")] {
                model.testAppearance = appearance
                model.testReduceTransparency = reduced; model.testReduceMotion = reduced
                try await Task.sleep(nanoseconds: 700_000_000)
                try await capture(name, output: output)
            }
            try await FontBackup.create(files: [installed.file], destination: URL(fileURLWithPath: output).appendingPathComponent("smoke-backup.zip"))
            // Exercise the packaged WKWebView + WebAssembly codec, not a mock.
            let web=model.workspace.appendingPathComponent("roundtrip.woff2")
            let encoded=try await WebFontCodec.convert(installed.file,decompress:false)
            guard encoded.prefix(4)==Data("wOF2".utf8) else{throw PocketError("WOFF2 encoding failed")}
            try encoded.write(to:web)
            let decoded=try await model.importer.read([web])
            guard decoded.contains(where:{$0.postScript==installed.postScript}) else{throw PocketError("WOFF2 decoding failed")}
            var doc=PersonalDocument(),annotation=FontAnnotation();annotation.note="본문용 · Body text";annotation.tags=["body","UI"];annotation.favorite=true
            doc.annotations[installed.id]=annotation;doc.collections=[FontCollection(name:"UI fonts",members:[installed.id]),FontCollection(name:"Body",rule:"body;TTF")]
            try await model.personalStore.save(doc);model.personal=doc
            let environment=URL(fileURLWithPath:output).appendingPathComponent("smoke-environment.zip")
            try await EnvironmentBackup.create(repository:model.repository,personal:model.personalStore,vault:model.vault,destination:environment)
            let archived=try await model.vault.read();guard !archived.isEmpty else{throw PocketError("Installed version was not archived")}
            for layout in ["list","two","grid","family"]{
                let view=try FontPrinting.makeView([installed],text:"다람쥐 · The quick brown fox",layout:layout)
                let pdf=view.dataWithPDF(inside:view.bounds)
                guard pdf.starts(with:Data("%PDF".utf8)) else{throw PocketError("Catalog PDF generation failed")}
                try pdf.write(to:URL(fileURLWithPath:output).appendingPathComponent("catalog-"+layout+".pdf"))
            }
            model.testReduceTransparency=false;model.testReduceMotion=false;model.testAppearance = .light;model.language="ko"
            model.details=true;model.detailsTab="characters"
            try await Task.sleep(nanoseconds:1_500_000_000);try await capture("characters-ko.png",output:output)
            model.detailsTab="inspection";try await Task.sleep(nanoseconds:300_000_000)
            model.details=false;try await Task.sleep(nanoseconds:400_000_000)
            model.toolsTab="collections";model.tools=true
            try await Task.sleep(nanoseconds:800_000_000);try await capture("collections-ko.png",output:output)
            model.toolsTab="versions";try await Task.sleep(nanoseconds:400_000_000)
            model.tools=false;try await Task.sleep(nanoseconds:400_000_000)
            model.quickSearch=true;try await Task.sleep(nanoseconds:400_000_000);model.quickSearch=false
            if let key = installed.managedKey { try await model.repository.setEnabled(key, enabled: false) }
            print("PASS: Native app launched; real import, user-wide install, disable/re-enable, font/environment backup, version archive, notes/tags/collections and packaged WOFF2 encode/decode passed; details, character map, library tools, quick search, Korean/English and accessibility variants rendered.")
            NSApp.terminate(nil)
        } catch { fputs("Smoke test failed: \(error)\n", stderr); exit(1) }
    }
    private static func wait(_ model: AppModel) async throws {
        let deadline = Date().addingTimeInterval(50)
        while model.busy && Date() < deadline { try await Task.sleep(nanoseconds: 100_000_000) }
        guard !model.busy, model.error == nil else { throw NSError(domain: model.error ?? "Operation timed out", code: 8) }
    }
    private static func capture(_ name: String, output: String) async throws {
        guard let window = NSApp.keyWindow ?? NSApp.windows.first(where: { $0.title == "Font Pocket" }), let view = window.contentView else { throw NSError(domain: "No app window", code: 3) }
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
