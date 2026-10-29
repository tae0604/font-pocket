import XCTest
import Foundation
@testable import FontPocketCore

private struct ParityRegistration:FontRegistration{func register(_ url:URL)throws{};func unregister(_ url:URL)throws{}}
private struct SelectiveRegistration:FontRegistration{let rejected:String;func register(_ url:URL)throws{if url.lastPathComponent.contains(rejected){throw PocketError("Rejected candidate")}};func unregister(_ url:URL)throws{}}
final class ParityTests:XCTestCase{
    func fixture()throws->URL{guard let path=ProcessInfo.processInfo.environment["FONTPOCKET_TEST_FONT"] else{throw XCTSkip("Font fixture required")};return URL(fileURLWithPath:path)}
    func temporary()throws->URL{let path=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString);try FileManager.default.createDirectory(at:path,withIntermediateDirectories:true);return path}
    func testMetadataGlyphCoverageAndIntegrity()throws{let font=try XCTUnwrap(FontReader.faces(fixture()).first),report=try FontAnalysis.inspect(font);XCTAssertGreaterThan(report.glyphs,100);XCTAssertTrue(report.characters.contains(0xac00));XCTAssertTrue(report.tables.contains("cmap"));XCTAssertNotNil(report.metadata["Version"]);XCTAssertTrue(report.issues.isEmpty)}
    func testPersonalDataAndSmartCollectionsRoundTrip()async throws{let root=try temporary();defer{try? FileManager.default.removeItem(at:root)};let store=try PersonalLibrary(root:root);var doc=PersonalDocument();var note=FontAnnotation();note.note="본문용";note.tags=["body","한글"];let font=try XCTUnwrap(FontReader.faces(fixture()).first);doc.annotations[font.id]=note;doc.collections=[FontCollection(name:"Body",rule:"body;TTF"),FontCollection(name:"Manual",members:[font.id])];try await store.save(doc);let restored=try await store.read();XCTAssertEqual(doc,restored);XCTAssertTrue(restored.collections.allSatisfy{$0.contains(font,annotation:note)})}
    func testVersionVaultDeduplicationAndIntegrity()async throws{let root=try temporary();defer{try? FileManager.default.removeItem(at:root)};let vault=try VersionVault(root:root);let first=try await vault.capture(fixture());_ = try await vault.capture(fixture());let records=try await vault.read();XCTAssertEqual(first.count,records.count);let saved=try await vault.file(records[0]);try Data("tampered".utf8).write(to:saved);do{_ = try await vault.file(records[0]);XCTFail("Tampered version accepted")}catch{}}
    func testEnvironmentRoundTripPreservesStateAndNotes()async throws{let root=try temporary();defer{try? FileManager.default.removeItem(at:root)};let repo=try FontRepository(paths:LibraryPaths(root:root.appendingPathComponent("A"),fonts:root.appendingPathComponent("AF")),registration:ParityRegistration());let personal=try PersonalLibrary(root:root.appendingPathComponent("AP")),vault=try VersionVault(root:root.appendingPathComponent("AV"));let file=try fixture();_ = try await repo.install(file);let hash=try FontReader.hash(file);try await repo.setEnabled(hash,enabled:false);var doc=PersonalDocument();var note=FontAnnotation();note.note="preserve me";doc.annotations[hash]=note;try await personal.save(doc);_ = try await vault.capture(file);let zip=root.appendingPathComponent("environment.zip");try await EnvironmentBackup.create(repository:repo,personal:personal,vault:vault,destination:zip);let target=try FontRepository(paths:LibraryPaths(root:root.appendingPathComponent("B"),fonts:root.appendingPathComponent("BF")),registration:ParityRegistration());let targetPersonal=try PersonalLibrary(root:root.appendingPathComponent("BP")),targetVault=try VersionVault(root:root.appendingPathComponent("BV"));try await EnvironmentBackup.restore(zip,repository:target,personal:targetPersonal,vault:targetVault);let records=try await target.snapshot();XCTAssertEqual(records.count,1);XCTAssertFalse(records[0].0.enabled);let notes=try await targetPersonal.read();XCTAssertEqual(notes,doc);let versions=try await targetVault.read();XCTAssertFalse(versions.isEmpty)}
    func testDuplicateFinderSeparatesIdenticalFiles()throws{let root=try temporary();defer{try? FileManager.default.removeItem(at:root)};let file=try fixture(),copy=root.appendingPathComponent("copy.ttf");try FileManager.default.copyItem(at:file,to:copy);let fonts=try FontReader.faces(file)+FontReader.faces(copy);let groups=try DuplicateFinder.find(fonts);XCTAssertTrue(groups.contains{$0.exact && $0.fonts.count==2})}
    func testWoffMalformedDataIsRejected(){XCTAssertThrowsError(try WoffDecoder.decode(Data("wOFF".utf8)))}
    func testWoffUncompressedRoundTrip()throws{
        let root=try temporary();defer{try? FileManager.default.removeItem(at:root)}
        let input=try Data(contentsOf:fixture())
        func n(_ p:Int,_ c:Int)->Int{input[p..<p+c].reduce(0){($0<<8)|Int($1)}}
        let count=n(4,2);var woff=Data(repeating:0,count:44+count*20)
        func put(_ v:Int,_ p:Int,_ bytes:Int=4){for i in 0..<bytes{woff[p+i]=UInt8(truncatingIfNeeded:v>>((bytes-i-1)*8))}}
        put(0x774f4646,0);put(n(0,4),4);put(count,12,2);var sfntSize=12+count*16
        for i in 0..<count{let source=12+i*16,length=n(source+12,4),offset=n(source+8,4),p=44+i*20;put(n(source,4),p);put(woff.count,p+4);put(length,p+8);put(length,p+12);put(n(source+4,4),p+16);woff.append(input.subdata(in:offset..<offset+length));while woff.count%4 != 0{woff.append(0)};sfntSize += (length+3) & ~3}
        put(woff.count,8);put(sfntSize,16)
        let output=root.appendingPathComponent("roundtrip.ttf");try WoffDecoder.decode(woff).write(to:output)
        XCTAssertEqual(try FontReader.faces(fixture()).map(\.postScript),try FontReader.faces(output).map(\.postScript));XCTAssertTrue(try FontAnalysis.validateSFNT(output).isEmpty)
    }
    func testVersionSwitchFailureRestoresPreviousActiveFile()async throws{
        let root=try temporary();defer{try? FileManager.default.removeItem(at:root)}
        let original=try fixture(),candidate=root.appendingPathComponent("candidate.ttf")
        var bytes=try Data(contentsOf:original);bytes.append(contentsOf:[0,0,0,0]);try bytes.write(to:candidate)
        let newHash=try FontReader.hash(candidate),oldHash=try FontReader.hash(original)
        let repo=try FontRepository(paths:LibraryPaths(root:root.appendingPathComponent("Library"),fonts:root.appendingPathComponent("Fonts")),registration:SelectiveRegistration(rejected:newHash))
        _ = try await repo.install(original);let ps=try XCTUnwrap(FontReader.faces(original).first?.postScript)
        do{try await repo.switchVersion(candidate,postScript:ps);XCTFail("Candidate should fail")}catch{}
        let records=try await repo.snapshot();XCTAssertEqual(records.count,1);XCTAssertEqual(records[0].0.key,oldHash);XCTAssertTrue(records[0].0.enabled)
    }
    func testAdoptionProtectsOutsideFilesAndKeepsOriginal()async throws{
        let root=try temporary();defer{try? FileManager.default.removeItem(at:root)}
        let paths=LibraryPaths(root:root.appendingPathComponent("Library"),fonts:root.appendingPathComponent("Fonts")),repo=try FontRepository(paths:paths,registration:ParityRegistration())
        do{_ = try await repo.adopt(fixture());XCTFail("Outside file adopted")}catch{}
        let file=paths.fonts.appendingPathComponent("existing.ttf");try FileManager.default.copyItem(at:fixture(),to:file)
        let adopted=try await repo.adopt(file);XCTAssertTrue(adopted.allSatisfy(\.canManage));XCTAssertFalse(FileManager.default.fileExists(atPath:file.path))
        let hash=try FontReader.hash(fixture());XCTAssertEqual(try FontReader.hash(paths.root.appendingPathComponent("Originals/"+hash+".ttf")),hash)
        try await repo.setEnabled(hash,enabled:false)
    }
}
