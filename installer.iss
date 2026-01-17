; Booky Installer Script for Inno Setup

#define MyAppName "Booky"
#define MyAppVersion "2.3.0"
#define MyAppPublisher "VoidMind"
#define MyAppURL "https://voidmind.io/"
#define MyAppExeName "Booky.exe"

[Setup]
AppId={{B00KY-C0NV-ERTF-0RK1-NDLE00000001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=
OutputDir=installer
OutputBaseFilename=BookySetup-{#MyAppVersion}
SetupIconFile=Assets\book.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\Booky.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\Tools\*"; DestDir: "{app}\Tools"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Context menu for .epub files - "Send to Kindle with Booky"
Root: HKA; Subkey: "Software\Classes\.epub\shell\SendToKindleBooky"; ValueType: string; ValueName: ""; ValueData: "Send to Kindle with Booky"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\.epub\shell\SendToKindleBooky"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKA; Subkey: "Software\Classes\.epub\shell\SendToKindleBooky\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu for .mobi files - "Convert & Send to Kindle with Booky"
Root: HKA; Subkey: "Software\Classes\.mobi\shell\ConvertSendBooky"; ValueType: string; ValueName: ""; ValueData: "Convert && Send to Kindle with Booky"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\.mobi\shell\ConvertSendBooky"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKA; Subkey: "Software\Classes\.mobi\shell\ConvertSendBooky\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
