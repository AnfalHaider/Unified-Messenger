; Inno Setup — Unified Messenger ARM64 (unpackaged WinExe)

#include "installer-shared.iss"

#define PublishDir "UnifiedMessenger\bin\Release\net8.0-windows10.0.19041.0\win-arm64\publish"

[Setup]
AppId={{A7B3C4D5-E6F7-4890-ABCD-EF1234567890}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={#InstallDir}
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
OutputBaseFilename=UnifiedMessengerSetup-arm64
OutputDir=dist
SetupIconFile=UnifiedMessenger\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
AppMutex={#MyAppMutex}
RestartApplications=no
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent unchecked

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure TaskKill(const FileName: String);
var
  ResultCode: Integer;
begin
  if Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM /T ' + FileName, '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) then
    Log(Format('taskkill %s exited %d', [FileName, ResultCode]))
  else
    Log(Format('taskkill %s failed', [FileName]));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    TaskKill('{#MyAppExeName}');
end;
