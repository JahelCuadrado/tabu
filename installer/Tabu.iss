; Tabu — Windows Installer Script (Inno Setup 6)
; Builds TabuSetup.exe from the published single-file Tabu.UI.exe.

#define MyAppName        "Tabu"
#define MyAppVersion     "1.6.1"
#define MyAppPublisher   "Jahel Cuadrado"
#define MyAppURL         "https://github.com/JahelCuadrado/Tabu"
#define MyAppExeName     "Tabu.UI.exe"
#define MyAppId          "{{B5E2A1F4-7C9D-4A5E-9D31-5F0F6C3C1A2B}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\publish-output
OutputBaseFilename=TabuSetup-v{#MyAppVersion}-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
SetupIconFile=..\src\Tabu.UI\Assets\tabu.ico
WizardImageFile=assets\wizard-banner.bmp
WizardSmallImageFile=assets\wizard-small.bmp
WizardImageStretch=no
WizardImageAlphaFormat=none
ShowLanguageDialog=auto
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon";        Description: "{cm:CreateDesktopIcon}";        GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "launchatstartup";    Description: "Launch Tabu when Windows starts"; GroupDescription: "Startup options:"

[Files]
Source: "..\src\Tabu.UI\bin\Release\net8.0-windows\win-x64\publish\Tabu.UI.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional: register at user-level Run key when the user opts in.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "Tabu"; ValueData: """{app}\{#MyAppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: launchatstartup

[Run]
; Interactive install: show "Launch Tabu" checkbox on the final wizard page (checked by default).
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
; Silent install (in-app self-update path): launch Tabu automatically once the new files are in place.
; runasoriginaluser keeps the launched process at the same privilege level as the calling Tabu instance.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait runasoriginaluser; Check: WizardSilent
