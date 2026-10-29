import Foundation
import WebKit
import FontPocketCore

@MainActor enum WebFontCodec {
    static func convert(_ url: URL, decompress: Bool) async throws -> Data {
        let data=try Data(contentsOf:url)
        guard data.count<=128*1024*1024 else{throw PocketError("Font exceeds 128 MB")}
        let view=WKWebView(frame:.zero)
        let packaged=Bundle.main.resourceURL.flatMap{Bundle(url:$0.appendingPathComponent("FontPocketMac_FontPocketMac.bundle"))}
        guard let resource=packaged?.url(forResource:"woff2-encoder",withExtension:"js") ?? Bundle.main.url(forResource:"woff2-encoder",withExtension:"js") ?? Bundle.module.url(forResource:"woff2-encoder",withExtension:"js") else{throw PocketError("Font codec resource missing")}
        let source=try String(contentsOf:resource).replacingOccurrences(of:"export{I as compress,Q as decompress};",with:"window.fontCodec={compress:I,decompress:Q};")
        let script=WKUserScript(source:source,injectionTime:.atDocumentStart,forMainFrameOnly:true)
        view.configuration.userContentController.addUserScript(script)
        view.loadHTMLString("<!doctype html><meta charset='utf-8'>",baseURL:nil)
        let deadline=Date().addingTimeInterval(120)
        while true{
            try Task.checkCancellation()
            if Date()>deadline{throw PocketError("Web font conversion timed out")}
            if (try? await view.evaluateJavaScript("!!window.fontCodec")) as? Bool == true{break}
            try await Task.sleep(nanoseconds:50_000_000)
        }
        let code="const a=Uint8Array.from(atob(input),c=>c.charCodeAt(0));const b=await window.fontCodec[operation](a);let s='';for(let i=0;i<b.length;i+=32768)s+=String.fromCharCode(...b.subarray(i,i+32768));return btoa(s);"
        let result=try await view.callAsyncJavaScript(code,arguments:["input":data.base64EncodedString(),"operation":decompress ? "decompress":"compress"],in:nil,contentWorld:.page)
        try Task.checkCancellation()
        guard let encoded=result as? String,let bytes=Data(base64Encoded:encoded),bytes.count<=150*1024*1024 else{throw PocketError("Invalid font conversion output")}
        return bytes
    }
    static func prepare(_ url:URL,folder:URL) async throws -> URL{
        let ext=url.pathExtension.lowercased();guard ["woff","woff2","sfnt"].contains(ext) else{return url}
        let data:Data
        if ext=="woff2"{data=try await convert(url,decompress:true)}else if ext=="woff"{data=try WoffDecoder.decode(Data(contentsOf:url))}else{data=try Data(contentsOf:url)}
        let native=data.prefix(4)==Data("OTTO".utf8) ? "otf":data.prefix(4)==Data("ttcf".utf8) ? "ttc":"ttf"
        let output=folder.appendingPathComponent(UUID().uuidString+"."+native)
        try data.write(to:output,options:.atomic);_ = try FontReader.faces(output);return output
    }
}
