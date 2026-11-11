import Foundation

public enum MacReleaseCatalog {
    public struct Asset: Codable, Sendable {
        public let name: String
        public let browser_download_url: URL
        public let size: Int
        public init(name:String,url:URL,size:Int) { self.name=name;browser_download_url=url;self.size=size }
    }
    public struct Release: Codable, Sendable {
        public let tag_name: String
        public let prerelease: Bool
        public let draft: Bool
        public let assets: [Asset]
        public init(tag:String,prerelease:Bool=false,draft:Bool=false,assets:[Asset]) { tag_name=tag;self.prerelease=prerelease;self.draft=draft;self.assets=assets }
    }
    public struct Candidate: Sendable {
        public let release: Release
        public let app: Asset
        public let checksum: Asset
        public let version: String
    }
    public static func version(_ name:String) -> String? {
        let prefix="FontPocket-macOS-",suffix="-universal.zip"
        guard name.hasPrefix(prefix),name.hasSuffix(suffix) else { return nil }
        let value=String(name.dropFirst(prefix.count).dropLast(suffix.count))
        let parts=value.split(separator:".",omittingEmptySubsequences:false)
        guard parts.count==3,parts.allSatisfy({ !$0.isEmpty && $0.count<=8 && $0.allSatisfy { $0.isASCII && $0.isNumber } }) else { return nil }
        return value
    }
    public static func trusted(_ asset:Asset) -> Bool {
        let url=asset.browser_download_url
        return url.scheme=="https" && url.host=="github.com" && url.user==nil && url.password==nil && (url.port==nil || url.port==443) && url.path.hasPrefix("/tae0604/font-pocket/releases/download/")
    }
    /// Release publication order and asset order do not imply version order.
    /// Ignore DMGs, Windows packages, previews and ZIPs lacking a matching hash.
    public static func latest(_ releases:[Release]) -> Candidate? {
        releases.filter { !$0.draft && !$0.prerelease }.flatMap { release in
            release.assets.compactMap { app -> Candidate? in
                guard let number=version(app.name), trusted(app), app.size>0,app.size<=200*1024*1024,
                      let checksum=release.assets.first(where:{ $0.name==app.name+".sha256" && trusted($0) && $0.size>0 && $0.size<=4096 }) else { return nil }
                return Candidate(release:release,app:app,checksum:checksum,version:number)
            }
        }.max { $0.version.compare($1.version,options:.numeric) == .orderedAscending }
    }
}
