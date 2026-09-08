; Installeur Heure+ — Inno Setup 6
; Compilation : ISCC.exe installer\HeurePlus.iss   (voir installer\build-installer.ps1)

#define AppName "Heure+"
; La version est ecrite par build-installer.ps1 dans version.generated.iss
; (lue depuis <Version> de HeurePlus.csproj). Valeur de secours 0.0.0 si on
; compile l'iss a la main sans avoir lance le script.
#ifexist "version.generated.iss"
  #include "version.generated.iss"
#endif
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define AppPublisher "Heure+"
#define AppExe "HeurePlus.exe"
#define PublishDir "..\src\HeurePlus\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{7C3F9A21-5D48-4B6E-A1C9-0E2F3A4B5C6D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Output
OutputBaseFilename=HeurePlus-Setup
SetupIconFile=..\src\HeurePlus\heureplus.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\LatoFont\*"; DestDir: "{app}\LatoFont"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; La base de donnees de l'utilisateur reste dans %LOCALAPPDATA%\HeurePlus et n'est PAS supprimee.
Type: filesandordirs; Name: "{app}\LatoFont"
