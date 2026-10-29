import SwiftUI
import AppKit
import UniformTypeIdentifiers
import FontPocketCore

extension AppModel {
    func savePersonal(_ document:PersonalDocument){perform(t("개인 정보 저장 중…","Saving library…")){try await self.personalStore.save(document);self.personal=document;self.record(self.t("저장했습니다.","Saved."))}}
    func archiveSelection(){let files=Set((page == .install ? queue.filter{checked.contains($0.id)}:selectedFonts).map(\.file));perform(t("버전 보관 중…","Archiving versions…")){for file in files{_ = try await self.vault.capture(file)};self.record(self.t("버전을 보관했습니다.","Versions archived."))}}
    func activateVersion(_ version:FontVersion){perform(t("버전 전환 중…","Switching version…")){let file=try await self.vault.file(version);for face in self.fonts where face.postScript==version.postScript && face.canManage{_ = try await self.vault.capture(face.file)};try await self.repository.switchVersion(file,postScript:version.postScript);self.fonts=try await self.repository.library(includeSystem:self.showSystem);self.record(self.t("버전을 전환했습니다.","Version activated."))}}
    func profileBackup(){let panel=NSSavePanel();panel.allowedContentTypes=[.zip];panel.nameFieldStringValue="FontPocket-Environment.zip";guard panel.runModal() == .OK,let url=panel.url else{return};perform(t("환경 백업 중…","Backing up environment…")){try await EnvironmentBackup.create(repository:self.repository,personal:self.personalStore,vault:self.vault,destination:url);self.record(self.t("환경 백업 완료","Environment backup complete"))}}
    func profileRestore(){let panel=NSOpenPanel();panel.allowedContentTypes=[.zip];guard panel.runModal() == .OK,let url=panel.url else{return};let alert=NSAlert();alert.messageText=t("환경을 복원할까요?","Restore environment?");alert.informativeText=t("백업 무결성을 먼저 검사합니다. 기존 개인 정보와 폰트는 유지하고 없는 항목을 추가합니다.","Integrity is verified first. Existing fonts and personal data are preserved; missing items are added.");alert.addButton(withTitle:t("복원","Restore"));alert.addButton(withTitle:t("취소","Cancel"));guard alert.runModal() == .alertFirstButtonReturn else{return};perform(t("환경 복원 중…","Restoring environment…")){try await EnvironmentBackup.restore(url,repository:self.repository,personal:self.personalStore,vault:self.vault);self.personal=try await self.personalStore.read();self.fonts=try await self.repository.library(includeSystem:self.showSystem);self.record(self.t("환경 복원 완료","Environment restored"))}}
    func cleanDuplicates(_ files:[FontFace]){guard !files.isEmpty else{return};let alert=NSAlert();alert.messageText=t("선택한 중복 폰트를 정리할까요?","Remove selected duplicate fonts?");alert.informativeText=t("복구 ZIP을 먼저 저장한 뒤 앱이 설치한 파일만 휴지통으로 옮깁니다.","A recovery ZIP is saved before app-managed files are moved to Trash.");alert.addButton(withTitle:t("정리","Remove"));alert.addButton(withTitle:t("취소","Cancel"));guard alert.runModal() == .alertFirstButtonReturn else{return};perform(t("중복 정리 중…","Removing duplicates…")){let root=LibraryPaths.live.root.appendingPathComponent("Removed");try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true);try await FontBackup.create(files:files.map(\.file),destination:root.appendingPathComponent(UUID().uuidString+".zip"));for key in Set(files.compactMap(\.managedKey)){try await self.repository.trash(key)};self.fonts=try await self.repository.library(includeSystem:self.showSystem);self.record(self.t("복구 ZIP 저장 및 정리 완료","Recovery backup saved; duplicates removed"))}}
    func restoreDeleted(){let root=LibraryPaths.live.root.appendingPathComponent("Removed");try? FileManager.default.createDirectory(at:root,withIntermediateDirectories:true);let panel=NSOpenPanel();panel.directoryURL=root;panel.allowedContentTypes=[.zip];guard panel.runModal() == .OK else{return};addFiles(panel.urls)}
    func exportWeb(_ face:FontFace,woff2:Bool){let panel=NSSavePanel();panel.allowedContentTypes=woff2 ? [UTType(filenameExtension:"woff2")!]:[.zip];panel.nameFieldStringValue=woff2 ? "font.woff2":"web-font.zip";guard panel.runModal() == .OK,let destination=panel.url else{return};perform(t("웹폰트 내보내는 중…","Exporting web font…")){
        if woff2{let bytes=try await WebFontCodec.convert(face.file,decompress:false);try bytes.write(to:destination,options:.atomic)}else{
            let folder=self.workspace.appendingPathComponent(UUID().uuidString);try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true);defer{try? FileManager.default.removeItem(at:folder)}
            let name="font."+face.file.pathExtension;try FileManager.default.copyItem(at:face.file,to:folder.appendingPathComponent(name))
            let css="@font-face {font-family:'PocketFont';src:url('./\(name)');font-display:swap;}body{font-family:'PocketFont',sans-serif;}"
            try css.write(to:folder.appendingPathComponent("font.css"),atomically:true,encoding:.utf8)
            try "<!doctype html><meta charset='utf-8'><link rel='stylesheet' href='font.css'><p contenteditable>Font Pocket — 다람쥐 헌 쳇바퀴에 타고파</p>".write(to:folder.appendingPathComponent("index.html"),atomically:true,encoding:.utf8)
            let temp=destination.deletingLastPathComponent().appendingPathComponent(UUID().uuidString+".zip");defer{try? FileManager.default.removeItem(at:temp)}
            _ = try await ProcessRunner.run("/usr/bin/ditto",["-c","-k","--keepParent",folder.path,temp.path]);try Task.checkCancellation()
            if FileManager.default.fileExists(atPath:destination.path){_ = try FileManager.default.replaceItemAt(destination,withItemAt:temp)}else{try FileManager.default.moveItem(at:temp,to:destination)}
        };self.record(self.t("내보내기 완료 · 배포 전 폰트 라이선스를 확인하세요.","Export complete · check the font license before distributing."))}}
}

@MainActor enum FontPrinting {
    static func printFonts(_ faces:[FontFace],text:String,characters:Bool=false){
        let view=NSTextView(frame:NSRect(x:0,y:0,width:680,height:1000));view.isEditable=false
        let result=NSMutableAttributedString(string:"")
        for face in faces.prefix(100){result.append(NSAttributedString(string:face.title+"\n",attributes:[.font:NSFont.boldSystemFont(ofSize:16)]));var sample=text
            if characters,let report=try? FontAnalysis.inspect(face){sample=report.characters.compactMap{Unicode.Scalar($0).map(String.init)}.joined(separator:" ")}
            result.append(NSAttributedString(string:sample+"\n\n",attributes:[.font:face.previewFont(size:characters ? 18:28)]))}
        view.textStorage?.setAttributedString(result);view.textContainer?.containerSize=NSSize(width:680,height:100000)
        if let manager=view.layoutManager,let container=view.textContainer{manager.ensureLayout(for:container);view.setFrameSize(NSSize(width:680,height:min(200000,max(1000,manager.usedRect(for:container).height+40))))}
        let operation=NSPrintOperation(view:view);operation.showsPrintPanel=true;operation.showsProgressPanel=true;operation.run()
    }
}
