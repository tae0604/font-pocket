import AppKit
import FontPocketCore

/// Paginate before starting a native print operation, so neither a long character
/// map nor a many-font catalog is silently clipped at the bottom of a text view.
@MainActor final class FontCatalogPrintView:NSView{
    struct Item{let text:NSAttributedString;let rect:NSRect}
    let items:[Item]
    let pageSize:NSSize
    let pageCount:Int
    override var isFlipped:Bool{true}
    init(items:[Item],pageSize:NSSize,pages:Int){self.items=items;self.pageSize=pageSize;pageCount=pages;super.init(frame:NSRect(origin:.zero,size:NSSize(width:pageSize.width,height:pageSize.height*CGFloat(pages))))}
    required init?(coder:NSCoder){fatalError("Not supported")}
    override func knowsPageRange(_ range:NSRangePointer)->Bool{range.pointee=NSRange(location:1,length:pageCount);return true}
    override func rectForPage(_ page:Int)->NSRect{NSRect(x:0,y:CGFloat(page-1)*pageSize.height,width:pageSize.width,height:pageSize.height)}
    override func draw(_ dirtyRect:NSRect){NSColor.white.setFill();dirtyRect.fill();for item in items where item.rect.intersects(dirtyRect){item.text.draw(with:item.rect,options:[.usesLineFragmentOrigin,.usesFontLeading])}}
}

@MainActor enum FontPrinting{
    static func makeView(_ faces:[FontFace],text:String,characters:Bool=false,layout:String="list",sizes:[Double]=[12,18,28,40],pageSize:NSSize=NSSize(width:540,height:720))throws->FontCatalogPrintView{
        guard !faces.isEmpty else{throw PocketError("Select fonts to print.")}
        let columns=characters ? 3:layout=="two" ? 2:layout=="grid" ? 3:1
        let width=(pageSize.width-20*CGFloat(columns-1))/CGFloat(columns)
        var items:[FontCatalogPrintView.Item]=[],page=0,column=0,y:CGFloat=0
        func append(_ value:NSAttributedString)throws{
            let height=ceil(value.boundingRect(with:NSSize(width:width,height:100000),options:[.usesLineFragmentOrigin,.usesFontLeading]).height)+18
            guard height<=pageSize.height else{throw PocketError("The sample is too long for one catalog tile. Shorten the sample text or choose List layout.")}
            if y+height>pageSize.height{column+=1;y=0;if column>=columns{page+=1;column=0}}
            guard page<300 else{throw PocketError("Print up to 300 pages at once. Select fewer fonts or characters.")}
            items.append(.init(text:value,rect:NSRect(x:CGFloat(column)*(width+20),y:CGFloat(page)*pageSize.height+y,width:width,height:height)));y+=height
        }
        let ordered=layout=="family" ? faces.sorted{$0.family==$1.family ? $0.weight<$1.weight:$0.family<$1.family}:faces
        for face in ordered{
            if characters{
                let report=try FontAnalysis.inspect(face)
                // Include every supported code point with its Unicode identifier.
                for start in stride(from:0,to:report.characters.count,by:30){
                    let value=NSMutableAttributedString(string:face.title+"\n",attributes:[.font:NSFont.boldSystemFont(ofSize:13),.foregroundColor:NSColor.black])
                    for code in report.characters.dropFirst(start).prefix(30){value.append(NSAttributedString(string:String(format:"U+%04X  ",code),attributes:[.font:NSFont.monospacedSystemFont(ofSize:10,weight:.regular),.foregroundColor:NSColor.black]));value.append(NSAttributedString(string:(Unicode.Scalar(code).map(String.init) ?? "")+"\n",attributes:[.font:face.previewFont(size:15),.foregroundColor:NSColor.black]))}
                    try append(value)
                }
            }else{
                for size in sizes{
                    // Separate sample sizes can continue on the next column/page.
                    let chunks=Array(text);var start=0
                    while start<chunks.count{
                        var count=min(chunks.count-start,max(1,Int(width/max(6,size*0.65))*max(1,Int((pageSize.height-70)/max(18,size*1.4)))))
                        var value=NSMutableAttributedString(string:"")
                        while true{
                            value=NSMutableAttributedString(string:face.title+" · \(Int(size)) pt\n",attributes:[.font:NSFont.boldSystemFont(ofSize:12),.foregroundColor:NSColor.black])
                            value.append(NSAttributedString(string:String(chunks[start..<start+count])+"\n",attributes:[.font:face.previewFont(size:size),.foregroundColor:NSColor.black]))
                            if value.boundingRect(with:NSSize(width:width,height:100000),options:[.usesLineFragmentOrigin,.usesFontLeading]).height+18<=pageSize.height || count==1{break}
                            count=max(1,count/2)
                        }
                        try append(value);start+=count
                    }
                }
            }
        }
        return FontCatalogPrintView(items:items,pageSize:pageSize,pages:page+1)
    }
    static func printFonts(_ faces:[FontFace],text:String,characters:Bool=false,layout:String="list"){
        do{
            let info=NSPrintInfo.shared.copy() as! NSPrintInfo
            info.leftMargin=36;info.rightMargin=36;info.topMargin=36;info.bottomMargin=36
            let size=NSSize(width:max(300,info.paperSize.width-72),height:max(400,info.paperSize.height-72))
            let view=try makeView(faces,text:text,characters:characters,layout:layout,pageSize:size)
            let operation=NSPrintOperation(view:view,printInfo:info);operation.showsPrintPanel=true;operation.showsProgressPanel=true;operation.run()
        }catch{let alert=NSAlert();alert.messageText="Font Pocket";alert.informativeText=error.localizedDescription;alert.runModal()}
    }
}
