import Foundation

private final class HTTPTransfer: NSObject, URLSessionDataDelegate, @unchecked Sendable {
    private let lock = NSLock()
    private var task: URLSessionDataTask?
    private var wasCancelled = false
    private var data = Data()
    private let maximum: Int
    private var failure: Error?
    private var continuation: CheckedContinuation<Data, Error>?
    private var session: URLSession?
    init(maximum: Int) { self.maximum = maximum }
    func start(_ url: URL, continuation: CheckedContinuation<Data, Error>) {
        lock.lock(); defer { lock.unlock() }
        if wasCancelled { continuation.resume(throwing: CancellationError()); return }
        self.continuation = continuation
        let config = URLSessionConfiguration.ephemeral
        config.timeoutIntervalForRequest = 45; config.timeoutIntervalForResource = 300
        let session = URLSession(configuration: config, delegate: self, delegateQueue: nil)
        self.session = session
        var request = URLRequest(url: url)
        request.setValue("FontPocket-macOS/0.2.0", forHTTPHeaderField: "User-Agent")
        let task = session.dataTask(with: request); self.task = task; task.resume()
    }
    func cancel() { lock.lock(); wasCancelled = true; task?.cancel(); lock.unlock() }
    func urlSession(_ session: URLSession, dataTask: URLSessionDataTask, didReceive response: URLResponse,
                    completionHandler: @escaping (URLSession.ResponseDisposition) -> Void) {
        guard let response = response as? HTTPURLResponse, (200..<300).contains(response.statusCode),
              response.expectedContentLength <= maximum else {
            failure = PocketError("The server rejected the request or the file is too large.")
            completionHandler(.cancel); return
        }
        completionHandler(.allow)
    }
    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) {
        completionHandler(request.url?.scheme?.lowercased() == "https" ? request : nil)
    }
    func urlSession(_ session: URLSession, dataTask: URLSessionDataTask, didReceive chunk: Data) {
        guard data.count + chunk.count <= maximum else {
            failure = PocketError("The download exceeds the size limit."); dataTask.cancel(); return
        }
        data.append(chunk)
    }
    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        lock.lock(); let cancelled = wasCancelled; let continuation = self.continuation; self.continuation = nil; lock.unlock()
        defer { session.finishTasksAndInvalidate(); self.session = nil }
        if cancelled { continuation?.resume(throwing: CancellationError()) }
        else if let error = failure ?? error { continuation?.resume(throwing: error) }
        else { continuation?.resume(returning: data) }
    }
}

public enum HTTPClient {
    public static func get(_ url: URL, maximum: Int = 4*1024*1024) async throws -> Data {
        guard url.scheme?.lowercased() == "https", url.host != nil, url.user == nil, url.password == nil else {
            throw PocketError("Use an HTTPS font download address.")
        }
        let transfer = HTTPTransfer(maximum: maximum)
        return try await withTaskCancellationHandler(operation: {
            try Task.checkCancellation()
            return try await withCheckedThrowingContinuation { transfer.start(url, continuation: $0) }
        }, onCancel: { transfer.cancel() })
    }
}

