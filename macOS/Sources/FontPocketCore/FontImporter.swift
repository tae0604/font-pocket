import Foundation

public enum ArchivePolicy {
    public static func safePath(_ path: String) -> Bool {
        !path.isEmpty && !path.hasPrefix("/") && !path.contains("\\") && !path.contains(":") &&
        !path.contains("\0") && !path.contains("\n") && !path.contains("\r") &&
        !path.split(separator: "/", omittingEmptySubsequences: false).contains("..")
    }
    // Validate ZIP central AND local names before invoking macOS unzip. Reject symlinks,
    // path-override extra fields, ZIP64 and oversized archives rather than guessing.
    public static func validate(_ data: Data) throws {
        func fail() -> PocketError { PocketError("Unsafe or unsupported ZIP archive.") }
        func u16(_ p: Int) throws -> Int {
            guard p >= 0 && p + 2 <= data.count else { throw fail() }
            return Int(data[p]) | Int(data[p+1]) << 8
        }
        func u32(_ p: Int) throws -> Int {
            guard p >= 0 && p + 4 <= data.count else { throw fail() }
            return Int(data[p]) | Int(data[p+1]) << 8 | Int(data[p+2]) << 16 | Int(data[p+3]) << 24
        }
        guard data.count >= 22 && data.count <= 200 * 1024 * 1024 else { throw fail() }
        let lower = max(0, data.count - 65557)
        guard let end = stride(from: data.count - 22, through: lower, by: -1).first(where: {
            (try? u32($0)) == 0x06054b50 &&
            $0 + 22 + ((try? u16($0+20)) ?? -1) == data.count
        }) else { throw fail() }
        let count = try u16(end+10), directorySize = try u32(end+12), offset = try u32(end+16)
        guard try u16(end+4) == 0, try u16(end+6) == 0, try u16(end+8) == count,
              count > 0 && count <= 2048, offset + directorySize == end else { throw fail() }
        var position = offset, total = 0, seen = Set<String>()
        for _ in 0..<count {
            guard try u32(position) == 0x02014b50 else { throw fail() }
            let flags = try u16(position+8), method = try u16(position+10), compressed = try u32(position+20), bytes = try u32(position+24)
            let nameLength = try u16(position+28), extraLength = try u16(position+30), commentLength = try u16(position+32)
            let attributes = try u32(position+38), local = try u32(position+42)
            let next = position+46+nameLength+extraLength+commentLength
            guard next <= end, flags & 1 == 0, method == 0 || method == 8, bytes <= 150*1024*1024,
                  compressed > 0 || bytes == 0, bytes <= max(compressed,1)*500,
                  (attributes >> 16) & 0xf000 != 0xa000 else { throw fail() }
            guard let name = String(data: data.subdata(in: position+46..<position+46+nameLength), encoding: .utf8), safePath(name),
                  seen.insert(name.precomposedStringWithCanonicalMapping.lowercased()).inserted else { throw fail() }
            total += bytes; guard total <= 512*1024*1024 else { throw fail() }
            var extra = position+46+nameLength
            let extraEnd = extra+extraLength
            while extra < extraEnd {
                let tag = try u16(extra), length = try u16(extra+2)
                guard extra+4+length <= extraEnd, tag != 0x0001, tag != 0x7075 else { throw fail() }
                extra += 4+length
            }
            guard local >= 0, local+30 <= offset, try u32(local) == 0x04034b50,
                  try u16(local+8) == method, try u16(local+6) == flags else { throw fail() }
            let localName = try u16(local+26), localExtra = try u16(local+28)
            guard localName == nameLength, local+30+localName+localExtra+compressed <= offset,
                  data.subdata(in: local+30..<local+30+localName) == data.subdata(in: position+46..<position+46+nameLength) else { throw fail() }
            // Do not allow local-only filename overrides either.
            var cursor = local+30+localName
            while cursor < local+30+localName+localExtra {
                let tag = try u16(cursor), length = try u16(cursor+2)
                guard cursor+4+length <= local+30+localName+localExtra, tag != 0x7075, tag != 0x0001 else { throw fail() }
                cursor += 4+length
            }
            position = next
        }
        guard position == end else { throw fail() }
    }
}

