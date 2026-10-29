import Foundation
import CoreText

public struct VariationAxis: Identifiable, Sendable {
    public let id: Int
    public let name: String
    public let minimum: Double
    public let defaultValue: Double
    public let maximum: Double
}
public struct FontInspection: Sendable {
    public let metadata: [String: String]
    public let characters: [UInt32]
    public let glyphs: Int
    public let axes: [VariationAxis]
    public let tables: [String]
    public let issues: [String]
}
public enum FontAnalysis {
    public static func inspect(_ face: FontFace) throws -> FontInspection {
        let font = face.previewFont(size: 24)
        let names: [(String, CFString)] = [("Family", kCTFontFamilyNameKey), ("Style", kCTFontStyleNameKey),
          ("Version", kCTFontVersionNameKey), ("Copyright", kCTFontCopyrightNameKey), ("Designer", kCTFontDesignerNameKey),
          ("Manufacturer", kCTFontManufacturerNameKey), ("Description", kCTFontDescriptionNameKey),
          ("License", kCTFontLicenseNameKey), ("License URL", kCTFontLicenseURLNameKey)]
        var metadata: [String: String] = [:]
        for (label, key) in names { if let value = CTFontCopyName(font, key) { metadata[label] = value as String } }
        metadata["PostScript"] = face.postScript; metadata["File"] = face.file.path
        metadata["SHA256"] = try FontReader.hash(face.file)
        let charset = CTFontCopyCharacterSet(font)
        var characters: [UInt32] = []
        for plane in 0...16 where CFCharacterSetHasMemberInPlane(charset, CFIndex(plane)) {
            for value in UInt32(plane*65536)...UInt32(min(0x10ffff, (plane+1)*65536-1)) {
                if value & 1023 == 0 { try Task.checkCancellation() }
                if !(0xd800...0xdfff).contains(value) && CFCharacterSetIsLongCharacterMember(charset, value) { characters.append(value) }
            }
        }
        let axes = (CTFontCopyVariationAxes(font) as? [[String: Any]] ?? []).compactMap { item -> VariationAxis? in
            guard let identifier = item[kCTFontVariationAxisIdentifierKey as String] as? NSNumber,
              let low = item[kCTFontVariationAxisMinimumValueKey as String] as? NSNumber,
              let high = item[kCTFontVariationAxisMaximumValueKey as String] as? NSNumber,
              let normal = item[kCTFontVariationAxisDefaultValueKey as String] as? NSNumber else { return nil }
            return VariationAxis(id: identifier.intValue, name: item[kCTFontVariationAxisNameKey as String] as? String ?? identifier.stringValue,
              minimum: low.doubleValue, defaultValue: normal.doubleValue, maximum: high.doubleValue)
        }
        let raw=try Data(contentsOf:face.file,options:.mappedIfSafe)
        func word(_ p:Int)->Int{guard p>=0,p+4<=raw.count else{return 0};return raw[p..<p+4].reduce(0){($0<<8)|Int($1)}}
        let base=raw.prefix(4)==Data("ttcf".utf8) ? word(12):0
        let count=base+6<=raw.count ? Int(raw[base+4])*256+Int(raw[base+5]):0
        let tables=(0..<min(count,4096)).compactMap{i->String? in let p=base+12+i*16;guard p+4<=raw.count else{return nil};return String(data:raw.subdata(in:p..<p+4),encoding:.ascii)}
        let issues = try validateSFNT(face.file)
        return FontInspection(metadata: metadata, characters: characters, glyphs: CTFontGetGlyphCount(font), axes: axes, tables: tables, issues: issues)
    }
    public static func validateSFNT(_ url: URL) throws -> [String] {
        let data = try Data(contentsOf: url, options: .mappedIfSafe)
        func u16(_ p: Int) throws -> Int { guard p >= 0, p+2 <= data.count else { throw PocketError("Truncated font table directory") }; return Int(data[p])<<8 | Int(data[p+1]) }
        func u32(_ p: Int) throws -> Int { guard p >= 0,p+4 <= data.count else { throw PocketError("Truncated font table") }; return Int(data[p])<<24 | Int(data[p+1])<<16 | Int(data[p+2])<<8 | Int(data[p+3]) }
        var offsets = [0], issues: [String] = []
        if data.prefix(4) == Data("ttcf".utf8) {
            let count = try u32(8); guard count > 0,count <= 1024 else { throw PocketError("Invalid collection count") }
            offsets = try (0..<count).map { try u32(12+$0*4) }
        }
        for base in offsets {
            let count = try u16(base+4); guard count > 0,count <= 4096 else { throw PocketError("Invalid table count") }
            for i in 0..<count {
                try Task.checkCancellation()
                let p = base+12+i*16, expected = UInt32(try u32(p+4)), start = try u32(p+8), length = try u32(p+12)
                guard start <= data.count,length <= data.count-start else { throw PocketError("Font table is outside the file") }
                let tag = try u32(p)
                var sum: UInt32 = 0
                for j in stride(from: 0, to: length, by: 4) {
                    var word: UInt32 = 0
                    for k in 0..<4 { word = (word << 8) | (j+k < length ? UInt32(data[start+j+k]) : 0) }
                    if tag == 0x68656164 && j == 8 { word = 0 }
                    sum = sum &+ word
                }
                if sum != expected { issues.append("Checksum mismatch: table \(String(tag,radix:16))") }
            }
        }
        return issues
    }
    public static let ranges: [(String, ClosedRange<UInt32>)] = [
        ("Latin",0x20...0x24f),("Hangul syllables",0xac00...0xd7a3),("Hangul Jamo",0x1100...0x11ff),
        ("Japanese Kana",0x3040...0x30ff),("CJK",0x4e00...0x9fff),("Cyrillic",0x400...0x4ff),
        ("Greek",0x370...0x3ff),("Arabic",0x600...0x6ff),("Emoji",0x1f300...0x1faff)]
}

public extension FontFace {
    var weight: Double { weightValue }
    var isVariable: Bool { variable }
    func variedFont(size: Double, values: [Int: Double]) -> CTFont {
        let original = previewFont(size: size)
        guard !values.isEmpty else { return original }
        let variations = Dictionary(uniqueKeysWithValues: values.map { (NSNumber(value: $0.key), NSNumber(value: $0.value)) })
        let descriptor = CTFontDescriptorCreateCopyWithAttributes(CTFontCopyFontDescriptor(original), [kCTFontVariationAttribute: variations] as CFDictionary)
        return CTFontCreateWithFontDescriptor(descriptor, size, nil)
    }
}
