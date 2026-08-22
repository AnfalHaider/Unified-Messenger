; Shared Inno Setup constants for Unified Messenger (included by installer.iss / installer-arm64.iss)

#define MyAppName "Unified Messenger"
#define MyAppFolderName "UnifiedMessenger"
#define MyAppExeName "UnifiedMessenger.exe"
#define MyAppPublisher "AnfalHaider"
#define MyAppURL "https://github.com/AnfalHaider/Unified-Messenger"
#define MyAppVersion "4.99.42"
#define MyAppMutex "UnifiedMessenger_AppMutex"

#define OllamaRuntimeDir "{localappdata}\UnifiedMessenger\ollama\runtime"
#define OllamaModelsDir "{localappdata}\UnifiedMessenger\ollama\models"

; Per-user install (no elevation). Binaries only â€” user data stays in %LocalAppData%\UnifiedMessenger.
#define InstallDir "{localappdata}\Programs\UnifiedMessenger"
#define LegacyInstallDir "{localappdata}\UnifiedMessenger"
#define UserDataDir "{localappdata}\UnifiedMessenger"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "uninstallremoveaimodels"; Description: "Remove downloaded AI models (~2 GB+)"; GroupDescription: "Additional uninstall options:"; Flags: unchecked
; Unchecked by default ON PURPOSE: keeping the data means a reinstall picks up where the owner left off,
; with their response-time history and signed-in accounts intact. But leaving it SILENTLY is the problem —
; a measured uninstall left 7.2 GB behind, including oversight-snapshot.json and contact-history.json,
; which hold customer names and message previews. For a product whose whole promise is that customer data
; stays on your machine, "and it stays there after you uninstall, with no way to say otherwise" is not a
; defensible default. This makes the choice visible without changing it.
; NOTE: keep this Description pure ASCII. This .iss is read as ANSI, not UTF-8 - the pre-existing comment
; on the InstallDir line above still shows an em-dash as mojibake ("a EUR ..."), so any non-ASCII character
; in a USER-VISIBLE string would render as garbage in the installer's task list.
Name: "uninstallremovedata"; Description: "Also erase all app data: message history, signed-in accounts and settings"; GroupDescription: "Additional uninstall options:"; Flags: unchecked

[Code]
function IsPreservedRootFile(const FileName: String): Boolean;
var
  Ext: String;
begin
  { Preserve ALL user-data stores. This directory is shared with the legacy install location, so
    CleanAppPayload runs here to strip stale binaries on update — but it must never delete accrued app data.
    Enumerating individual files drifted badly: response-times, contact-history, kpi-trend, awaiting-overrides
    and oversight-snapshot were all being wiped on every update (so First Response Time / SLA never
    accumulated, and cards showed "waiting for first sync" after an update). Any *.json here is app state.

    .log and .bak are preserved for the same reason, and both were being destroyed on every update:
      * app.log / app.old.log are the ONLY diagnostic surface. A user who updates in order to fix a
        problem was wiping the evidence of that problem, and support had nothing to work from.
      * <store>.corrupt-<timestamp>.bak is written when a data file cannot be read, and is the user's
        only route back to their settings. The release notes tell them to look for it — and since
        auto-update is on by default, the next update silently deleted it.
    Stale binaries from the legacy layout are .exe/.dll/.pri/.xbf/.mui and are still removed. }
  Ext := ExtractFileExt(FileName);
  Result :=
    (CompareText(Ext, '.json') = 0) or
    (CompareText(Ext, '.log') = 0) or
    (CompareText(Ext, '.bak') = 0);
end;

function IsPreservedRootDir(const DirName: String): Boolean;
begin
  Result :=
    (CompareText(DirName, 'WebView2') = 0) or
    (CompareText(DirName, 'avatars') = 0) or
    (CompareText(DirName, 'ollama') = 0);
end;

procedure CleanAppPayload(const AppDir: String);
var
  FindRec: TFindRec;
  Path: String;
begin
  if not DirExists(AppDir) then
    Exit;

  if FindFirst(AppDir + '\*', FindRec) then
  try
    repeat
      if (FindRec.Name = '.') or (FindRec.Name = '..') then
        Continue;

      Path := AppDir + '\' + FindRec.Name;

      if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
      begin
        if not IsPreservedRootDir(FindRec.Name) then
          DelTree(Path, True, True, True);
      end
      else if not IsPreservedRootFile(FindRec.Name) then
        DeleteFile(Path);
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

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
  begin
    TaskKill('{#MyAppExeName}');
    TaskKill('ollama.exe');
    CleanAppPayload(ExpandConstant('{app}'));
    if ExpandConstant('{app}') <> ExpandConstant('{#LegacyInstallDir}') then
      CleanAppPayload(ExpandConstant('{#LegacyInstallDir}'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    TaskKill('ollama.exe');
end;

[UninstallDelete]
Type: filesandordirs; Name: "{#OllamaRuntimeDir}"
Type: filesandordirs; Name: "{#OllamaModelsDir}"; Tasks: uninstallremoveaimodels
; Last, and only when explicitly asked for: the whole data root. This subsumes the two entries above
; (ollama lives inside it), so ordering matters — the narrower deletes run first.
Type: filesandordirs; Name: "{#UserDataDir}"; Tasks: uninstallremovedata
