import Foundation
import CoreText

public protocol FontRegistration: Sendable {
    func register(_ url: URL) throws
    func unregister(_ url: URL) throws
}

public struct CoreTextRegistration: FontRegistration {
    public init() {}
    public func register(_ url: URL) throws {
        var error: Unmanaged<CFError>?
        if !CTFontManagerRegisterFontsForURL(url as CFURL, .user, &error) {
            if let error {
                let value = error.takeRetainedValue()
                // ~/Library/Fonts is also watched by macOS. Treat a registration race
                // as success only when EVERY face resolves to this exact copied file.
                if CFErrorGetCode(value) == Int(CTFontManagerError.alreadyRegistered.rawValue),
                   let descriptors = CTFontManagerCreateFontDescriptorsFromURL(url as CFURL) as? [CTFontDescriptor], !descriptors.isEmpty,
                   descriptors.allSatisfy({ descriptor in
                       guard let name = CTFontDescriptorCopyAttribute(descriptor, kCTFontNameAttribute) as? String else { return false }
                       let font = CTFontCreateWithName(name as CFString, 12, nil)
                       let registered = CTFontCopyAttribute(font, kCTFontURLAttribute) as? URL
                       return registered?.standardizedFileURL == url.standardizedFileURL
                   }) { return }
                throw value as Error
            }
            throw PocketError("macOS could not enable this font.")
        }
    }
    public func unregister(_ url: URL) throws {
        var error: Unmanaged<CFError>?
        if !CTFontManagerUnregisterFontsForURL(url as CFURL, .user, &error) {
            if let error { throw error.takeRetainedValue() as Error }
            throw PocketError("macOS could not disable this font.")
        }
    }
}

public struct OwnedFont: Codable, Sendable {
    public let key: String
    public let filename: String
    public var enabled: Bool
}

