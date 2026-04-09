; InnoSetup script for AI-IDE-Avalonia
; Bundles the published win-x64 single-file executable into a Windows installer.
; Expected to be compiled from the repository root after:
;   dotnet publish AI-IDE-Avalonia/AI-IDE-Avalonia.csproj -c Release -r win-x64 -o publish

#define AppName    "AI-IDE-Avalonia"
#define AppVersion "1.0.0"
#define AppPublisher "YoussefWaelMohamedLotfy"
#define AppURL     "https://github.com/YoussefWaelMohamedLotfy/AI-IDE-Avalonia"
#define AppExeName "AI-IDE-Avalonia.exe"
#define PublishDir "..\publish"

[Setup]
AppId={{E4A7B2C1-3F9D-4E6A-B8C5-1D2E3F4A5B6C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=..\installer-output
OutputBaseFilename=AI-IDE-Avalonia-win-x64-Setup
SetupIconFile={#SourcePath}\..\AI-IDE-Avalonia\Assets\app-icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";    Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
