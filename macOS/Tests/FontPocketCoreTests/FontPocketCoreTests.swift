import XCTest
import Foundation
import CoreText
@testable import FontPocketCore

private struct TestRegistration: FontRegistration {
    var fail = false
    func register(_ url: URL) throws { if fail { throw PocketError("Simulated registration failure") } }
    func unregister(_ url: URL) throws {}
}

final class FontPocketCoreTests: XCTestCase {
    var root: URL!
    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory.appendingPathComponent("FontPocketTests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    }
    override func tearDownWithError() throws { if let root { try FileManager.default.removeItem(at: root) } }
    func fixture() throws -> URL {
        guard let path = ProcessInfo.processInfo.environment["FONTPOCKET_TEST_FONT"] else { throw XCTSkip("Set FONTPOCKET_TEST_FONT to a licensed TTF fixture.") }
        let source = URL(fileURLWithPath: path), file = root.appendingPathComponent("견본.ttf")
        try FileManager.default.copyItem(at: source, to: file)
        return file
    }
    func paths() -> LibraryPaths { LibraryPaths(root: root.appendingPathComponent("Library"), fonts: root.appendingPathComponent("Fonts")) }
    func testGoogleFontsFamilyAndHTMLLinks() throws {
        XCTAssertEqual(FontDownloads.googleFamily(URL(string: "https://fonts.google.com/specimen/Poppins?preview.lang=en_Latn")!), "poppins")
        XCTAssertNil(FontDownloads.googleFamily(URL(string: "https://example.com/specimen/Poppins")!))
        let links = FontDownloads.links(in: "<script><a href='https://bad.test/font.zip'>bad</a></script><a href='/releases/font.zip?a=1&amp;b=2'>Download <b>TTF</b></a><a href='javascript:alert(1)'>bad</a>", base: URL(string: "https://example.com/fonts/")!)
        XCTAssertEqual(links.count, 1)
        XCTAssertEqual(links.first?.0.absoluteString, "https://example.com/releases/font.zip?a=1&b=2")
        XCTAssertEqual(links.first?.1, "Download TTF")
    }
    func testUserScopeCoreTextRegistrationOnHostedMac() throws {
        guard ProcessInfo.processInfo.environment["FONTPOCKET_LIVE_CORETEXT_TEST"] == "1" else {
            throw XCTSkip("User-wide temporary registration is only enabled on the disposable CI Mac.")
        }
        let file = try fixture(), registration = CoreTextRegistration()
        try registration.register(file)
        defer { try? registration.unregister(file) }
        try registration.register(file)
        try registration.unregister(file)
    }
    func testTraversalPathsRejected() {
        for path in ["../font.ttf", "/font.ttf", "a/../../font.ttf", "a\\font.ttf", "C:font.ttf", "a\nfont.ttf"] { XCTAssertFalse(ArchivePolicy.safePath(path), path) }
        XCTAssertTrue(ArchivePolicy.safePath("ttf/견본.ttf"))
        XCTAssertThrowsError(try ArchivePolicy.validate(Data("not a zip".utf8)))
    }
    func testLiveDownloadSourcesOnHostedMac() async throws {
        guard ProcessInfo.processInfo.environment["FONTPOCKET_LIVE_DOWNLOAD_TEST"] == "1" else {
            throw XCTSkip("Live provider checks only run on the hosted preview builder.")
        }
        for input in ["https://noonnu.cc/font_page/694", "https://noonnu.cc/font_page/845", "https://fonts.google.com/specimen/Poppins?preview.lang=en_Latn"] {
            let choices = try await FontDownloads.resolve(URL(string: input)!)
            XCTAssertFalse(choices.isEmpty, input)
            if input.contains("694") { continue }
            let preferred = choices.first { $0.name.lowercased().contains("bold") && !$0.name.lowercased().contains("italic") }
                ?? choices.first { $0.name.lowercased().contains("ttf") } ?? choices[0]
            let download = try await FontDownloads.download(preferred, into: root.appendingPathComponent("Downloads"))
            let importer = try FontImporter(workspace: root.appendingPathComponent(UUID().uuidString))
            let faces = try await importer.read([download])
            XCTAssertFalse(faces.isEmpty, input)
        }
    }
    func testFontMetadataAndInstallationLifecycle() async throws {
        let file = try fixture(), repo = try FontRepository(paths: paths(), registration: TestRegistration())
        let faces = try FontReader.faces(file)
        XCTAssertFalse(faces.isEmpty); XCTAssertGreaterThan(CTFontGetGlyphCount(faces[0].previewFont(size: 28)), 0)
        let installed = try await repo.install(file); XCTAssertTrue(installed)
        let duplicate = try await repo.install(file); XCTAssertFalse(duplicate)
        let key = try FontReader.hash(file)
        try await repo.setEnabled(key, enabled: false)
        XCTAssertTrue(FileManager.default.fileExists(atPath: paths().disabled.appendingPathComponent("FontPocket-\(key).ttf").path))
        try await repo.setEnabled(key, enabled: true)
        XCTAssertTrue(FileManager.default.fileExists(atPath: paths().fonts.appendingPathComponent("FontPocket-\(key).ttf").path))
        let reopened = try FontRepository(paths: paths(), registration: TestRegistration())
        let duplicateAfterRestart = try await reopened.install(file); XCTAssertFalse(duplicateAfterRestart)
    }
    func testRegistrationFailureRollsBackFileAndCatalog() async throws {
        let file = try fixture(), repo = try FontRepository(paths: paths(), registration: TestRegistration(fail: true))
        do { _ = try await repo.install(file); XCTFail("Expected registration failure") } catch {}
        let remaining = try FileManager.default.contentsOfDirectory(at: paths().fonts, includingPropertiesForKeys: nil)
        XCTAssertTrue(remaining.isEmpty)
        XCTAssertFalse(FileManager.default.fileExists(atPath: paths().catalog.path))
    }
    func testModifiedOwnedFileIsNotDisabled() async throws {
        let file = try fixture(), repo = try FontRepository(paths: paths(), registration: TestRegistration())
        _ = try await repo.install(file)
        let key = try FontReader.hash(file), destination = paths().fonts.appendingPathComponent("FontPocket-\(key).ttf")
        try Data("changed outside app".utf8).write(to: destination)
        do { try await repo.setEnabled(key, enabled: false); XCTFail("Expected changed-file refusal") } catch {}
        XCTAssertTrue(FileManager.default.fileExists(atPath: destination.path))
    }
    func testBackupRestoreAndQueueDeduplication() async throws {
        let file = try fixture(), backup = root.appendingPathComponent("backup.zip")
        try await FontBackup.create(files: [file], destination: backup)
        try ArchivePolicy.validate(Data(contentsOf: backup))
        let importer = try FontImporter(workspace: root.appendingPathComponent("Import"))
        let faces = try await importer.read([backup, file])
        XCTAssertEqual(faces.count, try FontReader.faces(file).count)
    }
    func testArchiveSymlinkAndOversizedEntryRejected() async throws {
        let file = try fixture(), symlink = root.appendingPathComponent("link.ttf")
        try FileManager.default.createSymbolicLink(at: symlink, withDestinationURL: file)
        let zip = root.appendingPathComponent("link.zip")
        _ = try await ProcessRunner.run("/usr/bin/zip", ["-j", "-y", zip.path, symlink.path])
        XCTAssertThrowsError(try ArchivePolicy.validate(Data(contentsOf: zip)))
        let safeZIP = root.appendingPathComponent("safe.zip")
        _ = try await ProcessRunner.run("/usr/bin/zip", ["-j", safeZIP.path, file.path])
        var data = try Data(contentsOf: safeZIP)
        let marker = Data([0x50,0x4b,0x01,0x02])
        let index = try XCTUnwrap(data.range(of: marker)?.lowerBound)
        data.replaceSubrange(index+24..<index+28, with: [0xff,0xff,0xff,0x7f])
        XCTAssertThrowsError(try ArchivePolicy.validate(data))
    }
    func testCancelledBackupKeepsExistingFile() async throws {
        let file = try fixture(), destination = root.appendingPathComponent("keep.zip"), original = Data("existing backup".utf8)
        try original.write(to: destination)
        let task = Task {
            try await Task.sleep(nanoseconds: 5_000_000_000)
            try await FontBackup.create(files: [file], destination: destination)
        }
        task.cancel()
        do { try await task.value; XCTFail("Expected cancellation") } catch is CancellationError {} catch { XCTFail("\(error)") }
        XCTAssertEqual(try Data(contentsOf: destination), original)
    }
}
