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
    public var title: String { family + " " + style }
    public var canManage: Bool { managedKey != nil }
    public var canAdopt: Bool { state == .external && file.standardizedFileURL.deletingLastPathComponent() == LibraryPaths.live.fonts.standardizedFileURL && file.resolvingSymlinksInPath() == file.standardizedFileURL }
    public init(id: String, file: URL, family: String, style: String, postScript: String, format: String,
                bytes: Int64, state: FontState, managedKey: String? = nil,version:String="",weight:Double=0,variable:Bool=false,scripts:Set<String>=[]) {
        self.id = id; self.file = file; self.family = family; self.style = style; self.postScript = postScript
        self.format = format; self.bytes = bytes; self.state = state; self.managedKey = managedKey
        self.version=version;weightValue=weight;self.variable=variable;self.scripts=scripts
    }
    public func previewFont(size: Double) -> CTFont {
        let descriptors = CTFontManagerCreateFontDescriptorsFromURL(file as CFURL) as? [CTFontDescriptor] ?? []
        let descriptor = descriptors.first { (CTFontDescriptorCopyAttribute($0, kCTFontNameAttribute) as? String) == postScript }
        if let descriptor { return CTFontCreateWithFontDescriptor(descriptor, size, nil) }
        return CTFontCreateWithName(postScript as CFString, size, nil)
    }
}

public enum FontReader {
    public static let extensions: Set<String> = ["ttf", "otf", "ttc", "otc"]
    public static func hash(_ file: URL) throws -> String {
        let handle = try FileHandle(forReadingFrom: file); defer { try? handle.close() }
        var digest = SHA256()
        while let data = try handle.read(upToCount: 1024 * 1024), !data.isEmpty {
            try Task.checkCancellation(); digest.update(data: data)
        }
        return digest.finalize().map { String(format: "%02x", $0) }.joined()
    }
    public static func faces(_ file: URL, state: FontState = .available, managedKey: String? = nil) throws -> [FontFace] {
        let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey, .fileSizeKey])
        guard values.isRegularFile == true, values.isSymbolicLink != true,
              let bytes = values.fileSize, bytes >= 12, bytes <= 512 * 1024 * 1024 else {
            throw PocketError("Invalid font file: \(file.lastPathComponent)")
        }
        let handle = try FileHandle(forReadingFrom: file); defer { try? handle.close() }
        let magic = try handle.read(upToCount: 4) ?? Data()
        guard [Data([0,1,0,0]), Data("OTTO".utf8), Data("ttcf".utf8), Data("true".utf8)].contains(magic),
              let descriptors = CTFontManagerCreateFontDescriptorsFromURL(file as CFURL) as? [CTFontDescriptor], !descriptors.isEmpty else {
            throw PocketError("Cannot read this font: \(file.lastPathComponent)")
        }
        return descriptors.map { descriptor in
            let font = CTFontCreateWithFontDescriptor(descriptor, 16, nil)
            let name = CTFontCopyPostScriptName(font) as String
            let traits=CTFontCopyTraits(font) as NSDictionary
            let charset=CTFontCopyCharacterSet(font)
            let scripts=Set(FontAnalysis.ranges.filter{entry in entry.1.contains{CFCharacterSetIsLongCharacterMember(charset,$0)}}.map(\.0))
            return FontFace(id: (managedKey ?? file.standardizedFileURL.path) + "|" + name, file: file,
                            family: CTFontCopyFamilyName(font) as String, style: CTFontCopyName(font, kCTFontStyleNameKey) as String? ?? "Regular",
                            postScript: name, format: file.pathExtension.uppercased(), bytes: Int64(bytes), state: state, managedKey: managedKey,
                            version:CTFontCopyName(font,kCTFontVersionNameKey) as String? ?? "",weight:traits[kCTFontWeightTrait] as? Double ?? 0,
                            variable:!(CTFontCopyVariationAxes(font) as? [Any] ?? []).isEmpty,scripts:scripts)
        }
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
