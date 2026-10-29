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
            case "static","고정": return !font.isVariable
            case "favorite","즐겨찾기": return annotation.favorite
            default: return (font.title + " " + annotation.note + " " + annotation.tags.joined(separator: " ")).localizedCaseInsensitiveContains(term)
            }
        }
    }
}
public struct PersonalDocument: Codable, Equatable, Sendable {
    public var annotations: [String: FontAnnotation] = [:]
    public var collections: [FontCollection] = []
    public init() {}
    public mutating func remap(_ old:String,to new:String){
        guard old != new else{return}
        if let entry=annotations.removeValue(forKey:old),annotations[new]==nil{annotations[new]=entry}
        for index in collections.indices{if collections[index].members.remove(old) != nil{collections[index].members.insert(new)}}
    }
    /// Apply only fields actually edited in this window. Keep changes made by a
    /// restore or another window, and reject overlapping edits without writing.
    public func applyingEdits(_ edited: PersonalDocument, baseline: PersonalDocument) throws -> PersonalDocument {
        func merge<T: Equatable>(_ original: T, _ value: T, _ current: T) throws -> T {
            guard value != original else { return current }
            guard current == original || current == value else { throw LibraryEditConflict() }
            return value
        }
        var result = self
        for key in Set(baseline.annotations.keys).union(edited.annotations.keys) {
            guard baseline.annotations[key] != edited.annotations[key] else { continue }
            guard let value = edited.annotations[key] else {
                guard self.annotations[key] == nil || self.annotations[key] == baseline.annotations[key] else { throw LibraryEditConflict() }
                result.annotations.removeValue(forKey: key); continue
            }
            let old = baseline.annotations[key] ?? FontAnnotation()
            var current = self.annotations[key] ?? FontAnnotation()
            current.note = try merge(old.note, value.note, current.note)
            current.tags = try merge(old.tags, value.tags, current.tags)
            current.favorite = try merge(old.favorite, value.favorite, current.favorite)
            result.annotations[key] = current
        }
        for old in baseline.collections where !edited.collections.contains(where: { $0.id == old.id }) {
            if let current = result.collections.first(where: { $0.id == old.id }), current != old { throw LibraryEditConflict() }
            result.collections.removeAll { $0.id == old.id }
        }
        for value in edited.collections {
            guard let old = baseline.collections.first(where: { $0.id == value.id }) else {
                if let current = result.collections.first(where: { $0.id == value.id }) {
                    guard current == value else { throw LibraryEditConflict() }
                } else { result.collections.append(value) }
                continue
            }
            guard value != old else { continue }
            guard let index = result.collections.firstIndex(where: { $0.id == value.id }) else { throw LibraryEditConflict() }
            result.collections[index].name = try merge(old.name, value.name, result.collections[index].name)
            result.collections[index].rule = try merge(old.rule, value.rule, result.collections[index].rule)
            result.collections[index].members = try merge(old.members, value.members, result.collections[index].members)
        }
        return result
    }
}
public struct LibraryEditConflict: LocalizedError {
    public init() {}
    public var errorDescription: String? { "The same library field changed in another operation. Your edits were not overwritten." }
}
public actor PersonalLibrary {
    public let root: URL
    public init(root: URL = LibraryPaths.live.root) throws { self.root = root; try FileManager.default.createDirectory(at: root,withIntermediateDirectories: true) }
    public func read() throws -> PersonalDocument {
        let url = root.appendingPathComponent("personal.json")
        return FileManager.default.fileExists(atPath: url.path) ? try JSONDecoder().decode(PersonalDocument.self,from: Data(contentsOf: url)) : PersonalDocument()
    }
    public func save(_ document: PersonalDocument) throws { try JSONEncoder().encode(document).write(to: root.appendingPathComponent("personal.json"),options:.atomic) }
    public func saveChanges(_ edited: PersonalDocument, baseline: PersonalDocument) throws -> PersonalDocument {
        let merged = try read().applyingEdits(edited, baseline: baseline)
        try save(merged)
        return merged
    }
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
        let values = try url.resourceValues(forKeys:[.isRegularFileKey,.isSymbolicLinkKey])
        guard values.isRegularFile == true,values.isSymbolicLink != true else{throw PocketError("Invalid stored version file")}
        guard try FontReader.hash(url) == version.hash else { throw PocketError("Stored version integrity check failed") }
        return url
    }
    @discardableResult public func capture(_ url: URL) throws -> [FontVersion] {
        let faces = try FontReader.faces(url), hash = try FontReader.hash(url)
        var records = try read()
        let existing=records.filter{$0.hash == hash}
        let filename = existing.first?.filename ?? hash+"."+url.pathExtension.lowercased(), destination = root.appendingPathComponent(filename)
        guard filename==hash+"."+URL(fileURLWithPath:filename).pathExtension,FontReader.extensions.contains(URL(fileURLWithPath:filename).pathExtension.lowercased()) else{throw PocketError("Invalid stored version filename")}
        if let version=existing.first,(try? file(version)) != nil{return existing}
        // An existing index entry alone does not prove its original still exists.
        let temporary=root.appendingPathComponent(UUID().uuidString+".tmp")
        defer{try? FileManager.default.removeItem(at:temporary)}
        try FileManager.default.copyItem(at:url,to:temporary)
        guard try FontReader.hash(temporary)==hash else{throw PocketError("The version changed while copying")}
        try Task.checkCancellation()
        if FileManager.default.fileExists(atPath:destination.path){_ = try FileManager.default.replaceItemAt(destination,withItemAt:temporary)}else{try FileManager.default.moveItem(at:temporary,to:destination)}
        if !existing.isEmpty{return existing}
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
    public static func deletionCandidates(_ groups:[DuplicateGroup],selected:Set<String>,keep:[String:String])->[FontFace]{
        let selectedGroups=groups.filter{selected.contains($0.id)}
        let protected=Set(selectedGroups.compactMap{group in group.fonts.first{$0.id==keep[group.id]}?.file})
        var files=Set<URL>()
        return selectedGroups.flatMap{group in
            guard group.fonts.contains(where:{$0.id==keep[group.id]}) else{return [FontFace]()}
            return group.fonts.filter{face in face.canManage && !protected.contains(face.file) && (group.exact || !["TTC","OTC"].contains(face.format)) && files.insert(face.file).inserted}
        }
    }
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