public enum FontDownloads {
    public static func googleFamily(_ url: URL) -> String? {
        guard url.host?.lowercased() == "fonts.google.com", url.pathComponents.count >= 3,
              url.pathComponents[1] == "specimen" else { return nil }
        let family = url.pathComponents[2].lowercased().filter { $0.isASCII && ($0.isLetter || $0.isNumber) }
        return family.isEmpty ? nil : family
    }
    public static func links(in html: String, base: URL) -> [(URL, String)] {
        let cleaned = html.replacingOccurrences(of: "(?is)<(script|style)\\b[^>]*>.*?</\\1>", with: "", options: .regularExpression)
        let pattern = "(?is)<a\\b[^>]*?href\\s*=\\s*['\"]([^'\"]+)['\"][^>]*>(.*?)</a>"
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return [] }
        let text = cleaned as NSString
        return regex.matches(in: cleaned, range: NSRange(location: 0, length: text.length)).compactMap { match in
            let raw = text.substring(with: match.range(at: 1)).replacingOccurrences(of: "&amp;", with: "&")
            guard let url = URL(string: raw, relativeTo: base)?.absoluteURL, url.scheme?.lowercased() == "https" else { return nil }
            let label = text.substring(with: match.range(at: 2)).replacingOccurrences(of: "<[^>]+>", with: "", options: .regularExpression)
                .replacingOccurrences(of: "&amp;", with: "&").trimmingCharacters(in: .whitespacesAndNewlines)
            return (url, label)
        }
    }
    public static func resolve(_ url: URL) async throws -> [DownloadCandidate] {
        if let family = googleFamily(url) {
            for section in ["ofl", "apache", "ufl"] {
                let endpoint = URL(string: "https://api.github.com/repos/google/fonts/contents/\(section)/\(family)")!
                do {
                    let data = try await HTTPClient.get(endpoint)
                    let files = try JSONDecoder().decode([GitHubFile].self, from: data)
                    let choices = files.compactMap { item -> DownloadCandidate? in
                        guard FontReader.extensions.contains(URL(fileURLWithPath: item.name).pathExtension.lowercased()), let download = item.download_url else { return nil }
                        return DownloadCandidate(url: download, name: item.name)
                    }
                    if !choices.isEmpty { return choices.sorted { $0.name < $1.name } }
                } catch is CancellationError { throw CancellationError() } catch { continue }
            }
            throw PocketError("Google Fonts desktop files were not found for this family.")
        }
        return try await explore(url, depth: 0, visited: [])
    }
    private struct GitHubFile: Decodable { let name: String; let download_url: URL? }
    private struct GitHubAsset: Decodable { let name: String; let browser_download_url: URL }
    private struct GitHubRelease: Decodable { let assets: [GitHubAsset] }
    private static func explore(_ url: URL, depth: Int, visited: Set<URL>) async throws -> [DownloadCandidate] {
        try Task.checkCancellation()
        guard depth <= 4 && !visited.contains(url) else { return [] }
        let allowed = FontImporter.supported.union(["zip"])
        if allowed.contains(url.pathExtension.lowercased()) {
            return [DownloadCandidate(url: url, name: url.lastPathComponent)]
        }
        if url.host?.lowercased() == "github.com" {
            let parts = url.pathComponents.filter { $0 != "/" }
            if parts.count >= 2 {
                var endpoint = "https://api.github.com/repos/\(parts[0])/\(parts[1])/releases/latest"
                if parts.count >= 5 && parts[2] == "releases" && parts[3] == "tag" {
                    endpoint = "https://api.github.com/repos/\(parts[0])/\(parts[1])/releases/tags/\(parts[4])"
                }
                let release = try JSONDecoder().decode(GitHubRelease.self, from: await HTTPClient.get(URL(string: endpoint)!))
                return release.assets.filter { allowed.contains(URL(fileURLWithPath: $0.name).pathExtension.lowercased()) }
                    .map { DownloadCandidate(url: $0.browser_download_url, name: $0.name) }
            }
        }
        let data = try await HTTPClient.get(url)
        guard let html = String(data: data, encoding: .utf8) else { throw PocketError("Cannot read the download page.") }
        let anchors = links(in: html, base: url)
        // Prefer the creator's actual desktop packages, not Noonnu's CSS/webfont previews.
        let direct = anchors.filter { allowed.contains($0.0.pathExtension.lowercased()) }
        if !direct.isEmpty {
            var seen = Set<URL>()
            return direct.filter { seen.insert($0.0).inserted }.map { DownloadCandidate(url: $0.0, name: $0.1.isEmpty ? $0.0.lastPathComponent : $0.1 + " · " + $0.0.lastPathComponent) }
        }
        var next = anchors.filter { pair in
            let text = pair.1.lowercased()
            return text.contains("다운로드") || text.contains("download") || pair.0.host == "github.com" && pair.0.path.contains("/releases")
        }
        if url.host == "noonnu.cc" { next = next.filter { $0.0.host != "noonnu.cc" } }
        var visited = visited; visited.insert(url)
        for (candidate, _) in next.prefix(5) {
            do {
                let result = try await explore(candidate, depth: depth+1, visited: visited)
                if !result.isEmpty { return result }
            } catch is CancellationError { throw CancellationError() } catch { continue }
        }
        throw PocketError("No desktop font download was found. Download the ZIP from the creator's site, then add it here.")
    }
    public static func download(_ choice: DownloadCandidate, into folder: URL) async throws -> URL {
        let data = try await HTTPClient.get(choice.url, maximum: ArchivePolicy.maximumCompressed)
        try Task.checkCancellation()
        var ext = choice.url.pathExtension.lowercased()
        if data.prefix(4) == Data([0x50,0x4b,0x03,0x04]) { ext = "zip" }
        guard FontImporter.supported.union(["zip"]).contains(ext) else { throw PocketError("The server did not return a supported font or ZIP file.") }
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let file = folder.appendingPathComponent(UUID().uuidString + "." + ext)
        try data.write(to: file, options: .atomic)
        return file
    }
}
