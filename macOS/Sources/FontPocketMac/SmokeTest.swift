import AppKit
import Foundation

@MainActor enum SmokeTest {
    static func run(_ model: AppModel) async {
        do {
            guard let fixture = ProcessInfo.processInfo.environment["FONTPOCKET_TEST_FONT"],
                  let output = ProcessInfo.processInfo.environment["FONTPOCKET_MAC_SMOKE_OUTPUT"],
                  ProcessInfo.processInfo.environment["FONTPOCKET_MAC_TEST_ROOT"] != nil else { throw NSError(domain: "Smoke test configuration", code: 1) }
            model.language = "ko"
            model.links = "https://noonnu.cc/font_page/694\nhttps://noonnu.cc/font_page/845\nhttps://fonts.google.com/specimen/Poppins"
            model.addFiles([URL(fileURLWithPath: fixture)])
            let deadline = Date().addingTimeInterval(45)
            while model.busy && Date() < deadline { try await Task.sleep(nanoseconds: 100_000_000) }
            guard !model.busy, model.queue.count > 0 else { throw NSError(domain: "Queue did not load", code: 2) }
            try FileManager.default.createDirectory(atPath: output, withIntermediateDirectories: true)
            for (language, filename) in [("ko", "install-ko.png"), ("en", "install-en.png")] {
                model.language = language
                try await Task.sleep(nanoseconds: 600_000_000)
                guard let view = NSApp.windows.first(where: { $0.title == "Font Pocket" })?.contentView,
                      let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { throw NSError(domain: "No app window", code: 3) }
                view.cacheDisplay(in: view.bounds, to: bitmap)
                guard let data = bitmap.representation(using: .png, properties: [:]), data.count > 10000 else { throw NSError(domain: "Empty window image", code: 4) }
                try data.write(to: URL(fileURLWithPath: output).appendingPathComponent(filename))
            }
            print("PASS: Native app launched, real font queue and previews loaded, Korean and English windows rendered.")
            NSApp.terminate(nil)
        } catch { fputs("Smoke test failed: \(error)\n", stderr); exit(1) }
    }
}
