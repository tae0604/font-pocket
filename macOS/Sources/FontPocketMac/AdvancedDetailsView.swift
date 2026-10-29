import SwiftUI
import AppKit
import FontPocketCore

struct AdvancedDetailsView:View{
    @EnvironmentObject var model:AppModel
    @Environment(\.dismiss) var dismiss
    let face:FontFace
    @State private var report:FontInspection?
    @State private var problem=""
    @State private var query=""
    @State private var range=""
    @State private var page=0
    @State private var axes:[Int:Double]=[:]
    @State private var copied=""
    private var characters:[UInt32]{guard let report else{return []};return report.characters.filter{value in
        if !range.isEmpty,let bounds=FontAnalysis.ranges.first(where:{$0.0==range})?.1,!bounds.contains(value){return false}
        if value<32 || (0x7f...0x9f).contains(value){return false}
        return query.isEmpty || String(format:"U+%04X",value).localizedCaseInsensitiveContains(query) || Unicode.Scalar(value).map{String($0).contains(query)} == true
    }}
    var body:some View{
        VStack(alignment:.leading,spacing:14){
            HStack{Text(face.title).font(.title2.bold());Spacer();Button(model.t("인쇄·PDF","Print / PDF")){FontPrinting.printFonts([face],text:model.previewText)};Button(model.t("닫기","Done")){dismiss()}.keyboardShortcut(.cancelAction)}
            TabView(selection:$model.detailsTab){
                VStack(alignment:.leading,spacing:12){
                    PreviewPane(face:face){EmptyView()}.frame(minHeight:240)
                    if let report,!report.axes.isEmpty{
                        Text(model.t("가변 축 조절","Variable axes")).font(.headline)
                        ForEach(report.axes){axis in HStack{Text(axis.name).frame(width:100,alignment:.leading);Slider(value:Binding(get:{axes[axis.id] ?? axis.defaultValue},set:{axes[axis.id]=$0}),in:axis.minimum...axis.maximum);Text(String(format:"%.1f",axes[axis.id] ?? axis.defaultValue)).monospacedDigit().frame(width:65)}}
                        ScrollView{Text(model.previewText).font(Font(face.variedFont(size:model.previewSize,values:axes))).frame(maxWidth:.infinity,alignment:.leading)}.frame(minHeight:100)
                        Button(model.t("기본값 복원","Reset axes")){axes=[:]}
                    }
                }.padding().tag("preview").tabItem{Text(model.t("미리보기","Preview"))}
                VStack{
                    HStack{TextField(model.t("문자 또는 U+AC00 검색","Character or U+AC00"),text:$query).textFieldStyle(.roundedBorder);Picker(model.t("범위","Range"),selection:$range){Text(model.t("전체","All")).tag("");ForEach(FontAnalysis.ranges,id:\.0){Text($0.0).tag($0.0)}}.frame(width:210)}
                    ScrollView{LazyVGrid(columns:[GridItem(.adaptive(minimum:64))],spacing:10){ForEach(Array(characters.dropFirst(page*256).prefix(256)),id:\.self){value in Button{
                        let text=Unicode.Scalar(value).map(String.init) ?? "";NSPasteboard.general.clearContents();NSPasteboard.general.setString(text,forType:.string);copied=String(format:"U+%04X",value)
                    }label:{VStack{Text(Unicode.Scalar(value).map(String.init) ?? "").font(Font(face.previewFont(size:28)));Text(String(format:"%04X",value)).font(.caption2).foregroundStyle(.secondary)}.frame(width:60,height:60)}.buttonStyle(.bordered)}}}
                    HStack{Button("←"){page=max(0,page-1)}.disabled(page==0);Text("\(page+1) / \(max(1,(characters.count+255)/256)) · \(characters.count)");Button("→"){page+=1}.disabled((page+1)*256>=characters.count);Spacer();Text(copied).font(.caption)}
                    Button(model.t("문자표 인쇄·PDF","Print character map / PDF")){FontPrinting.printFonts([face],text:model.previewText,characters:true)}
                }.padding().onChange(of:query){_ in page=0}.onChange(of:range){_ in page=0}.tag("characters").tabItem{Text(model.t("문자표","Characters"))}
                VStack(alignment:.leading){
                    if let report{List{ForEach(FontAnalysis.ranges,id:\.0){item in HStack{Text(item.0);Spacer();let count=report.characters.filter{item.1.contains($0)}.count;Text("\(count) / \(Int(item.1.upperBound-item.1.lowerBound)+1)").monospacedDigit()}}}}
                    Text(model.t("문자 포함 여부는 언어의 모든 조합·문맥 표현 지원을 보장하지 않습니다.","Character coverage does not guarantee every language shaping or contextual behavior.")).font(.caption).foregroundStyle(.secondary)
                }.padding().tag("coverage").tabItem{Text(model.t("지원 범위","Coverage"))}
                List{if let report{ForEach(report.metadata.keys.sorted(),id:\.self){key in VStack(alignment:.leading,spacing:5){Text(key).font(.headline);Text(report.metadata[key] ?? "").textSelection(.enabled)}}}}.tag("information").tabItem{Text(model.t("정보","Information"))}
                VStack(alignment:.leading,spacing:12){
                    if let report{Text(model.t("글리프 \(report.glyphs)개 · 문자 \(report.characters.count)개","\(report.glyphs) glyphs · \(report.characters.count) characters"));Text("Tables: "+report.tables.sorted().joined(separator:", ")).textSelection(.enabled)
                        Text(report.issues.isEmpty ? model.t("테이블 범위·체크섬 검사 통과","Table bounds and checksums passed"):report.issues.joined(separator:"\n")).foregroundStyle(report.issues.isEmpty ? Color.primary:Color.red)
                    };Text(problem).foregroundStyle(.red);Text(model.t("모든 렌더링 문제나 악성 코드를 검사하는 기능은 아닙니다.","This does not detect every rendering issue or malicious font.")).font(.caption).foregroundStyle(.secondary);Spacer()
                }.padding().tag("inspection").tabItem{Text(model.t("검사","Inspection"))}
            }
            HStack{Button(model.t("원본·CSS·HTML 저장","Export font / CSS / HTML")){model.exportWeb(face,woff2:false)};Button(model.t("WOFF2 저장","Export WOFF2")){model.exportWeb(face,woff2:true)};Button(model.t("WOFF2·CSS·HTML 저장","Export WOFF2 / CSS / HTML")){model.exportWeb(face,woff2:true,package:true)};Spacer();Text(problem).font(.caption).foregroundStyle(.red)}.disabled(model.busy)
        }.padding(22).frame(width:900,height:700)
        .safeAreaInset(edge:.bottom){if model.busy{TaskControls().environmentObject(model).padding(12)}}
        .task{let task=Task.detached{try FontAnalysis.inspect(face)};do{report=try await withTaskCancellationHandler(operation:{try await task.value},onCancel:{task.cancel()})}catch{problem=error.localizedDescription}}
    }
}

