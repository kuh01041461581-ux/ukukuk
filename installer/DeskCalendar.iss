; DeskCalendar 설치 파일 (Inno Setup). 내 계정 폴더에 설치하므로 관리자 권한이 필요 없다.
#define AppName "DeskCalendar"
#ifndef AppVersion
  #define AppVersion "1.0"
#endif

[Setup]
AppId={{6C1D7A52-3E4B-4F8A-9B0C-DE5C0A1E2024}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=DeskCalendar
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=DeskCalendar-Setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\DeskCalendar.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; Flags: unchecked

[Files]
Source: "..\dist\DeskCalendar\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\DeskCalendar.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\DeskCalendar.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DeskCalendar.exe"; Description: "DeskCalendar 지금 실행"; Flags: nowait postinstall skipifsilent
