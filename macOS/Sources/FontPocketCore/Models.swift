import Foundation
import CoreText
import CryptoKit

public struct PocketError: LocalizedError {
    public let message: String
    public init(_ message: String) { self.message = message }
    public var errorDescription: String? { message }
}

public enum FontState: String, Codable, Sendable { case available, enabled, disabled, external }

public struct FontFace: Identifiable, Hashable, Sendable {
    public let id: String
    public let file: URL
    public let family: String
    public let style: String
    public let postScript: String
    public let format: String
    public let bytes: Int64
    public let state: FontState
    public let managedKey: String?
    public let version:String
    public let weightValue:Double
    public let variable:Bool
    public let scripts:Set<String>
    var renderSource: FontRenderSource?
    public var title: String { family + " " + style }
    public var canManage: Bool { managedKey != nil }
    public var canAdopt: Bool { state == .external && file.standardizedFileURL.deletingLastPathComponent() == LibraryPaths.live.fonts.standardizedFileURL && file.resolvingSymlinksInPath() == file.standardizedFileURL }
    public init(id: String, file: URL, family: String, style: String, postScript: String, format: String,
                bytes: Int64, state: FontState, managedKey: String? = nil,version:String="",weight:Double=0,variable:Bool=false,scripts: Set<String> = []) {
        self.id = id; self.file = file; self.family = family; self.style = style; self.postScript = postScript
        self.format = format; self.bytes = bytes; self.state = state; self.managedKey = managedKey
        self.version=version;weightValue=weight;self.variable=variable;self.scripts=scripts
    }
    public func previewFont(size: Double) -> CTFont {
        if let renderSource { return renderSource.font(size: size) }
        let descriptors = CTFontManagerCreateFontDescriptorsFromURL(file as CFURL) as? [CTFontDescriptor] ?? []
        let descriptor = descriptors.first { (CTFontDescriptorCopyAttribute($0, kCTFontNameAttribute) as? String) == postScript }
        if let descriptor { return CTFontCreateWithFontDescriptor(descriptor, size, nil) }
        return CTFontCreateWithName(postScript as CFString, size, nil)
    }
}

public enum FontReader {
    public static let extensions: Set<String> = ["ttf", "otf", "ttc", "otc"]
    public static func hash(_ file: URL, checkCancellation: Bool = true) throws -> String {
        let handle = try FileHandle(forReadingFrom: file); defer { try? handle.close() }
        var digest = SHA256()
        while let data = try handle.read(upToCount: 1024 * 1024), !data.isEmpty {
            if checkCancellation { try Task.checkCancellation() }; digest.update(data: data)
        }
        return digest.finalize().map { String(format: "%02x", $0) }.joined()
    }
    public static func faces(_ file: URL, state: FontState = .available, managedKey: String? = nil, checkCancellation: Bool = true) throws -> [FontFace] {
        if checkCancellation { try Task.checkCancellation() }
        // URL resource values are cached by Foundation and can survive an atomic
        // replacement of the file. Fresh attributes are also the cache key.
        let attributes = try FileManager.default.attributesOfItem(atPath: file.path)
        guard attributes[.type] as? FileAttributeType == .typeRegular,
              let bytes = (attributes[.size] as? NSNumber)?.intValue, bytes >= 12, bytes <= 512 * 1024 * 1024 else {
            throw PocketError("Invalid font file: \(file.lastPathComponent)")
        }
        let identity = FontMetadataCache.identity(file, attributes: attributes)
        if let cached = FontMetadataCache.files.object(forKey: identity as NSString) {
            return cached.faces.map { identified($0, state: state, key: managedKey) }
        }
        let handle = try FileHandle(forReadingFrom: file); defer { try? handle.close() }
        let magic = try handle.read(upToCount: 4) ?? Data()
        guard [Data([0,1,0,0]), Data("OTTO".utf8), Data("ttcf".utf8), Data("true".utf8)].contains(magic),
              let descriptors = CTFontManagerCreateFontDescriptorsFromURL(file as CFURL) as? [CTFontDescriptor], !descriptors.isEmpty else {
            throw PocketError("Cannot read this font: \(file.lastPathComponent)")
        }
        let faces = try descriptors.map { descriptor in
            if checkCancellation { try Task.checkCancellation() }
            let font = CTFontCreateWithFontDescriptor(descriptor, 16, nil)
            let name = CTFontCopyPostScriptName(font) as String
            let traits=CTFontCopyTraits(font) as NSDictionary
            let charset=CTFontCopyCharacterSet(font)
            let scripts=Set(FontAnalysis.ranges.filter{entry in entry.1.contains{CFCharacterSetIsLongCharacterMember(charset,$0)}}.map(\.0))
            var face = FontFace(id: file.standardizedFileURL.path + "|" + name, file: file,
                            family: CTFontCopyFamilyName(font) as String, style: CTFontCopyName(font, kCTFontStyleNameKey) as String? ?? "Regular",
                            postScript: name, format: file.pathExtension.uppercased(), bytes: Int64(bytes), state: .available,
                            version:CTFontCopyName(font,kCTFontVersionNameKey) as String? ?? "",weight:traits[kCTFontWeightTrait] as? Double ?? 0,
                            variable:!(CTFontCopyVariationAxes(font) as? [Any] ?? []).isEmpty,scripts:scripts)
            face.renderSource = FontRenderSource(identity: identity + "|" + name, descriptor: descriptor, font: font)
            return face
        }
        FontMetadataCache.files.setObject(.init(faces), forKey: identity as NSString, cost: bytes)
        return faces.map { identified($0, state: state, key: managedKey) }
    }
    private static func identified(_ face: FontFace, state: FontState, key: String?) -> FontFace {
        var copy = FontFace(id: (key ?? face.file.standardizedFileURL.path) + "|" + face.postScript,
            file: face.file, family: face.family, style: face.style, postScript: face.postScript,
            format: face.format, bytes: face.bytes, state: state, managedKey: key,
            version: face.version, weight: face.weight, variable: face.isVariable, scripts: face.scripts)
        copy.renderSource = face.renderSource
        return copy
    }
}

public struct DownloadCandidate: Identifiable, Hashable, Sendable {
    public var id: String { url.absoluteString }
    public let url: URL
    public let name: String
    public init(url: URL, name: String) { self.url = url; self.name = name }
}

public struct LibraryPaths: Sendable {
    public let root: URL
    public let fonts: URL
    public var disabled: URL { root.appendingPathComponent("Disabled", isDirectory: true) }
    public var catalog: URL { root.appendingPathComponent("catalog.json") }
    public init(root: URL, fonts: URL) { self.root = root; self.fonts = fonts }
    public static var live: LibraryPaths {
        let home = FileManager.default.homeDirectoryForCurrentUser
        return LibraryPaths(root: home.appendingPathComponent("Library/Application Support/Font Pocket", isDirectory: true),
                            fonts: home.appendingPathComponent("Library/Fonts", isDirectory: true))
    }
}
