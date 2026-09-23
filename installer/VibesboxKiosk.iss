; Inno Setup script for VibesboxKiosk. Built by .github/workflows/release.yml:
;   iscc /DAppVersion=1.2.0 installer\VibesboxKiosk.iss
; Packs the self-contained publish\ folder (build\publish.ps1).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{6F3C2B1E-8A4D-4C57-9E2B-5D8A7F0C4B19}
AppName=Vibesbox Kiosk
AppVersion={#AppVersion}
AppPublisher=Vibesbox
AppPublisherURL=https://github.com/sofianchitac/VibesboxKiosk
DefaultDirName={autopf}\VibesboxKiosk
DefaultGroupName=Vibesbox Kiosk
DisableProgramGroupPage=yes
; Per-user install by default (no admin needed); the dialog offers all users.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
SetupIconFile=..\src\VibesboxKiosk\VibesboxKiosk.ico
UninstallDisplayIcon={app}\VibesboxKiosk.exe
OutputDir=Output
OutputBaseFilename=VibesboxKiosk-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start the kiosk when I sign in"; GroupDescription: "Startup:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Vibesbox Kiosk"; Filename: "{app}\VibesboxKiosk.exe"
Name: "{autodesktop}\Vibesbox Kiosk"; Filename: "{app}\VibesboxKiosk.exe"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VibesboxKiosk"; ValueData: """{app}\VibesboxKiosk.exe"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\VibesboxKiosk.exe"; Description: "{cm:LaunchProgram,Vibesbox Kiosk}"; Flags: nowait postinstall skipifsilent
