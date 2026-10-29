import Foundation
import CoreText

/// Immutable Core Text descriptors may be shared between threads. NSCache keeps
/// size-specific CTFonts bounded and thread-safe, avoiding file I/O per glyph.
final class FontRenderSource: @unchecked Sendable, Hashable {
    let identity: String
    let descriptor: CTFontDescriptor
    private final class FontBox: NSObject {
        let font: CTFont
        init(_ font: CTFont) { self.font = font }
    }
    private let sizes = NSCache<NSNumber, FontBox>()

    init(identity: String, descriptor: CTFontDescriptor) {
        self.identity = identity
        self.descriptor = descriptor
        sizes.countLimit = 8
    }

    func font(size: Double) -> CTFont {
        let key = NSNumber(value: size)
        if let box = sizes.object(forKey: key) { return box.font }
        let font = CTFontCreateWithFontDescriptor(descriptor, size, nil)
        sizes.setObject(FontBox(font), forKey: key)
        return font
    }

    static func == (lhs: FontRenderSource, rhs: FontRenderSource) -> Bool { lhs.identity == rhs.identity }
    func hash(into hasher: inout Hasher) { hasher.combine(identity) }
}

enum FontMetadataCache {
    final class Entry: NSObject {
        let faces: [FontFace]
        init(_ faces: [FontFace]) { self.faces = faces }
    }
    static let files: NSCache<NSString, Entry> = {
        let cache = NSCache<NSString, Entry>()
        cache.countLimit = 128
        cache.totalCostLimit = 64 * 1024 * 1024
        return cache
    }()

    static func identity(_ url: URL) throws -> String {
        let attributes = try FileManager.default.attributesOfItem(atPath: url.path)
        let bytes = (attributes[.size] as? NSNumber)?.stringValue ?? ""
        let modified = (attributes[.modificationDate] as? Date)?.timeIntervalSinceReferenceDate.bitPattern ?? 0
        let inode = (attributes[.systemFileNumber] as? NSNumber)?.stringValue ?? ""
        return url.standardizedFileURL.path + "|\(inode)|\(bytes)|\(modified)"
    }
}
