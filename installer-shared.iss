; Shared Inno Setup constants for Unified Messenger (included by installer.iss / installer-arm64.iss)

#define MyAppName "Unified Messenger"
#define MyAppFolderName "UnifiedMessenger"
#define MyAppExeName "UnifiedMessenger.exe"
#define MyAppPublisher "AnfalHaider"
#define MyAppURL "https://github.com/AnfalHaider/Unified-Messenger"
#define MyAppVersion "4.84.0"
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

[Code]
function IsPreservedRootFile(const FileName: String): Boolean;
begin
  { Preserve ALL user-data JSON stores. This directory is shared with the legacy install location, so
    CleanAppPayload runs here to strip stale binaries on update — but it must never delete accrued app data.
    Enumerating individual files drifted badly: response-times, contact-history, kpi-trend, awaiting-overrides
    and oversight-snapshot were all being wiped on every update (so First Response Time / SLA never
    accumulated, and cards showed "waiting for first sync" after an update). Any *.json here is app state. }
  Result := (CompareText(ExtractFileExt(FileName), '.json') = 0);
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
