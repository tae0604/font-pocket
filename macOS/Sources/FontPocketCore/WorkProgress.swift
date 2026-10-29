import Foundation

public struct WorkProgress: Sendable, Equatable {
    public enum Phase: String, Sendable { case preparing, archiving, verifying, installing, complete }
    public let phase: Phase
    public let completed: Int
    public let total: Int
    public init(_ phase: Phase, completed: Int = 0, total: Int = 0) {
        self.phase = phase; self.total = max(0,total); self.completed = min(max(0,completed),max(0,total))
    }
    public var fraction: Double? {
        if phase == .complete { return 1 }
        if phase == .verifying, total > 0 { return 0.99 }
        guard total > 0, phase == .archiving || phase == .installing else { return nil }
        return min(0.99, Double(completed) / Double(total))
    }
    public var percent: Int? {
        if phase == .complete{return 100}
        if phase == .verifying,total>0{return 99}
        guard fraction != nil else{return nil}
        return min(99,completed*100/total)
    }
    public var phaseOrder: Int {
        switch phase{case .preparing:return 0;case .archiving,.installing:return 1;case .verifying:return 2;case .complete:return 3}
    }
}

public typealias ProgressHandler = @Sendable (WorkProgress) -> Void

/// zip emits its "adding ... (deflated/stored ...)" line AFTER each file is
/// compressed. Count actual completed files, not elapsed time or ZIP output size.
private final class ArchiveProgress: @unchecked Sendable {
    private let lock = NSLock()
    private let paths: Set<String>
    private var completed = Set<String>()
    private let report: ProgressHandler?
    init(paths: Set<String>, report: ProgressHandler?) { self.paths = paths; self.report = report }
    func consume(_ line: String) {
        guard let start=line.range(of:"adding: "),
              let end=line.range(of:" \\((?:deflated|stored) [0-9]+%\\)\\s*$",options:.regularExpression),end.lowerBound>=start.upperBound else{return}
        let path=String(line[start.upperBound..<end.lowerBound])
        lock.lock()
        let inserted=paths.contains(path) && completed.insert(path).inserted
        let count=completed.count
        lock.unlock()
        if inserted { report?(WorkProgress(.archiving,completed:count,total:paths.count)) }
    }
}

public enum BackupArchive {
    /// Write a temporary ZIP and only replace an existing backup after the
    /// process succeeds and the resulting archive passes the restore policy.
    public static func create(folder: URL, destination: URL, progress: ProgressHandler? = nil) async throws {
        let fm=FileManager.default
        let temporary=destination.deletingLastPathComponent().appendingPathComponent(".FontPocket-\(UUID().uuidString).zip")
        defer{try? fm.removeItem(at:temporary)}
        guard let enumerator=fm.enumerator(at:folder,includingPropertiesForKeys:[.isRegularFileKey,.isSymbolicLinkKey]) else{throw PocketError("Cannot read backup staging folder")}
        var paths=Set<String>()
        let prefix=folder.deletingLastPathComponent().path+"/"
        for case let file as URL in enumerator {
            try Task.checkCancellation()
            let values=try file.resourceValues(forKeys:[.isRegularFileKey,.isSymbolicLinkKey])
            guard values.isSymbolicLink != true else{throw PocketError("Symbolic links cannot be backed up")}
            if values.isRegularFile == true {
                let name=String(file.path.dropFirst(prefix.count))
                guard ArchivePolicy.safePath(name) else{throw PocketError("Unsupported backup filename")}
                paths.insert(name)
            }
        }
        guard !paths.isEmpty else{throw PocketError("No files to back up")}
        let reporter=ArchiveProgress(paths:paths,report:progress)
        progress?(WorkProgress(.archiving,total:paths.count))
        _ = try await ProcessRunner.run("/usr/bin/zip",["-r","-UN=UTF8",temporary.path,folder.lastPathComponent],
            directory:folder.deletingLastPathComponent(),onLine:{reporter.consume($0)})
        try Task.checkCancellation()
        progress?(WorkProgress(.verifying,completed:paths.count,total:paths.count))
        try ArchivePolicy.validate(Data(contentsOf:temporary,options:.mappedIfSafe))
        try Task.checkCancellation()
        if fm.fileExists(atPath:destination.path){_ = try fm.replaceItemAt(destination,withItemAt:temporary)}else{try fm.moveItem(at:temporary,to:destination)}
        progress?(WorkProgress(.complete,completed:paths.count,total:paths.count))
    }
}
