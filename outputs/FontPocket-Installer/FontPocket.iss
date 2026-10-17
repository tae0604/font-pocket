#define AppVersion "0.1.0"
#define AppSource "..\FontPocket"

[Setup]
#ifdef QABuild
AppId={{86CD9C20-C610-46D9-AF3C-8143392DCC15}
#else
AppId={{6B48CEDC-7827-4A9A-979C-D2362E472D8D}
#endif
AppName=Font Pocket
AppVersion={#AppVersion}
AppVerName=Font Pocket 0.1.0
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
ChangesAssociations=no
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
korean.DesktopIcon=바탕화면에 바로가기 만들기
english.DesktopIcon=Create a desktop shortcut
korean.Shortcuts=바로가기:
english.Shortcuts=Shortcuts:
korean.LaunchApp=Font Pocket 실행
english.LaunchApp=Launch Font Pocket
korean.NeedFramework=Font Pocket을 실행하려면 .NET Framework 4.8 이상이 필요합니다.%nWindows 업데이트 또는 Microsoft의 .NET Framework 4.8 설치를 완료한 뒤 다시 실행해 주세요.
english.NeedFramework=Font Pocket requires .NET Framework 4.8 or later.%nInstall it through Windows Update or Microsoft, then run Setup again.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:Shortcuts}"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\FontPocket"; ValueType: string; ValueName: "UILanguage"; ValueData: "{code:AppLanguage}"; Check: NotQABuild

[Files]
Source: "{#AppSource}\FontPocket.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppSource}\사용방법.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppSource}\FontPocket.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppSource}\Brand\Fonts\Pretendard-LICENSE.txt"; DestDir: "{app}\Licenses"; Flags: ignoreversion

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
// Neither path is installed by this script or removed by the uninstaller.
// Do not add wildcard UninstallDelete rules: preserve user-created files too.
