import XCTest
import CoreText
@testable import FontPocketCore

private final class RegistrationTrace: FontRegistration, @unchecked Sendable {
    private let lock = NSLock()
    private var count = 0
    var cancellingHash = ""
    var registrations: Int { lock.lock(); defer { lock.unlock() }; return count }
    func register(_ url: URL) throws {
        if !cancellingHash.isEmpty && url.lastPathComponent.contains(cancellingHash) {
            withUnsafeCurrentTask { $0?.cancel() }
            throw CancellationError()
        }
        lock.lock(); count += 1; lock.unlock()
    }
    func unregister(_ url: URL) throws { }
}

final class BugRegressionTests: XCTestCase {
    var root: URL!
    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    }
    override func tearDownWithError() throws { try FileManager.default.removeItem(at: root) }
    func fixture() throws -> URL {
        guard let file = ProcessInfo.processInfo.environment["FONTPOCKET_TEST_FONT"] else { throw XCTSkip("Font fixture required") }
        return URL(fileURLWithPath: file)
    }
    func paths() -> LibraryPaths { LibraryPaths(root: root.appendingPathComponent("Library"), fonts: root.appendingPathComponent("Fonts")) }
    func candidate() throws -> URL {
        var data = try Data(contentsOf: fixture()); data.append(contentsOf: [0,0,0,0])
        let file = root.appendingPathComponent("candidate.ttf"); try data.write(to: file); return file
    }

    func testRepeatedPreviewsReuseDescriptorAndSizeWithoutReopeningFile() throws {
        let file = root.appendingPathComponent("font.ttf")
        try FileManager.default.copyItem(at: fixture(), to: file)
        let first = try XCTUnwrap(FontReader.faces(file).first)
        let second = try XCTUnwrap(FontReader.faces(file, state: .disabled, managedKey: "owned").first)
        XCTAssertTrue(first.renderSource === second.renderSource)
        XCTAssertTrue(first.previewFont(size: 28) === first.previewFont(size: 28))
        XCTAssertEqual(second.state, .disabled)
        XCTAssertTrue(second.id.hasPrefix("owned|"))
        var bytes = try Data(contentsOf: file); bytes.append(contentsOf: [0,0,0,0]); try bytes.write(to: file)
        let changed = try XCTUnwrap(FontReader.faces(file).first)
        XCTAssertFalse(first.renderSource === changed.renderSource)
        XCTAssertEqual(changed.bytes, first.bytes + 4)
    }

    func testCharacterSearchSeparatesLiteralLettersFromUnicodeNumbers() throws {
        let index = CharacterIndex([0, 32, 65, 66, 0xac00, 0xac01, 0xd800, 0x10ffff])
        XCTAssertEqual(try index.matching("A", script: ""), [65])
        XCTAssertEqual(try index.matching("AB", script: ""), [65,66])
        XCTAssertEqual(try index.matching("u+ac0", script: ""), [0xac00,0xac01])
        XCTAssertEqual(try index.matching("", script: "Hangul syllables"), [0xac00,0xac01])
        XCTAssertEqual(index.coverage["Hangul syllables"], 2)
        XCTAssertFalse(index.characters.contains(0)); XCTAssertFalse(index.characters.contains(0xd800))
    }

    func testCancelledVersionSwitchStillRestoresOriginalActivation() async throws {
        let trace = RegistrationTrace(), repo = try FontRepository(paths: paths(), registration: trace)
        let original = try fixture(), next = try candidate(), originalHash = try FontReader.hash(original)
        _ = try await repo.install(original)
        trace.cancellingHash = try FontReader.hash(next)
        let name = try XCTUnwrap(FontReader.faces(original).first?.postScript)
        let operation = Task { () -> Bool in
            do { try await repo.switchVersion(next, postScript: name); return false }
            catch { return true }
        }
        let failed = await operation.value
        XCTAssertTrue(failed)
        let records = try await repo.snapshot()
        XCTAssertEqual(records.count, 1)
        XCTAssertEqual(records[0].0.key, originalHash)
        XCTAssertTrue(records[0].0.enabled)
        XCTAssertEqual(trace.registrations, 2)
    }

    func testMissingManagedFontCanBeReinstalledWithoutChangingItsState() async throws {
        let trace = RegistrationTrace(), repo = try FontRepository(paths: paths(), registration: trace)
        let file = try fixture()
        _ = try await repo.install(file, enabled: false)
        let first = try await repo.snapshot()
        XCTAssertEqual(trace.registrations, 0)
        try FileManager.default.removeItem(at: first[0].1)
        let repaired = try await repo.install(file)
        XCTAssertTrue(repaired)
        let records = try await repo.snapshot()
        XCTAssertFalse(records[0].0.enabled)
        XCTAssertEqual(try FontReader.hash(records[0].1), try FontReader.hash(file))
        XCTAssertEqual(trace.registrations, 0)
    }

    func testReinstallRefusesToOverwriteAnExternallyChangedManagedFile() async throws {
        let repo = try FontRepository(paths: paths(), registration: RegistrationTrace())
        _ = try await repo.install(fixture())
        let file = try await repo.snapshot()[0].1
        let changed = Data("changed outside Font Pocket".utf8); try changed.write(to: file)
        do { _ = try await repo.install(fixture()); XCTFail("Changed file was overwritten") } catch { }
        XCTAssertEqual(try Data(contentsOf: file), changed)
    }

    func testRecaptureRepairsMissingAndCorruptVaultOriginals() async throws {
        let vault = try VersionVault(root: root.appendingPathComponent("Versions"))
        let records = try await vault.capture(fixture()), record = try XCTUnwrap(records.first)
        let saved = try await vault.file(record)
        try FileManager.default.removeItem(at: saved)
        _ = try await vault.capture(fixture())
        let repaired = try await vault.file(record)
        try Data("corrupt".utf8).write(to: repaired)
        _ = try await vault.capture(fixture())
        let valid = try await vault.file(record)
        XCTAssertEqual(try FontReader.hash(valid), record.hash)
        let reopened = try await vault.read(); XCTAssertEqual(reopened.count, records.count)
    }

    func testVersionSelectionCannotActivateAnUnrelatedStyle() async throws {
        let repo = try FontRepository(paths: paths(), registration: RegistrationTrace())
        _ = try await repo.install(fixture())
        do { try await repo.switchVersion(fixture(), postScript: "An unrelated style"); XCTFail("Unrelated style accepted") } catch { }
        let records = try await repo.snapshot(); XCTAssertTrue(records[0].0.enabled)
    }

    func testPersonalSaveMergesIndependentFieldsAndKeepsRestoredCollections() async throws {
        let store = try PersonalLibrary(root: root.appendingPathComponent("Personal"))
        var baseline = PersonalDocument(), note = FontAnnotation(); note.note = "original"; baseline.annotations["font"] = note
        try await store.save(baseline)
        var edited = baseline; edited.annotations["font"]?.note = "edited"
        var restored = baseline; restored.annotations["font"]?.favorite = true
        restored.collections = [FontCollection(name: "Restored")]; try await store.save(restored)
        let saved = try await store.saveChanges(edited, baseline: baseline)
        XCTAssertEqual(saved.annotations["font"]?.note, "edited")
        XCTAssertEqual(saved.annotations["font"]?.favorite, true)
        XCTAssertEqual(saved.collections, restored.collections)
    }

    func testPersonalSaveRejectsConflictingEditsWithoutOverwritingDisk() async throws {
        let store = try PersonalLibrary(root: root.appendingPathComponent("Personal"))
        var baseline = PersonalDocument(); baseline.annotations["font"] = FontAnnotation()
        var first = baseline; first.annotations["font"]?.note = "first"
        var second = baseline; second.annotations["font"]?.note = "second"
        try await store.save(first)
        do { _ = try await store.saveChanges(second, baseline: baseline); XCTFail("Conflicting save accepted") }
        catch is LibraryEditConflict { }
        let saved = try await store.read(); XCTAssertEqual(saved, first)
    }

    func testDuplicateKeepChoicesAreProtectedAcrossOverlappingGroups() {
        func face(_ name: String) -> FontFace { FontFace(id:name,file:root.appendingPathComponent(name+".ttf"),family:"Example",style:"Regular",postScript:"Example-Regular",format:"TTF",bytes:100,state:.enabled,managedKey:name) }
        let a=face("A"),b=face("B"),c=face("C")
        let groups=[DuplicateGroup(id:"exact",exact:true,fonts:[a,b]),DuplicateGroup(id:"style",exact:false,fonts:[b,c])]
        let deletion=DuplicateFinder.deletionCandidates(groups,selected:["exact","style"],keep:["exact":b.id,"style":c.id])
        XCTAssertEqual(deletion.map(\.id),[a.id])
    }

    func testInstallRemapPreservesManualCollectionMembership() {
        var doc=PersonalDocument();doc.annotations["queue"] = FontAnnotation();doc.collections=[FontCollection(name:"UI",members:["queue"])]
        doc.remap("queue",to:"installed")
        XCTAssertNil(doc.annotations["queue"]);XCTAssertNotNil(doc.annotations["installed"])
        XCTAssertEqual(doc.collections[0].members,["installed"])
    }

    func testRestoreRejectsInvalidLaterRecordBeforeInstallingAnything() async throws {
        let folder=root.appendingPathComponent("Source/FontPocketEnvironment"),fonts=folder.appendingPathComponent("Fonts")
        try FileManager.default.createDirectory(at:fonts,withIntermediateDirectories:true)
        let source=try fixture(),next=try candidate(),a=try FontReader.hash(source),b=try FontReader.hash(next)
        let good="FontPocket-"+a+".ttf",bad="FontPocket-"+b+".invalid"
        try FileManager.default.copyItem(at:source,to:fonts.appendingPathComponent(good));try FileManager.default.copyItem(at:next,to:fonts.appendingPathComponent(bad))
        let manifest=EnvironmentManifest(schema:1,personal:PersonalDocument(),fonts:[ProfileFont(filename:good,hash:a,enabled:true),ProfileFont(filename:bad,hash:b,enabled:true)])
        try JSONEncoder().encode(manifest).write(to:folder.appendingPathComponent("manifest.json"))
        let archive=root.appendingPathComponent("invalid.zip");_ = try await ProcessRunner.run("/usr/bin/ditto",["-c","-k","--keepParent",folder.path,archive.path])
        let trace=RegistrationTrace(),repo=try FontRepository(paths:paths(),registration:trace),personal=try PersonalLibrary(root:root.appendingPathComponent("Personal")),vault=try VersionVault(root:root.appendingPathComponent("Vault"))
        do{try await EnvironmentBackup.restore(archive,repository:repo,personal:personal,vault:vault);XCTFail("Invalid record accepted")}catch{}
        let snapshot=try await repo.snapshot();XCTAssertTrue(snapshot.isEmpty);XCTAssertEqual(trace.registrations,0)
    }
}
