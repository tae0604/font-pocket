#define AppVersion "0.1.3"
#ifndef AppSource
#define AppSource "..\FontPocket"
#endif

[Setup]
#ifdef QABuild
AppId={{86CD9C20-C610-46D9-AF3C-8143392DCC15}
#else
AppId={{6B48CEDC-7827-4A9A-979C-D2362E472D8D}
#endif
AppName=Font Pocket
AppVersion={#AppVersion}
AppVerName=Font Pocket {#AppVersion}
AppPublisher=Font Pocket
AppComments=폰트 다운로드 및 관리
DefaultDirName={localappdata}\Programs\Font Pocket
DefaultGroupName=Font Pocket
PrivilegesRequired=lowest
MinVersion=10.0.18362
DisableProgramGroupPage=yes
DisableWelcomePage=yes
DisableDirPage=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
WizardStyle=modern
; The native launcher owns the live language picker and supplies /LANG.
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage
UsePreviousLanguage=no
SetupIconFile={#AppSource}\FontPocket.ico
WizardSmallImageFile={#AppSource}\FontPocket-installer.bmp
WizardImageFile={#AppSource}\FontPocket-wizard.bmp
UninstallDisplayIcon={app}\FontPocket.ico
UninstallDisplayName=Font Pocket
Compression=lzma2
SolidCompression=yes
#ifndef QABuild
OutputDir=..
OutputBaseFilename=FontPocket-Setup-{#AppVersion}
#endif
VersionInfoVersion={#AppVersion}
VersionInfoProductName=Font Pocket
VersionInfoDescription=Font Pocket 설치
CloseApplications=yes
CloseApplicationsFilter=FontPocket.exe
RestartApplications=no
Uninstallable=yes
ChangesAssociations=yes
ChangesEnvironment=no
#ifdef QABuild
; Isolated build: same payload/install/uninstall code without user registration.
CreateUninstallRegKey=no
UninstallFilesDir={app}
OutputDir=..\..\work\installer-qa
OutputBaseFilename=FontPocket-Setup-QA
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[CustomMessages]
korean.FontTypeLegacy=글꼴 파일
english.FontTypeLegacy=Font file
korean.FontTypeTTF=TrueType 글꼴
english.FontTypeTTF=TrueType font
korean.FontTypeOTF=OpenType 글꼴
english.FontTypeOTF=OpenType font
korean.FontTypeTTC=TrueType 글꼴 모음
english.FontTypeTTC=TrueType font collection
korean.FontTypeOTC=OpenType 글꼴 모음
english.FontTypeOTC=OpenType font collection
korean.FontTypeSFNT=SFNT 글꼴
english.FontTypeSFNT=SFNT font
korean.FontTypeWOFF=WOFF 웹 글꼴
english.FontTypeWOFF=WOFF web font
korean.FontTypeWOFF2=WOFF2 웹 글꼴
english.FontTypeWOFF2=WOFF2 web font
korean.DesktopIcon=바탕화면에 바로가기 만들기
english.DesktopIcon=Create a desktop shortcut
korean.Shortcuts=바로가기:
english.Shortcuts=Shortcuts:
korean.LaunchApp=Font Pocket 실행
english.LaunchApp=Launch Font Pocket
korean.NeedFramework=Font Pocket을 실행하려면 .NET Framework 4.8 이상이 필요합니다.%nWindows 업데이트 또는 Microsoft의 .NET Framework 4.8 설치를 완료한 뒤 다시 실행해 주세요.
english.NeedFramework=Font Pocket requires .NET Framework 4.8 or later.%nInstall it through Windows Update or Microsoft, then run Setup again.
korean.SaveLanguageFailed=선택한 앱 언어를 저장하지 못했습니다.
english.SaveLanguageFailed=Could not save the selected app language.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:Shortcuts}"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\FontPocket"; ValueType: string; ValueName: "UILanguage"; ValueData: "{code:AppLanguage}"; Check: NotQABuild

; Font file handlers: register choices without overwriting Windows defaults.
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font"; ValueType: string; ValueName: ""; ValueData: "{cm:FontTypeLegacy}"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"",0"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"" --open ""%1"""; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\Applications\FontPocket.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Font Pocket"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\Applications\FontPocket.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"" --open ""%1"""; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "Font Pocket"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Preview, install and manage fonts with Font Pocket."; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: """{app}\FontPocket.exe"",0"; Check: NotQABuild
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "Font Pocket"; ValueData: "Software\FontPocket\Capabilities"; Flags: uninsdeletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.ttf\OpenWithProgids"; ValueType: string; ValueName: "FontPocket.Font.TTF"; ValueData: ""; Flags: uninsdeletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\Applications\FontPocket.exe\SupportedTypes"; ValueType: string; ValueName: ".ttf"; ValueData: ""; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ttf"; ValueData: "FontPocket.Font.TTF"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.otf\OpenWithProgids"; ValueType: string; ValueName: "FontPocket.Font.OTF"; ValueData: ""; Flags: uninsdeletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\Applications\FontPocket.exe\SupportedTypes"; ValueType: string; ValueName: ".otf"; ValueData: ""; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities\FileAssociations"; ValueType: string; ValueName: ".otf"; ValueData: "FontPocket.Font.OTF"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.ttc\OpenWithProgids"; ValueType: string; ValueName: "FontPocket.Font.TTC"; ValueData: ""; Flags: uninsdeletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\Applications\FontPocket.exe\SupportedTypes"; ValueType: string; ValueName: ".ttc"; ValueData: ""; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ttc"; ValueData: "FontPocket.Font.TTC"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.otc\OpenWithProgids"; ValueType: string; ValueName: "FontPocket.Font.OTC"; ValueData: ""; Flags: uninsdeletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\Applications\FontPocket.exe\SupportedTypes"; ValueType: string; ValueName: ".otc"; ValueData: ""; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities\FileAssociations"; ValueType: string; ValueName: ".otc"; ValueData: "FontPocket.Font.OTC"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.sfnt\OpenWithProgids"; ValueType: string; ValueName: "FontPocket.Font.SFNT"; ValueData: ""; Flags: uninsdeletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\Applications\FontPocket.exe\SupportedTypes"; ValueType: string; ValueName: ".sfnt"; ValueData: ""; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities\FileAssociations"; ValueType: string; ValueName: ".sfnt"; ValueData: "FontPocket.Font.SFNT"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.woff\OpenWithProgids"; ValueType: string; ValueName: "FontPocket.Font.WOFF"; ValueData: ""; Flags: uninsdeletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\Applications\FontPocket.exe\SupportedTypes"; ValueType: string; ValueName: ".woff"; ValueData: ""; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities\FileAssociations"; ValueType: string; ValueName: ".woff"; ValueData: "FontPocket.Font.WOFF"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.woff2\OpenWithProgids"; ValueType: string; ValueName: "FontPocket.Font.WOFF2"; ValueData: ""; Flags: uninsdeletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\Applications\FontPocket.exe\SupportedTypes"; ValueType: string; ValueName: ".woff2"; ValueData: ""; Check: NotQABuild
Root: HKCU; Subkey: "Software\FontPocket\Capabilities\FileAssociations"; ValueType: string; ValueName: ".woff2"; ValueData: "FontPocket.Font.WOFF2"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.TTF"; ValueType: string; ValueName: ""; ValueData: "{cm:FontTypeTTF}"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.TTF\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"",0"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.TTF\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"" --open ""%1"""; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.ttf\OpenWithProgids"; ValueType: none; ValueName: "FontPocket.Font"; Flags: deletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.OTF"; ValueType: string; ValueName: ""; ValueData: "{cm:FontTypeOTF}"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.OTF\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"",0"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.OTF\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"" --open ""%1"""; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.otf\OpenWithProgids"; ValueType: none; ValueName: "FontPocket.Font"; Flags: deletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.TTC"; ValueType: string; ValueName: ""; ValueData: "{cm:FontTypeTTC}"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.TTC\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"",0"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.TTC\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"" --open ""%1"""; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.ttc\OpenWithProgids"; ValueType: none; ValueName: "FontPocket.Font"; Flags: deletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.OTC"; ValueType: string; ValueName: ""; ValueData: "{cm:FontTypeOTC}"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.OTC\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"",0"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.OTC\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"" --open ""%1"""; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.otc\OpenWithProgids"; ValueType: none; ValueName: "FontPocket.Font"; Flags: deletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.SFNT"; ValueType: string; ValueName: ""; ValueData: "{cm:FontTypeSFNT}"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.SFNT\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"",0"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.SFNT\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"" --open ""%1"""; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.sfnt\OpenWithProgids"; ValueType: none; ValueName: "FontPocket.Font"; Flags: deletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.WOFF"; ValueType: string; ValueName: ""; ValueData: "{cm:FontTypeWOFF}"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.WOFF\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"",0"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.WOFF\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"" --open ""%1"""; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.woff\OpenWithProgids"; ValueType: none; ValueName: "FontPocket.Font"; Flags: deletevalue; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.WOFF2"; ValueType: string; ValueName: ""; ValueData: "{cm:FontTypeWOFF2}"; Flags: uninsdeletekey; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.WOFF2\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"",0"; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\FontPocket.Font.WOFF2\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FontPocket.exe"" --open ""%1"""; Check: NotQABuild
Root: HKCU; Subkey: "Software\Classes\.woff2\OpenWithProgids"; ValueType: none; ValueName: "FontPocket.Font"; Flags: deletevalue; Check: NotQABuild

[Files]
Source: "{#AppSource}\FontPocket.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppSource}\Microsoft.Web.WebView2.*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppSource}\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppSource}\WebAssets\*"; DestDir: "{app}\WebAssets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppSource}\Licenses\*"; DestDir: "{app}\Licenses"; Flags: ignoreversion
Source: "{#AppSource}\사용방법.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppSource}\FontPocket.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppSource}\Brand\Fonts\Pretendard-LICENSE.txt"; DestDir: "{app}\Licenses"; Flags: ignoreversion
Source: "{#AppSource}\LICENSE"; DestDir: "{app}\Licenses"; DestName: "FontPocket-MIT.txt"; Flags: ignoreversion

#ifndef QABuild
[Icons]
Name: "{autoprograms}\Font Pocket"; Filename: "{app}\FontPocket.exe"; WorkingDir: "{app}"; IconFilename: "{app}\FontPocket.ico"
Name: "{autodesktop}\Font Pocket"; Filename: "{app}\FontPocket.exe"; WorkingDir: "{app}"; IconFilename: "{app}\FontPocket.ico"; Tasks: desktopicon
#endif

[Run]
Filename: "{app}\FontPocket.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\FontPocket.exe"; Flags: nowait; Check: IsAppUpdate

[Code]
function NotQABuild(): Boolean;
begin
#ifdef QABuild
  Result := False;
#else
  Result := True;
#endif
end;

function AppLanguage(Param: String): String;
begin
  if ActiveLanguage = 'english' then Result := 'en' else Result := 'ko';
end;

function IsAppUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:FONTPOCKETUPDATE|0}') = '1';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  PreferenceFolder: String;
begin
  if CurStep <> ssPostInstall then Exit;
#ifdef QABuild
  PreferenceFolder := ExpandConstant('{app}\qa-language');
#else
  PreferenceFolder := ExpandConstant('{localappdata}\FontPocket');
#endif
  // The app reads this file before the legacy registry preference. Update both
  // stores so an explicit Setup choice also replaces a previous app setting.
  if not ForceDirectories(PreferenceFolder) then
    RaiseException(CustomMessage('SaveLanguageFailed'));
  if not SaveStringToFile(PreferenceFolder + '\ui-language.txt', AppLanguage(''), False) then
    RaiseException(CustomMessage('SaveLanguageFailed'));
end;

function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM32,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);
  if Result then Result := Release >= 528040;
  if not Result then
    MsgBox(CustomMessage('NeedFramework'), mbError, MB_OK);
end;

procedure InitializeWizard();
begin
  WizardForm.WizardSmallBitmapImage.Width := ScaleX(48);
  WizardForm.WizardSmallBitmapImage.Height := ScaleY(48);
  WizardForm.WizardSmallBitmapImage.Left := WizardForm.MainPanel.ClientWidth - WizardForm.WizardSmallBitmapImage.Width - ScaleX(18);
  WizardForm.WizardSmallBitmapImage.Top := (WizardForm.MainPanel.ClientHeight - WizardForm.WizardSmallBitmapImage.Height) div 2;
  WizardForm.PageNameLabel.Width := WizardForm.WizardSmallBitmapImage.Left - WizardForm.PageNameLabel.Left - ScaleX(12);
  WizardForm.PageDescriptionLabel.Width := WizardForm.WizardSmallBitmapImage.Left - WizardForm.PageDescriptionLabel.Left - ScaleX(12);
end;

// User data lives in LocalAppData\FontPocket and fonts in Windows\Fonts.
// Only the app language preference is written here; user fonts and other data
// are not installed or removed by this script.
// Do not add wildcard UninstallDelete rules: preserve user-created files too.
