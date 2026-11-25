; Observer Control Center — per-user Windows installer.
; Unsigned preview installers may still trigger Microsoft SmartScreen.
#ifndef MyAppVersion
  #define MyAppVersion "2.0.7-preview.1"
#endif

#define AppName "Observer Control Center"
#define AppExe "Observer.ControlCenter.exe"

[Setup]
AppId={{F3A0A485-64AE-4CEB-85B7-779FA0CBBE19}
AppName={#AppName}
AppVerName={#AppName} {#MyAppVersion}
AppVersion={#MyAppVersion}
AppPublisher=KingProjects12
AppPublisherURL=https://github.com/KingProjects12/Observer-Control-Center
AppSupportURL=https://github.com/KingProjects12/Observer-Control-Center/issues
AppUpdatesURL=https://github.com/KingProjects12/Observer-Control-Center/releases
DefaultDirName={localappdata}\Programs\Observer Control Center
DefaultGroupName=Observer Control Center
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Observer-Control-Center-Setup-{#MyAppVersion}-win-x64
SetupIconFile=..\Assets\ObserverApp.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=105
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UsePreviousAppDir=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Registry]
Root: HKCU; Subkey: "Software\KingProjects12\ObserverControlCenter"; ValueType: string; ValueName: "Language"; ValueData: "{language}"; Flags: uninsdeletevalue

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Observer Control Center"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\Observer Control Center"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,Observer Control Center}"; Flags: nowait postinstall skipifsilent
