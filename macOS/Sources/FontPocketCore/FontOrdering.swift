import Foundation

public enum FontOrdering {
    /// Keep the order stable across refresh, catalog, family picker and search.
    /// Style names are not a reliable weight order (ExtraLight sorts after Bold).
    public static func precedes(_ a: FontFace, _ b: FontFace) -> Bool {
        let family = a.family.localizedStandardCompare(b.family)
        if family != .orderedSame { return family == .orderedAscending }
        if a.weight != b.weight { return a.weight < b.weight }
        let style = a.style.localizedStandardCompare(b.style)
        if style != .orderedSame { return style == .orderedAscending }
        if a.isVariable != b.isVariable { return !a.isVariable }
        return a.id < b.id
    }
    public static func family(of face: FontFace, in fonts: [FontFace]) -> [FontFace] {
        var seen = Set<String>()
        return (fonts + [face]).filter {
            $0.family.localizedCaseInsensitiveCompare(face.family) == .orderedSame && seen.insert($0.id).inserted
        }.sorted(by: precedes)
    }
    public static func comparisonAllowed(_ fonts: [FontFace]) -> Bool {
        guard (2...12).contains(fonts.count) else { return false }
        // Count each shared TTC file once, and avoid overflow from corrupt metadata.
        var sizes: [URL: Int64] = [:]
        for face in fonts { sizes[face.file] = max(sizes[face.file] ?? 0, max(0, face.bytes)) }
        var remaining: Int64 = 256 * 1024 * 1024
        for size in sizes.values { guard size <= remaining else { return false }; remaining -= size }
        return true
    }
}
