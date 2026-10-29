import AppKit
import Carbon

@MainActor final class GlobalSearchShortcut{
    private var hotkey:EventHotKeyRef?
    private var handler:EventHandlerRef?
    private weak var model:AppModel?
    init(model:AppModel){
        self.model=model
        var event=EventTypeSpec(eventClass:OSType(kEventClassKeyboard),eventKind:UInt32(kEventHotKeyPressed))
        let context=Unmanaged.passUnretained(self).toOpaque()
        InstallEventHandler(GetApplicationEventTarget(),{_,_,context in
            guard let context else{return OSStatus(eventNotHandledErr)}
            let value=Unmanaged<GlobalSearchShortcut>.fromOpaque(context).takeUnretainedValue()
            Task{@MainActor in NSApp.activate(ignoringOtherApps:true);NSApp.windows.first?.makeKeyAndOrderFront(nil);value.model?.quickSearch=true}
            return noErr
        },1,&event,context,&handler)
        let identifier=EventHotKeyID(signature:0x46504f43,id:1)
        let status=RegisterEventHotKey(UInt32(kVK_Space),UInt32(cmdKey|optionKey),identifier,GetApplicationEventTarget(),0,&hotkey)
        if status != noErr{model.record(model.t("빠른 검색 단축키 등록 실패 · 앱에서 ⌘K를 사용하세요.","Global search shortcut unavailable · use ⌘K inside the app."))}
    }
    deinit{if let hotkey{UnregisterEventHotKey(hotkey)};if let handler{RemoveEventHandler(handler)}}
}