public actor FontImporter {
    public static let supported = FontReader.extensions.union(["woff","woff2","sfnt"])
    public let workspace: URL
    private let converter: (@Sendable (URL,URL) async throws -> URL)?
    public init(workspace: URL, converter: (@Sendable (URL,URL) async throws -> URL)? = nil) throws {
        self.workspace = workspace
        self.converter = converter
        try FileManager.default.createDirectory(at: workspace, withIntermediateDirectories: true)
    }
    public func read(_ urls: [URL]) async throws -> [FontFace] {
        var faces: [FontFace] = [], seen = Set<String>()
        for url in urls {
            try Task.checkCancellation()
            let scoped = url.startAccessingSecurityScopedResource(); defer { if scoped { url.stopAccessingSecurityScopedResource() } }
            for file in try await expand(url) {
                try Task.checkCancellation()
                let file = try await converter?(file,workspace) ?? file
                let digest = try FontReader.hash(file)
                if !seen.insert(digest).inserted { continue }
                // Own a copy so originals can be moved without breaking the installation queue.
                let copy = workspace.appendingPathComponent(digest + "." + file.pathExtension.lowercased())
                if !FileManager.default.fileExists(atPath: copy.path) { try FileManager.default.copyItem(at: file, to: copy) }
                faces += try FontReader.faces(copy, managedKey: nil)
            }
        }
        return faces
    }
    private func expand(_ url: URL) async throws -> [URL] {
        let values = try url.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey])
        guard values.isSymbolicLink != true else { throw PocketError("Symbolic links are not imported.") }
        if values.isDirectory == true { return try walk(url) }
        if url.pathExtension.lowercased() == "zip" {
            let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
            guard size <= 200*1024*1024 else { throw PocketError("ZIP files must be under 200 MB.") }
            try ArchivePolicy.validate(Data(contentsOf: url, options: .mappedIfSafe))
            let target = workspace.appendingPathComponent(UUID().uuidString, isDirectory: true)
            try FileManager.default.createDirectory(at: target, withIntermediateDirectories: true)
            defer { try? FileManager.default.removeItem(at: target) }
            _ = try await ProcessRunner.run("/usr/bin/unzip", ["-qq", "-n", url.path, "-d", target.path])
            let files = try walk(target)
            // Retain extracted files inside the workspace before removing the extraction directory.
            var copies: [URL] = []
            for file in files {
                try Task.checkCancellation()
                let destination = workspace.appendingPathComponent(UUID().uuidString + "." + file.pathExtension)
                try FileManager.default.copyItem(at: file, to: destination); copies.append(destination)
            }
            guard !copies.isEmpty else { throw PocketError("No TTF, OTF, TTC or OTC fonts were found in this ZIP.") }
            return copies
        }
        guard Self.supported.contains(url.pathExtension.lowercased()) else {
            throw PocketError("Use TTF, OTF, TTC, OTC, ZIP files or folders. Web fonts need desktop-font downloads.")
        }
        return [url]
    }
    private func walk(_ directory: URL) throws -> [URL] {
        guard let enumerator = FileManager.default.enumerator(at: directory, includingPropertiesForKeys: [.isSymbolicLinkKey, .isRegularFileKey], options: [.skipsHiddenFiles, .skipsPackageDescendants]) else { return [] }
        var result: [URL] = []
        for case let file as URL in enumerator {
            try Task.checkCancellation()
            let values = try file.resourceValues(forKeys: [.isSymbolicLinkKey, .isRegularFileKey])
            if values.isSymbolicLink == true { enumerator.skipDescendants(); continue }
            if file.pathComponents.contains("__MACOSX") { enumerator.skipDescendants(); continue }
            if values.isRegularFile == true && Self.supported.contains(file.pathExtension.lowercased()) { result.append(file) }
            guard result.count <= 2048 else { throw PocketError("Import up to 2,048 font files at a time.") }
        }
        return result.sorted { $0.path < $1.path }
    }
}

public enum FontBackup {
    public static func create(files: [URL], destination: URL) async throws {
        guard !files.isEmpty else { throw PocketError("Select fonts to back up.") }
        let fm = FileManager.default, staging = fm.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let folder = staging.appendingPathComponent("Font Pocket Backup", isDirectory: true)
        let temporaryZIP = destination.deletingLastPathComponent().appendingPathComponent(".FontPocket-\(UUID().uuidString).zip")
        try fm.createDirectory(at: folder, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: staging); try? fm.removeItem(at: temporaryZIP) }
        for file in Set(files) {
            try Task.checkCancellation()
            _ = try FontReader.faces(file)
            let hash = try FontReader.hash(file)
            let name = hash.prefix(12) + "-" + file.lastPathComponent
            try fm.copyItem(at: file, to: folder.appendingPathComponent(String(name)))
        }
        _ = try await ProcessRunner.run("/usr/bin/ditto", ["-c", "-k", "--keepParent", folder.path, temporaryZIP.path])
        try Task.checkCancellation()
        // Same directory / filesystem: replacement is atomic and preserves existing backups on cancellation.
        if fm.fileExists(atPath: destination.path) { _ = try fm.replaceItemAt(destination, withItemAt: temporaryZIP) }
        else { try fm.moveItem(at: temporaryZIP, to: destination) }
    }
}
