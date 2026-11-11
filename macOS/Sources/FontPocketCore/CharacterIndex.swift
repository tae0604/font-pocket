import Foundation

public struct CharacterIndex: Sendable {
    public let characters: [UInt32]
    public let coverage: [String: Int]

    public init(_ values: [UInt32]) {
        let supported = Array(Set(values.filter { Unicode.Scalar($0) != nil })).sorted()
        characters = supported.filter { $0 >= 32 && !(0x7f...0x9f).contains($0) }
        coverage = Dictionary(uniqueKeysWithValues: FontAnalysis.ranges.map { name, bounds in
            (name, Self.lowerBound(bounds.upperBound + 1, in: supported) - Self.lowerBound(bounds.lowerBound, in: supported))
        })
    }

    public func matching(_ query: String, script: String) throws -> [UInt32] {
        let candidates: ArraySlice<UInt32>
        if let bounds = FontAnalysis.ranges.first(where: { $0.0 == script })?.1 {
            candidates = characters[Self.lowerBound(bounds.lowerBound, in: characters)..<Self.lowerBound(bounds.upperBound + 1, in: characters)]
        } else { candidates = characters[...] }
        let query = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else { return Array(candidates) }
        let normalized = query.uppercased()
        let hexadecimal = normalized.hasPrefix("U+")
        let wanted = Set(query.unicodeScalars.map(\.value))
        var result: [UInt32] = []
        for (index, value) in candidates.enumerated() {
            if index & 1023 == 0 { try Task.checkCancellation() }
            if hexadecimal ? String(format: "U+%04X", value).hasPrefix(normalized) : wanted.contains(value) {
                result.append(value)
            }
        }
        return result
    }

    private static func lowerBound(_ value: UInt32, in values: [UInt32]) -> Int {
        var lower = 0, upper = values.count
        while lower < upper {
            let middle = lower + (upper - lower) / 2
            if values[middle] < value { lower = middle + 1 } else { upper = middle }
        }
        return lower
    }
}
