import SwiftUI
import AppKit
import FontPocketCore

struct LibraryToolsView:View{
    @EnvironmentObject var model:AppModel
    @Environment(\.dismiss) var dismiss
    @State private var document=PersonalDocument()
    @State private var name=""
    @State private var rule=""
    @State private var versions:[FontVersion]=[]
    @State private var groups:[DuplicateGroup]=[]
    @State private var keep:[String:String]=[:]
    @State private var clean=Set<String>()
    @State private var versionSelection=Set<String>()
    @State private var catalogLayout="list"
    @State private var inspection=""
    @State private var update=""
    @State private var comparison:[FontFace]=[]
    @State private var compare=false
    private var face:FontFace?{model.preview ?? model.fonts.first}
    var body:some View{
        VStack(alignment:.leading,spacing:16){
            HStack{Text(model.t("라이브러리 도구","Library tools")).font(.title2.bold());Spacer();Button(model.t("닫기","Done")){dismiss()}.keyboardShortcut(.cancelAction)}
            TabView(selection:$model.toolsTab){
                notes.tag("notes").tabItem{Text(model.t("메모·태그","Notes / tags"))}
                collections.tag("collections").tabItem{Text(model.t("모음","Collections"))}
                duplicates.tag("duplicates").tabItem{Text(model.t("중복","Duplicates"))}
                versionView.tag("versions").tabItem{Text(model.t("버전","Versions"))}
                catalog.tag("catalog").tabItem{Text(model.t("전체 견본","Catalog"))}
                inspectionView.tag("inspect").tabItem{Text(model.t("검사","Inspect"))}
                backup.tag("backup").tabItem{Text(model.t("백업·업데이트","Backup / update"))}
            }
        }.padding(22).frame(width:1000,height:740)
        .sheet(isPresented:$compare){ComparisonView(faces:comparison).environmentObject(model)}
        .task{document=model.personal;do{versions=try await model.vault.read()}catch{model.error=error.localizedDescription}}
    }
    private var notes:some View{VStack(alignment:.leading,spacing:14){
        if let face{
            Text(face.title).font(.headline)
            TextEditor(text:Binding(get:{document.annotations[face.id]?.note ?? ""},set:{value in var entry=document.annotations[face.id] ?? FontAnnotation();entry.note=value;document.annotations[face.id]=entry})).frame(minHeight:160).border(Color.secondary.opacity(0.2))
            TextField(model.t("쉼표로 구분한 태그","Tags separated by commas"),text:Binding(get:{document.annotations[face.id]?.tags.joined(separator:", ") ?? ""},set:{value in var entry=document.annotations[face.id] ?? FontAnnotation();entry.tags=value.split(separator:",").map{$0.trimmingCharacters(in:.whitespaces)}.filter{!$0.isEmpty};document.annotations[face.id]=entry})).textFieldStyle(.roundedBorder)
            ForEach(document.collections.filter{$0.rule.isEmpty}){collection in Toggle(collection.name,isOn:Binding(get:{document.collections.first(where:{$0.id==collection.id})?.members.contains(face.id) == true},set:{value in guard let index=document.collections.firstIndex(where:{$0.id==collection.id}) else{return};if value{document.collections[index].members.insert(face.id)}else{document.collections[index].members.remove(face.id)}}))}
            Button(model.t("저장","Save")){model.savePersonal(document)}.disabled(model.busy)
        }else{Text(model.t("폰트를 먼저 선택하세요.","Select a font first."))};Spacer()
    }.padding()}
    private var collections:some View{VStack(alignment:.leading,spacing:12){
        HStack{TextField(model.t("모음 이름","Collection name"),text:$name);TextField(model.t("자동 조건 (비우면 일반 모음)","Smart rule (empty for manual collection)"),text:$rule);Button(model.t("추가","Add")){document.collections.append(FontCollection(name:name,rule:rule,members:Set(model.selectedFonts.map(\.id))));name="";rule=""}.disabled(name.trimmingCharacters(in:.whitespaces).isEmpty)}
        Text(model.t("자동 조건 예: 본문;TTF;활성 · 이름·태그·형식·활성·비활성·가변 조건을 ;로 연결합니다.","Smart rules: body;TTF;enabled. Join name/tag, format, enabled/disabled or variable conditions with ;.")).font(.caption).foregroundStyle(.secondary)
        List{ForEach(document.collections){collection in HStack{VStack(alignment:.leading){TextField(model.t("모음 이름","Name"),text:Binding(get:{document.collections.first(where:{$0.id==collection.id})?.name ?? ""},set:{value in if let i=document.collections.firstIndex(where:{$0.id==collection.id}){document.collections[i].name=value}}));TextField(model.t("자동 조건 · 일반 모음은 비워두세요","Smart rule · leave empty for manual"),text:Binding(get:{document.collections.first(where:{$0.id==collection.id})?.rule ?? ""},set:{value in if let i=document.collections.firstIndex(where:{$0.id==collection.id}){document.collections[i].rule=value}})).font(.caption).foregroundStyle(.secondary)};Spacer();Button(model.t("보기","Show")){model.perform(model.t("모음 저장 중…","Saving collection…")){try await model.personalStore.save(document);model.personal=document;model.activeCollection=collection.id;dismiss()}};Button(model.t("삭제","Delete")){document.collections.removeAll{$0.id==collection.id}}}}}
        HStack{Button(model.t("저장","Save")){model.savePersonal(document)}.disabled(model.busy);Button(model.t("모든 폰트 보기","Show all fonts")){model.activeCollection=nil;dismiss()};Spacer()}
    }.padding()}
    private var duplicates:some View{VStack(alignment:.leading){
        HStack{Button(model.t("중복 검색","Find duplicates")){let fonts=model.fonts;model.perform(model.t("중복 검색 중…","Finding duplicates…")){let task=Task.detached{try DuplicateFinder.find(fonts)};groups=try await withTaskCancellationHandler(operation:{try await task.value},onCancel:{task.cancel()});for group in groups{keep[group.id]=group.fonts.first(where:{!$0.canManage})?.id ?? group.fonts[0].id};clean=Set(groups.filter(\.exact).map(\.id))}}.disabled(model.busy);Spacer();Text(model.t("완전 동일 그룹만 기본 선택됩니다.","Only byte-identical groups are selected by default.")).font(.caption)}
        List(groups){group in VStack(alignment:.leading,spacing:8){Toggle(group.exact ? model.t("동일 파일","Identical files"):model.t("동일 스타일 후보 · 직접 확인","Same-style candidates · review manually"),isOn:Binding(get:{clean.contains(group.id)},set:{if $0{clean.insert(group.id)}else{clean.remove(group.id)}}));Picker(model.t("남길 폰트","Keep"),selection:Binding(get:{keep[group.id] ?? group.fonts[0].id},set:{keep[group.id]=$0})){ForEach(group.fonts){font in Text(font.title+" · "+font.format+" · "+font.file.lastPathComponent).tag(font.id)}}}}
        Button(model.t("복구 백업 후 선택 그룹 정리","Back up and remove selected duplicates")){let remove=groups.filter{clean.contains($0.id)}.flatMap{group in group.fonts.filter{$0.file != group.fonts.first(where:{$0.id==keep[group.id]})?.file && $0.canManage && (group.exact || !["TTC","OTC"].contains($0.format))}};model.cleanDuplicates(remove)}.disabled(model.busy || clean.isEmpty)
    }.padding()}
    private var versionView:some View{VStack(alignment:.leading,spacing:12){
        HStack{Button(model.t("선택 폰트 보관","Archive selected")){model.archiveSelection()}.disabled(model.busy);Button(model.t("버전 파일·ZIP 추가","Add version files / ZIP")){let panel=NSOpenPanel();panel.allowsMultipleSelection=true;guard panel.runModal() == .OK else{return};model.perform(model.t("버전 읽는 중…","Reading versions…")){for file in try await model.importer.read(panel.urls){_ = try await model.vault.capture(file.file)};versions=try await model.vault.read()}}.disabled(model.busy);Button(model.t("새로고침","Refresh")){Task{do{versions=try await model.vault.read()}catch{model.error=error.localizedDescription}}}}
        Table(versions,selection:$versionSelection){TableColumn(model.t("글꼴","Font"),value:\.family);TableColumn(model.t("스타일","Style"),value:\.style);TableColumn(model.t("버전","Version"),value:\.version);TableColumn("SHA256"){Text(String($0.hash.prefix(12)))}}
        HStack{Button(model.t("선택 버전 비교","Compare versions")){model.perform(model.t("버전 읽는 중…","Reading versions…")){var result:[FontFace]=[];for version in versions where versionSelection.contains(version.id){let url=try await model.vault.file(version);if let face=try FontReader.faces(url).first(where:{$0.postScript==version.postScript}){result.append(face)}};comparison=result;compare=true}}.disabled(model.busy || versionSelection.count<2 || versionSelection.count>12)
        Button(model.t("선택한 버전 사용","Activate selected version")){if let version=versions.first(where:{versionSelection.contains($0.id)}){model.activateVersion(version)}}.disabled(model.busy || versionSelection.count != 1)}
    }.padding()}
    private var catalog:some View{VStack{
        HStack{Picker(model.t("배치","Layout"),selection:$catalogLayout){Text(model.t("문장","List")).tag("list");Text(model.t("두 열","Two columns")).tag("two");Text(model.t("격자","Grid")).tag("grid");Text(model.t("패밀리 순서","Families")).tag("family")}.pickerStyle(.segmented);Button(model.t("인쇄·PDF","Print / PDF")){FontPrinting.printFonts(model.visibleFonts,text:model.previewText,layout:catalogLayout)}}
        TextField(model.t("견본 문구","Sample text"),text:$model.previewText).textFieldStyle(.roundedBorder)
        ScrollView{LazyVGrid(columns:Array(repeating:GridItem(.flexible()),count:catalogLayout=="two" ? 2:catalogLayout=="grid" ? 3:1),alignment:.leading){ForEach(catalogLayout=="family" ? model.visibleFonts.sorted{$0.family < $1.family}:model.visibleFonts){font in VStack(alignment:.leading,spacing:8){Text(font.title).font(.caption).foregroundStyle(.secondary);Text(model.previewText).font(Font(font.previewFont(size:28))).lineLimit(3)}.padding().frame(maxWidth:.infinity,alignment:.leading).background(Color(nsColor:.textBackgroundColor),in:RoundedRectangle(cornerRadius:8)).onTapGesture(count:2){model.selection=[font.id];model.tools=false}}}}
    }.padding()}
    private var inspectionView:some View{VStack(alignment:.leading){Button(model.t("선택한 폰트 검사","Inspect selected fonts")){let fonts=model.selectedFonts.isEmpty ? model.visibleFonts:model.selectedFonts;model.perform(model.t("파일 검사 중…","Inspecting files…")){let task=Task.detached{try fonts.map{font in let report=try FontAnalysis.inspect(font);return font.title+"\n"+(report.issues.isEmpty ? "PASS":report.issues.joined(separator:"\n"))}.joined(separator:"\n\n")};inspection=try await withTaskCancellationHandler(operation:{try await task.value},onCancel:{task.cancel()})}}.disabled(model.busy);ScrollView{Text(inspection).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading)}}.padding()}
    private var backup:some View{VStack(alignment:.leading,spacing:18){
        Button(model.t("환경 백업","Back up environment")){model.profileBackup()};Button(model.t("환경 복원","Restore environment")){model.profileRestore()};Button(model.t("삭제 폰트 복원","Restore removed fonts")){model.restoreDeleted()}
        Divider();Button(model.t("서체 관리자 열기","Open Font Book")){NSWorkspace.shared.open(URL(fileURLWithPath:"/System/Applications/Font Book.app"))}
        Button(model.t("업데이트 확인","Check for updates")){model.perform(model.t("업데이트 확인 중…","Checking updates…")){update=try await MacUpdates.check()}}
        Text(update).textSelection(.enabled);Spacer()
        Button(model.t("업데이트 다운로드·설치","Download and install update")){model.perform(model.t("업데이트 처리 중…","Updating…")){try await MacUpdates.installLatest()}}
    }.padding().disabled(model.busy)}
}
