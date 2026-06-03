; Inno Setup script for Unified Messenger (standalone .exe deployment)
; Compile after `dotnet publish` — see README or release docs for exact CLI.

#define MyAppName "Unified Messenger"
#define MyAppExeName "UnifiedMessenger.exe"
#define MyAppPublisher "AnfalHaider"
#define MyAppURL "https://github.com/AnfalHaider/Unified-Messenger"
#define MyAppVersion "1.0.3"

; Path to the self-contained publish output (adjust Configuration / RID as needed)
#define PublishDir "UnifiedMessenger\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{A7B3C4D5-E6F7-4890-ABCD-EF1234567890}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=UnifiedMessengerSetup
OutputDir=dist
SetupIconFile=UnifiedMessenger\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