public actor FontRepository {
    private let paths: LibraryPaths
    private let registration: any FontRegistration
    private var records: [OwnedFont]
    private let fm = FileManager.default

    public init(paths: LibraryPaths = .live, registration: any FontRegistration = CoreTextRegistration()) throws {
        self.paths = paths; self.registration = registration
        for dir in [paths.root, paths.fonts, paths.disabled] {
            try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
            guard try dir.resourceValues(forKeys: [.isSymbolicLinkKey]).isSymbolicLink != true else {
                throw PocketError("The font library location must not be a symbolic link.")
            }
        }
        if FileManager.default.fileExists(atPath: paths.catalog.path) {
            records = try JSONDecoder().decode([OwnedFont].self, from: Data(contentsOf: paths.catalog))
            guard records.allSatisfy({ record in
                record.key.count == 64 && record.key.allSatisfy { $0.isHexDigit } &&
                record.filename == "FontPocket-" + record.key + "." + URL(fileURLWithPath: record.filename).pathExtension &&
                FontReader.extensions.contains(URL(fileURLWithPath: record.filename).pathExtension.lowercased())
            }) else { throw PocketError("The font catalog is invalid. Your font files were not changed.") }
        } else { records = [] }
    }
    private func location(_ record: OwnedFont) -> URL {
        (record.enabled ? paths.fonts : paths.disabled).appendingPathComponent(record.filename)
    }
    private func persist() throws {
        try JSONEncoder().encode(records).write(to: paths.catalog, options: .atomic)
    }
    private func owned(_ key: String) throws -> (Int, OwnedFont, URL) {
        guard let index = records.firstIndex(where: { $0.key == key }) else { throw PocketError("This font is read-only.") }
        let record = records[index], url = location(record)
        guard try FontReader.hash(url) == record.key else { throw PocketError("The font changed outside Font Pocket. Refresh before continuing.") }
        _ = try FontReader.faces(url)
        return (index, record, url)
    }
    public func library(includeSystem: Bool) throws -> [FontFace] {
        var result: [FontFace] = [], known = Set<String>()
        for record in records {
            try Task.checkCancellation()
            let url = location(record)
            if let faces = try? FontReader.faces(url, state: record.enabled ? .enabled : .disabled, managedKey: record.key) {
                result += faces; known.insert(url.standardizedFileURL.path)
            }
        }
        let collection = CTFontCollectionCreateFromAvailableFonts(nil)
        let descriptors = CTFontCollectionCreateMatchingFontDescriptors(collection) as? [CTFontDescriptor] ?? []
        let urls = Set(descriptors.compactMap { CTFontDescriptorCopyAttribute($0, kCTFontURLAttribute) as? URL })
        for url in urls.sorted(by: { $0.path < $1.path }) {
            try Task.checkCancellation()
            if known.contains(url.standardizedFileURL.path) { continue }
            if !includeSystem && url.path.hasPrefix("/System/") { continue }
            if let faces = try? FontReader.faces(url, state: .external) { result += faces }
        }
        return result.sorted { $0.title.localizedStandardCompare($1.title) == .orderedAscending }
    }
    @discardableResult public func install(_ source: URL) throws -> Bool {
        try Task.checkCancellation()
        _ = try FontReader.faces(source)
        let key = try FontReader.hash(source)
        if records.contains(where: { $0.key == key }) { return false }
        let ext = source.pathExtension.lowercased()
        guard FontReader.extensions.contains(ext) else { throw PocketError("Unsupported font format: \(ext)") }
        let record = OwnedFont(key: key, filename: "FontPocket-\(key).\(ext)", enabled: true)
        let destination = location(record)
        guard !fm.fileExists(atPath: destination.path) else { throw PocketError("The destination already exists. It was not overwritten.") }
        let temp = paths.fonts.appendingPathComponent(".FontPocket-\(UUID().uuidString).tmp")
        defer { try? fm.removeItem(at: temp) }
        try fm.copyItem(at: source, to: temp)
        guard try FontReader.hash(temp) == key else { throw PocketError("The font changed while copying.") }
        try Task.checkCancellation()
        try fm.moveItem(at: temp, to: destination)
        do { try registration.register(destination) }
        catch { try? fm.removeItem(at: destination); throw error }
        records.append(record)
        do { try persist() }
        catch {
            records.removeLast()
            // If unregistering fails, retain the file: never delete a registered font.
            do { try registration.unregister(destination); try fm.removeItem(at: destination) }
            catch { records.append(record); try? persist() }
            throw error
        }
        return true
    }
    public func setEnabled(_ key: String, enabled: Bool) throws {
        try Task.checkCancellation()
        let (index, record, source) = try owned(key)
        if record.enabled == enabled { return }
        var updated = record; updated.enabled = enabled
        let destination = location(updated)
        guard !fm.fileExists(atPath: destination.path) else { throw PocketError("The destination already exists.") }
        if record.enabled { try registration.unregister(source) }
        do {
            try fm.moveItem(at: source, to: destination)
            if enabled { try registration.register(destination) }
            records[index] = updated
            try persist()
        } catch {
            if enabled { try? registration.unregister(destination) }
            if fm.fileExists(atPath: destination.path) { try? fm.moveItem(at: destination, to: source) }
            if record.enabled { try? registration.register(source) }
            records[index] = record; try? persist()
            throw error
        }
    }
    public func trash(_ key: String) throws {
        try Task.checkCancellation()
        let (index, record, source) = try owned(key)
        if record.enabled { try registration.unregister(source) }
        do {
            var trashed: NSURL?
            try fm.trashItem(at: source, resultingItemURL: &trashed)
            records.remove(at: index)
            do { try persist() }
            catch {
                if let trashed { try fm.moveItem(at: trashed as URL, to: source) }
                records.insert(record, at: index)
                if record.enabled { try? registration.register(source) }
                throw error
            }
        } catch {
            if record.enabled && fm.fileExists(atPath: source.path) { try? registration.register(source) }
            throw error
        }
    }
}