struct QuickSearchView:View{
    @EnvironmentObject var model:AppModel
    @Environment(\.dismiss) var dismiss
    @State private var query=""
    @FocusState private var focused:Bool
    var body:some View{VStack{
        HStack{Image(systemName:"magnifyingglass");TextField(model.t("이름·메모·태그 검색","Search name, notes or tags"),text:$query).textFieldStyle(.plain).font(.title3).focused($focused);Button(model.t("닫기","Done")){dismiss()}.keyboardShortcut(.cancelAction)}.padding()
        List(model.fonts.filter{face in let note=model.personal.annotations[face.id] ?? FontAnnotation();return query.isEmpty || (face.title+" "+face.version+" "+face.format+" "+note.note+" "+note.tags.joined(separator:" ")).localizedCaseInsensitiveContains(query)}.prefix(100)){face in Button{model.search="";model.activeCollection=nil;model.onlyFavorites=false;model.formatFilter="";model.stateFilter="";model.languageFilter="";model.weightFilter="";model.variableOnly=false;model.fixedOnly=false;model.selection=[face.id];model.page = .manager;dismiss()}label:{HStack{Text(face.title);Spacer();Text(face.format).foregroundStyle(.secondary)}}.buttonStyle(.plain).padding(.vertical,5)}
    }.frame(width:660,height:480).onAppear{focused=true}}
}
