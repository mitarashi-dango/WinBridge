#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif

#ifndef SourceDir
  #error SourceDir must be provided by the packaging script.
#endif

#ifndef OutputDir
  #error OutputDir must be provided by the packaging script.
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "WinBridge-Setup-x64"
#endif

#define AppName "WinBridge"
#define AppPublisher "mitarashi-dango"
#define AppUrl "https://github.com/mitarashi-dango/WinBridge"
#define AppExeName "WinBridge.exe"

[Setup]
AppId={{D933E636-5071-45B1-BC03-59FB87FA34F1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl + "/issues"}
AppUpdatesURL={#AppUrl + "/releases"}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\Resources\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
MinVersion=10.0.22000
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[CustomMessages]
english.DesktopIcon=Create a desktop shortcut
japanese.DesktopIcon=デスクトップにショートカットを作成する
english.AdditionalShortcuts=Additional shortcuts:
japanese.AdditionalShortcuts=追加のショートカット:
english.UninstallShortcut=Uninstall %1
japanese.UninstallShortcut=%1 のアンインストール
english.LaunchProgram=Launch %1
japanese.LaunchProgram=%1 を起動する

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallShortcut,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
