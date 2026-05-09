; Last.fm Scrobbler — Inno Setup script
; Compile via build.ps1, or manually:
;   ISCC.exe /DAppVersion=1.2.0 setup.iss

#ifndef AppVersion
  #define AppVersion "1.1.100"
#endif

#define AppName      "Last.fm Scrobbler"
#define AppPublisher "dagkanbayramoglu"
#define AppURL       "https://github.com/dagkanbayramoglu/lastfm-scrobbler-apple-music-windows"
#define AppExeName   "LastFmScrobbler.exe"

[Setup]
AppId={{B4F2A9D1-7E3C-4A8F-9B2D-6C5E0A1F3D82}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\LastFmScrobbler
DefaultGroupName={#AppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=.
OutputBaseFilename=LastFmScrobbler-Setup-{#AppVersion}
SetupIconFile=..\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName}
; Allow silent installs (used by the in-app auto-updater)
CloseApplications=yes
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";  Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupentry"; Description: "Start with Windows";      GroupDescription: "System integration:";  Flags: unchecked

[Files]
Source: "..\portable\LastFmScrobbler.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}";    Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";        Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{autostartup}\{#AppName}";        Filename: "{app}\{#AppExeName}"; Tasks: startupentry

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
