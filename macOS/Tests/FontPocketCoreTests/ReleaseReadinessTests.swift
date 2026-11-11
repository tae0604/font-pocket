import XCTest
@testable import FontPocketCore

private struct ReleaseRegistration: FontRegistration { func register(_ url:URL) throws {}; func unregister(_ url:URL) throws {} }

final class ReleaseReadinessTests: XCTestCase {
    var root:URL!
    override func setUpWithError() throws { root=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString);try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true) }
    override func tearDownWithError() throws { try FileManager.default.removeItem(at:root) }
    func fixture() throws -> URL { guard let path=ProcessInfo.processInfo.environment["FONTPOCKET_TEST_FONT"] else { throw XCTSkip("Font fixture required") };return URL(fileURLWithPath:path) }
    func face(_ id:String,weight:Double,bytes:Int64=100,file:String?=nil)->FontFace {
        FontFace(id:id,file:root.appendingPathComponent(file ?? id+".ttf"),family:"Example",style:id,postScript:id,format:"TTF",bytes:bytes,state:.available,weight:weight)
    }
    func asset(_ version:String,size:Int=100,host:String="github.com")->MacReleaseCatalog.Asset {
        let name="FontPocket-macOS-"+version+"-universal.zip"
        return .init(name:name,url:URL(string:"https://"+host+"/tae0604/font-pocket/releases/download/test/"+name)!,size:size)
    }
    func release(_ version:String,preview:Bool=false,withHash:Bool=true)->MacReleaseCatalog.Release {
        let app=asset(version)
        return .init(tag:version,prerelease:preview,assets:[app]+(withHash ? [.init(name:app.name+".sha256",url:URL(string:app.browser_download_url.absoluteString+".sha256")!,size:96)] : []))
    }
    func testWeightOrderingAndFamilyPickerAreStable() {
        let bold=face("Bold",weight:0.4),light=face("Light",weight:-0.4),extra=face("ExtraLight",weight:-0.6)
        XCTAssertEqual([bold,light,extra].sorted(by:FontOrdering.precedes).map(\.id),["ExtraLight","Light","Bold"])
        XCTAssertEqual(FontOrdering.family(of:light,in:[bold,light,extra,light]).map(\.id),["ExtraLight","Light","Bold"])
    }
    func testComparisonCountsSharedCollectionOnceAndRejectsOversizedFiles() {
        let a=face("A",weight:0,bytes:150*1024*1024,file:"shared.ttc"),b=face("B",weight:0.2,bytes:150*1024*1024,file:"shared.ttc")
        XCTAssertTrue(FontOrdering.comparisonAllowed([a,b]))
        XCTAssertFalse(FontOrdering.comparisonAllowed([a,face("C",weight:0,bytes:150*1024*1024)]))
        XCTAssertFalse(FontOrdering.comparisonAllowed([a]))
        XCTAssertFalse(FontOrdering.comparisonAllowed([a,face("huge",weight:0,bytes:Int64.max)]))
    }
    func testMixedImportKeepsValidFontsAndReportsInvalidFile() async throws {
        let folder=root.appendingPathComponent("Mixed");try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        try FileManager.default.copyItem(at:fixture(),to:folder.appendingPathComponent("valid.ttf"))
        try Data("this is a damaged font".utf8).write(to:folder.appendingPathComponent("broken.ttf"))
        let importer=try FontImporter(workspace:root.appendingPathComponent("Queue"))
        let result=try await importer.readReport([folder])
        XCTAssertFalse(result.faces.isEmpty);XCTAssertEqual(result.failures.count,1);XCTAssertTrue(result.failures[0].contains("broken.ttf"))
    }
    func testEmptyFolderIsReportedInsteadOfSilentSuccess() async throws {
        let folder=root.appendingPathComponent("Empty");try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let importer=try FontImporter(workspace:root.appendingPathComponent("Queue")),result=try await importer.readReport([folder])
        XCTAssertTrue(result.faces.isEmpty);XCTAssertEqual(result.failures.count,1)
    }
    func testCatalogRejectsDuplicateOwnershipRecordsBeforeMutations() throws {
        let paths=LibraryPaths(root:root.appendingPathComponent("Library"),fonts:root.appendingPathComponent("Fonts"))
        try FileManager.default.createDirectory(at:paths.root,withIntermediateDirectories:true)
        let key=String(repeating:"a",count:64),record=OwnedFont(key:key,filename:"FontPocket-"+key+".ttf",enabled:true)
        try JSONEncoder().encode([record,record]).write(to:paths.catalog)
        XCTAssertThrowsError(try FontRepository(paths:paths,registration:ReleaseRegistration()))
    }
    func testUpdateSelectionUsesHighestMacVersionRatherThanPublicationOrder() {
        XCTAssertEqual(MacReleaseCatalog.latest([release("0.2.2"),release("0.10.0"),release("0.3.0")])?.version,"0.10.0")
    }
    func testUpdateSelectionSkipsPreviewsMissingChecksumsAndForeignSources() {
        let foreign=asset("99.0.0",host:"example.com")
        let fake=MacReleaseCatalog.Release(tag:"fake",assets:[foreign,.init(name:foreign.name+".sha256",url:foreign.browser_download_url,size:90)])
        XCTAssertEqual(MacReleaseCatalog.latest([release("5.0.0",preview:true),release("4.0.0",withHash:false),fake,release("0.3.0")])?.version,"0.3.0")
        XCTAssertNil(MacReleaseCatalog.latest([release("1.0.0",withHash:false)]))
    }
    func testUpdateVersionRejectsLooseFilenameMatches() {
        for name in ["evilFontPocket-macOS-9.0.0-universal.zip","FontPocket-macOS-9.0.0-universal.zip.tmp","FontPocket-macOS-9.0-beta-universal.zip","FontPocket-macOS-9..0-universal.zip"] { XCTAssertNil(MacReleaseCatalog.version(name)) }
        XCTAssertEqual(MacReleaseCatalog.version("FontPocket-macOS-0.3.0-universal.zip"),"0.3.0")
    }
    func testCancelledRestoreKeepsMetadataForAlreadyCommittedFonts() async throws {
        let repo=try FontRepository(paths:LibraryPaths(root:root.appendingPathComponent("A"),fonts:root.appendingPathComponent("AF")),registration:ReleaseRegistration())
        let source=try fixture(),next=root.appendingPathComponent("next.ttf")
        var bytes=try Data(contentsOf:source);bytes.append(contentsOf:[0,0,0,0]);try bytes.write(to:next)
        _ = try await repo.install(source);_ = try await repo.install(next,enabled:false)
        let personal=try PersonalLibrary(root:root.appendingPathComponent("AP")),vault=try VersionVault(root:root.appendingPathComponent("AV"))
        let key=try FontReader.hash(source),ps=try XCTUnwrap(FontReader.faces(source).first?.postScript),id=key+"|"+ps
        var document=PersonalDocument(),annotation=FontAnnotation();annotation.note="Keep after stopping restore";annotation.favorite=true
        document.annotations[id]=annotation;document.collections=[FontCollection(name:"Restored",members:[id])];try await personal.save(document)
        let zip=root.appendingPathComponent("environment.zip");try await EnvironmentBackup.create(repository:repo,personal:personal,vault:vault,destination:zip)
        let target=try FontRepository(paths:LibraryPaths(root:root.appendingPathComponent("B"),fonts:root.appendingPathComponent("BF")),registration:ReleaseRegistration())
        let notes=try PersonalLibrary(root:root.appendingPathComponent("BP")),versions=try VersionVault(root:root.appendingPathComponent("BV"))
        let task=Task {
            try await EnvironmentBackup.restore(zip,repository:target,personal:notes,vault:versions,progress:{ progress in
                if progress.phase == .restoring && progress.completed==1 { withUnsafeCurrentTask { $0?.cancel() } }
            })
        }
        do { try await task.value;XCTFail("Restore was not cancelled") } catch is CancellationError {}
        let snapshot=try await target.snapshot(),saved=try await notes.read()
        XCTAssertEqual(snapshot.count,1);XCTAssertTrue(snapshot[0].0.enabled)
        XCTAssertEqual(saved.annotations[id]?.note,annotation.note);XCTAssertEqual(saved.collections,document.collections)
    }
    func testRestoreMergePreservesExistingEditsAndAddsMissingData() async throws {
        let store=try PersonalLibrary(root:root)
        var existing=PersonalDocument(),old=FontAnnotation();old.note="local edit";existing.annotations["font"]=old;try await store.save(existing)
        var restored=PersonalDocument();restored.annotations["font"]=FontAnnotation();restored.annotations["new"]=FontAnnotation();restored.collections=[FontCollection(name:"Imported")]
        try await store.mergeRestored(restored)
        let saved=try await store.read();XCTAssertEqual(saved.annotations["font"]?.note,"local edit");XCTAssertNotNil(saved.annotations["new"]);XCTAssertEqual(saved.collections,restored.collections)
    }
    func testStopEscalatesWhenSubprocessIgnoresTermination() async throws {
        let task=Task { try await ProcessRunner.run("/bin/sh",["-c","trap '' TERM; while :; do :; done"]) }
        try await Task.sleep(nanoseconds:250_000_000)
        let start=Date();task.cancel()
        do { _ = try await task.value;XCTFail("Process cancellation returned success") } catch is CancellationError {}
        XCTAssertLessThan(Date().timeIntervalSince(start),8)
    }
    func testManagedSymlinkIsRejectedWithoutChangingOutsideOriginal() async throws {
        let paths=LibraryPaths(root:root.appendingPathComponent("Library"),fonts:root.appendingPathComponent("Fonts"))
        let repo=try FontRepository(paths:paths,registration:ReleaseRegistration()),original=try fixture()
        _ = try await repo.install(original)
        let record=try await repo.snapshot()[0],hash=try FontReader.hash(original)
        try FileManager.default.removeItem(at:record.1)
        try FileManager.default.createSymbolicLink(at:record.1,withDestinationURL:original)
        do { try await repo.setEnabled(hash,enabled:false);XCTFail("Managed symlink was accepted") } catch {}
        XCTAssertEqual(try FontReader.hash(original),hash)
        XCTAssertEqual(try FileManager.default.destinationOfSymbolicLink(atPath:record.1.path),original.path)
        let state=await repo.managedState(hash);XCTAssertEqual(state,.enabled)
    }
    func testCorruptZipDoesNotHideGoodSiblingFontInFolder() async throws {
        let folder=root.appendingPathComponent("Mixed");try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        try FileManager.default.copyItem(at:fixture(),to:folder.appendingPathComponent("good.ttf"))
        try Data("invalid archive".utf8).write(to:folder.appendingPathComponent("broken.zip"))
        let importer=try FontImporter(workspace:root.appendingPathComponent("Queue")),result=try await importer.readReport([folder])
        XCTAssertFalse(result.faces.isEmpty);XCTAssertEqual(result.failures.count,1);XCTAssertTrue(result.failures[0].contains("broken.zip"))
    }
}
