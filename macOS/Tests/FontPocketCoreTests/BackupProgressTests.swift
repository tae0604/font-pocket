import XCTest
@testable import FontPocketCore

private final class ProgressHistory: @unchecked Sendable {
    private let lock=NSLock()
    private var storage:[WorkProgress]=[]
    private var cancelAction:(@Sendable ()->Void)?
    func append(_ value:WorkProgress){lock.lock();storage.append(value);let cancel=value.phase == .archiving && value.completed==1 ? cancelAction:nil;lock.unlock();cancel?()}
    var values:[WorkProgress]{lock.lock();defer{lock.unlock()};return storage}
    func cancellation(_ action:(@Sendable ()->Void)?){lock.lock();cancelAction=action;lock.unlock()}
}

final class BackupProgressTests:XCTestCase{
    var root:URL!
    override func setUpWithError()throws{root=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString);try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)}
    override func tearDownWithError()throws{try FileManager.default.removeItem(at:root)}
    func fonts()throws->[URL]{
        guard let path=ProcessInfo.processInfo.environment["FONTPOCKET_TEST_FONT"] else{throw XCTSkip("Font fixture required")}
        return try ["첫 번째.ttf","second (sample).ttf","third.ttf"].map{name in let target=root.appendingPathComponent(name);try FileManager.default.copyItem(at:URL(fileURLWithPath:path),to:target);return target}
    }
    func testBackupReportsActualCompletedFilesAndRoundTripsUnicodeNames()async throws{
        let files=try fonts(),output=root.appendingPathComponent("backup.zip"),history=ProgressHistory()
        try await FontBackup.create(files:files,destination:output,progress:{history.append($0)})
        let compression=history.values.filter{$0.phase == .archiving}
        XCTAssertEqual(compression.map(\.completed),[0,1,2,3])
        XCTAssertTrue(compression.allSatisfy{$0.total==3 && ($0.percent ?? 100)<100})
        XCTAssertEqual(history.values.last?.phase,.complete);XCTAssertEqual(history.values.last?.percent,100)
        let paths=try ArchivePolicy.validate(Data(contentsOf:output));XCTAssertTrue(paths.contains{$0.contains("첫 번째.ttf")})
        let importer=try FontImporter(workspace:root.appendingPathComponent("Restored"))
        let restored=try await importer.read([output]);XCTAssertEqual(restored.count,try FontReader.faces(files[0]).count)
    }
    func testCancellationAfterCompressionStartsPreservesExistingBackup()async throws{
        let files=try fonts(),output=root.appendingPathComponent("keep.zip"),original=Data("keep existing backup".utf8),history=ProgressHistory()
        try original.write(to:output)
        let task=Task{try await FontBackup.create(files:files,destination:output,progress:{history.append($0)})}
        history.cancellation{task.cancel()};defer{history.cancellation(nil)}
        do{try await task.value;XCTFail("Expected in-progress cancellation")}catch is CancellationError{}
        XCTAssertTrue(history.values.contains{$0.phase == .archiving && $0.completed==1})
        XCTAssertFalse(history.values.contains{$0.phase == .complete})
        XCTAssertEqual(try Data(contentsOf:output),original)
    }
    func testPercentDoesNotClaimCompletionBeforeCommit(){
        XCTAssertNil(WorkProgress(.preparing,total:10).percent)
        XCTAssertEqual(WorkProgress(.archiving,completed:58,total:100).percent,58)
        XCTAssertEqual(WorkProgress(.archiving,completed:100,total:100).percent,99)
        XCTAssertEqual(WorkProgress(.verifying,completed:100,total:100).percent,99)
        XCTAssertEqual(WorkProgress(.complete,completed:100,total:100).percent,100)
        XCTAssertEqual(WorkProgress(.archiving,completed:-3,total:10).percent,0)
    }
}
