; Unified Messenger 6 installer. Built by `npm run dist` (scripts/dist.mjs), which packages the app into
; out\ first and passes the version in. Per-user install, no admin prompt.
;
; It never touches the data folder (%APPDATA%\unified-messenger-v6): logins, history and settings survive
; every reinstall and the uninstall too. A running copy is closed the normal way before files are replaced,
; because killing it with account pages open damages their saved sessions.

#define AppName "Unified Messenger"
#define ExeName "UnifiedMessenger6.exe"
#ifndef AppVersion
  #define AppVersion "6.0.0"
#endif

[Setup]
; Its own id, not v5's: the two are different apps and must not uninstall each other.
AppId={{E423C8CD-1F8A-42C8-B9F1-4C88204ECCB4}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={localappdata}\Programs\UnifiedMessenger6
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=UnifiedMessenger6Setup
SetupIconFile=assets\icon.ico
UninstallDisplayIcon={app}\{#ExeName}
UninstallDisplayName={#AppName}
WizardStyle=modern
; Fast rather than small: this is rebuilt and reinstalled after every change.
Compression=lzma2/fast
SolidCompression=no
CloseApplications=no

[InstallDelete]
; The app's own files are replaced wholesale, so a file removed from the source does not linger.
Type: filesandordirs; Name: "{app}\resources\app"

[Files]
Source: "out\Unified Messenger-win32-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; The app id must match app.setAppUserModelId in app/main.ts: Windows shows an unpackaged app's notifications only
; for an id a Start Menu shortcut carries.
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#ExeName}"; AppUserModelID: "UnifiedMessenger.v6"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; AppUserModelID: "UnifiedMessenger.v6"

[Run]
; Also after a silent install, so installing a new build ends with the app open.
Filename: "{app}\{#ExeName}"; Description: "Open {#AppName}"; Flags: nowait postinstall

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function IsRunning(): Boolean;
var Code: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C tasklist /FI "IMAGENAME eq {#ExeName}" /NH | find /I "{#ExeName}" >NUL', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := Code = 0;
end;

function WaitUntilClosed(Limit: Integer): Boolean;
var Waited: Integer;
begin
  Waited := 0;
  while IsRunning() and (Waited < Limit) do begin
    Sleep(500);
    Waited := Waited + 500;
  end;
  Result := not IsRunning();
end;

{ Asks a running copy to quit through its normal shutdown and waits for it to finish writing. "--quit" reaches
  the running copy; closing its window would only hide it to the tray. taskkill without /F is the fallback for
  a build from before --quit existed, whose window close still quit. }
function CloseRunningApp(): Boolean;
var Code: Integer;
begin
  Result := True;
  if not IsRunning() then exit;
  if FileExists(ExpandConstant('{app}\{#ExeName}')) then begin
    Exec(ExpandConstant('{app}\{#ExeName}'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);
    if WaitUntilClosed(20000) then exit;
  end;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#ExeName}', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := WaitUntilClosed(30000);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if not CloseRunningApp() then
    Result := '{#AppName} is still open. Close it, then run the installer again.';
end;

function InitializeUninstall(): Boolean;
begin
  Result := CloseRunningApp();
  if not Result then
    MsgBox('{#AppName} is still open. Close it, then uninstall again.', mbError, MB_OK);
end;
