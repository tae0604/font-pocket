import Foundation

private final class CancellationFlag: @unchecked Sendable {
    private let lock = NSLock()
    private var value = false
    func cancel() { lock.lock(); value = true; lock.unlock() }
    var cancelled: Bool { lock.lock(); defer { lock.unlock() }; return value }
}

public enum ProcessRunner {
    public static func run(_ executable: String, _ arguments: [String], directory: URL? = nil, onLine: (@Sendable (String) -> Void)? = nil) async throws -> String {
        let flag = CancellationFlag()
        let workingDirectory=directory
        return try await withTaskCancellationHandler(operation: {
            try await withCheckedThrowingContinuation { continuation in
                DispatchQueue.global(qos: .userInitiated).async {
                    do {
                        if flag.cancelled { throw CancellationError() }
                        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
                        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
                        defer { try? FileManager.default.removeItem(at: directory) }
                        let output = directory.appendingPathComponent("output")
                        FileManager.default.createFile(atPath: output.path, contents: nil)
                        let handle = try FileHandle(forWritingTo: output); defer { try? handle.close() }
                        let process = Process()
                        process.executableURL = URL(fileURLWithPath: executable); process.arguments = arguments
                        process.currentDirectoryURL = workingDirectory
                        process.standardOutput = handle; process.standardError = handle
                        let liveReader = try FileHandle(forReadingFrom: output);defer{try? liveReader.close()}
                        var pending=Data()
                        @discardableResult func drain() throws -> Bool {
                            guard onLine != nil,let bytes=try liveReader.read(upToCount:64*1024),!bytes.isEmpty else{return false}
                            pending.append(bytes)
                            while let newline=pending.firstIndex(of:10){
                                onLine?(String(decoding:pending.prefix(upTo:newline),as:UTF8.self))
                                pending.removeSubrange(...newline)
                            }
                            return true
                        }
                        try process.run()
                        var cancelled = false
                        while process.isRunning {
                            try drain()
                            if flag.cancelled && !cancelled { cancelled = true; process.terminate() }
                            Thread.sleep(forTimeInterval: 0.05)
                        }
                        process.waitUntilExit()
                        while try drain() {}
                        if !pending.isEmpty{onLine?(String(decoding:pending,as:UTF8.self))}
                        if cancelled || flag.cancelled { throw CancellationError() }
                        let reader = try FileHandle(forReadingFrom: output); defer { try? reader.close() }
                        let text = String(decoding: try reader.read(upToCount: 1024 * 1024) ?? Data(), as: UTF8.self)
                        guard process.terminationStatus == 0 else { throw PocketError(String(text.prefix(1000))) }
                        continuation.resume(returning: text)
                    } catch { continuation.resume(throwing: error) }
                }
            }
        }, onCancel: { flag.cancel() })
    }
}
