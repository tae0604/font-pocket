import Foundation
import AppKit
import FontPocketCore

@MainActor enum MacUpdates{
    struct Asset:Decodable{let name:String;let browser_download_url:URL;let size:Int}
    struct Release:Decodable{let tag_name:String;let prerelease:Bool;let draft:Bool;let assets:[Asset]}
    static func version(_ asset:Asset)->String?{let pattern="(?i)FontPocket-macOS-([0-9]+(?:\\.[0-9]+)+)-universal\\.zip";guard let regex=try? NSRegularExpression(pattern:pattern),let match=regex.firstMatch(in:asset.name,range:NSRange(asset.name.startIndex...,in:asset.name)),let range=Range(match.range(at:1),in:asset.name) else{return nil};return String(asset.name[range])}
    static func latest() async throws -> (Release,Asset,Asset)?{
        let releases=try JSONDecoder().decode([Release].self,from:await HTTPClient.get(URL(string:"https://api.github.com/repos/tae0604/font-pocket/releases?per_page=30")!))
        for release in releases where !release.draft && !release.prerelease{
            if let app=release.assets.first(where:{$0.name.lowercased().contains("macos") && $0.name.hasSuffix(".zip")}),let hash=release.assets.first(where:{$0.name==app.name+".sha256"}){return(release,app,hash)}
        };return nil
    }
    static func check() async throws -> String{
        guard let (release,asset,_)=try await latest() else{return "macOS 정식 업데이트가 아직 없습니다. / No stable macOS release is available yet."}
        let current=Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "0.2.2"
        if let available=version(asset),available.compare(current,options:.numeric) != .orderedDescending{return "최신 버전입니다. / Up to date. macOS \(current)"}
        return "macOS: \(release.tag_name) · https://github.com/tae0604/font-pocket/releases"
    }
    static func installLatest() async throws{
        guard let (release,asset,sidecar)=try await latest() else{throw PocketError("No stable macOS release with a SHA-256 checksum is available.")}
        let current=Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "0.2.2"
        guard let available=version(asset),available.compare(current,options:.numeric) == .orderedDescending else{throw PocketError("이미 최신 버전입니다. / Already up to date.")}
        guard [asset,sidecar].allSatisfy({$0.browser_download_url.host=="github.com" && $0.browser_download_url.path.hasPrefix("/tae0604/font-pocket/releases/download/")}) else{throw PocketError("Untrusted update source")}
        let alert=NSAlert();alert.messageText="Font Pocket \(release.tag_name)";alert.informativeText="업데이트를 다운로드·검증한 뒤 앱을 교체하고 다시 시작할까요? / Download, verify, install and restart?";alert.addButton(withTitle:"업데이트 / Update");alert.addButton(withTitle:"취소 / Cancel");guard alert.runModal() == .alertFirstButtonReturn else{return}
        let bytes=try await HTTPClient.get(asset.browser_download_url,maximum:200*1024*1024)
        guard bytes.count==asset.size else{throw PocketError("Update size mismatch")}
        let expected=String(decoding:try await HTTPClient.get(sidecar.browser_download_url,maximum:4096),as:UTF8.self).split(whereSeparator:{$0.isWhitespace}).first.map(String.init) ?? ""
        let work=FileManager.default.temporaryDirectory.appendingPathComponent("FontPocket-Update-"+UUID().uuidString);try FileManager.default.createDirectory(at:work,withIntermediateDirectories:true)
        let zip=work.appendingPathComponent("update.zip");try bytes.write(to:zip)
        guard expected.count==64,try FontReader.hash(zip)==expected.lowercased() else{throw PocketError("Update SHA-256 verification failed")}
        try ArchivePolicy.validate(bytes);_ = try await ProcessRunner.run("/usr/bin/unzip",["-qq",zip.path,"-d",work.path])
        let candidate=work.appendingPathComponent("Font Pocket.app")
        _ = try await ProcessRunner.run("/usr/bin/codesign",["--verify","--deep","--strict",candidate.path])
        guard let info=Bundle(url:candidate),info.bundleIdentifier=="io.github.tae0604.font-pocket",info.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String==available else{throw PocketError("Unexpected update app identity or version")}
        let target=Bundle.main.bundleURL
        guard target.pathExtension=="app",FileManager.default.isWritableFile(atPath:target.deletingLastPathComponent().path) else{NSWorkspace.shared.activateFileViewerSelecting([candidate]);return}
        let script=work.appendingPathComponent("install.sh")
        let body="""
        #!/bin/sh
        while kill -0 "$1" 2>/dev/null; do sleep 1; done
        old="$2.previous.$1"
        if [ -e "$old" ]; then exit 1; fi
        /bin/mv "$2" "$old" || exit 1
        if /usr/bin/ditto "$3" "$2" && /usr/bin/codesign --verify --deep --strict "$2"; then
          /usr/bin/open -n "$2"
        else
          /bin/mv "$2" "$2.failed" 2>/dev/null
          /bin/mv "$old" "$2"
          /usr/bin/open -n "$2"
        fi
        """
        try body.write(to:script,atomically:true,encoding:.utf8);try Task.checkCancellation()
        let process=Process();process.executableURL=URL(fileURLWithPath:"/bin/sh");process.arguments=[script.path,String(ProcessInfo.processInfo.processIdentifier),target.path,candidate.path];try process.run()
        DispatchQueue.main.asyncAfter(deadline:.now()+0.3){NSApp.terminate(nil)}
    }
}
