import Foundation
import CoreText

public struct FontAnnotation: Codable, Equatable, Sendable {
    public var note = ""
    public var tags: [String] = []
    public var favorite = false
    public init() {}
}
public struct FontCollection: Identifiable, Codable, Equatable, Sendable {
    public var id = UUID()
    public var name: String
    public var rule: String
    public var members: Set<String>
    public init(name: String, rule: String = "", members: Set<String> = []) { self.name = name; self.rule = rule; self.members = members }
    public func contains(_ font: FontFace, annotation: FontAnnotation) -> Bool {
        if rule.trimmingCharacters(in: .whitespaces).isEmpty { return members.contains(font.id) }
        return rule.split(separator: ";").allSatisfy { part in
            let term = part.trimmingCharacters(in: .whitespaces).lowercased()
            switch term {
            case "ttf","otf","ttc","otc": return font.format.lowercased() == term
            case "enabled","활성": return font.state == .enabled || font.state == .external
            case "disabled","비활성": return font.state == .disabled
            case "variable","가변": return font.isVariable
            default: return (font.title + " " + annotation.note + " " + annotation.tags.joined(separator: " ")).localizedCaseInsensitiveContains(term)
            }
        }
    }
}
public struct PersonalDocument: Codable, Equatable, Sendable {
    public var annotations: [String: FontAnnotation] = [:]
    public var collections: [FontCollection] = []
    public init() {}
}
public actor PersonalLibrary {
    public let root: URL
    public init(root: URL = LibraryPaths.live.root) throws { self.root = root; try FileManager.default.createDirectory(at: root,withIntermediateDirectories: true) }
    public func read() throws -> PersonalDocument {
        let url = root.appendingPathComponent("personal.json")
        return FileManager.default.fileExists(atPath: url.path) ? try JSONDecoder().decode(PersonalDocument.self,from: Data(contentsOf: url)) : PersonalDocument()
    }
    public func save(_ document: PersonalDocument) throws { try JSONEncoder().encode(document).write(to: root.appendingPathComponent("personal.json"),options:.atomic) }
}

public struct FontVersion: Identifiable, Codable, Sendable {
    public var id: String { hash + "|" + postScript }
    public let hash: String
    public let filename: String
    public let family: String
    public let style: String
    public let postScript: String
    public let version: String
    public let added: Date
}
public actor VersionVault {
    public let root: URL
    public init(root: URL = LibraryPaths.live.root.appendingPathComponent("Versions")) throws {
        self.root = root;try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
    }
    public func read() throws -> [FontVersion] {
        let url = root.appendingPathComponent("versions.json")
        return FileManager.default.fileExists(atPath:url.path) ? try JSONDecoder().decode([FontVersion].self,from:Data(contentsOf:url)) : []
    }
    public func file(_ version: FontVersion) throws -> URL {
        guard version.filename == version.hash+"."+URL(fileURLWithPath:version.filename).pathExtension,
              version.hash.count == 64,version.hash.allSatisfy(\.isHexDigit) else { throw PocketError("Invalid version record") }
        let url = root.appendingPathComponent(version.filename)
        guard try FontReader.hash(url) == version.hash else { throw PocketError("Stored version integrity check failed") }
        return url
    }
    @discardableResult public func capture(_ url: URL) throws -> [FontVersion] {
        let hash = try FontReader.hash(url), faces = try FontReader.faces(url)
        var records = try read()
        if records.contains(where:{$0.hash == hash}) { return records.filter{$0.hash == hash} }
        let filename = hash+"."+url.pathExtension.lowercased(), destination = root.appendingPathComponent(filename)
        if !FileManager.default.fileExists(atPath:destination.path) { try FileManager.default.copyItem(at:url,to:destination) }
        let added = faces.map { face in FontVersion(hash:hash,filename:filename,family:face.family,style:face.style,postScript:face.postScript,
            version:CTVersion(face),added:Date()) }
        records += added
        try JSONEncoder().encode(records).write(to:root.appendingPathComponent("versions.json"),options:.atomic)
        return added
    }
    private func CTVersion(_ face: FontFace) -> String { CTFontCopyName(face.previewFont(size:12),kCTFontVersionNameKey) as String? ?? "—" }
}

public struct DuplicateGroup: Identifiable, Sendable {
    public let id: String
    public let exact: Bool
    public let fonts: [FontFace]
}
public enum DuplicateFinder {
    public static func find(_ fonts: [FontFace]) throws -> [DuplicateGroup] {
        var hashes: [URL:String] = [:]
        for file in Set(fonts.map(\.file)) { try Task.checkCancellation(); hashes[file] = try FontReader.hash(file) }
        var result: [DuplicateGroup] = []
        for (hash, group) in Dictionary(grouping:fonts,by:{hashes[$0.file] ?? ""}) where Set(group.map(\.file)).count > 1 {
            result.append(DuplicateGroup(id:hash,exact:true,fonts:group))
        }
        for (key, group) in Dictionary(grouping:fonts,by:{$0.postScript+"|"+String($0.isVariable)}) where Set(group.compactMap{hashes[$0.file]}).count > 1 {
            result.append(DuplicateGroup(id:key,exact:false,fonts:group))
        }
        return result.sorted{$0.id < $1.id}
    }
}
