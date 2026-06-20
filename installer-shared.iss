; Shared Inno Setup constants for Unified Messenger (included by installer.iss / installer-arm64.iss)

#define MyAppName "Unified Messenger"
#define MyAppFolderName "UnifiedMessenger"
#define MyAppExeName "UnifiedMessenger.exe"
#define MyAppPublisher "AnfalHaider"
#define MyAppURL "https://github.com/AnfalHaider/Unified-Messenger"
#define MyAppVersion "4.26.0"
#define MyAppMutex "UnifiedMessenger_AppMutex"

#define OllamaRuntimeDir "{localappdata}\UnifiedMessenger\ollama\runtime"
#define OllamaModelsDir "{localappdata}\UnifiedMessenger\ollama\models"

; Per-user install (no elevation). Binaries only — user data stays in %LocalAppData%\UnifiedMessenger.
#define InstallDir "{localappdata}\Programs\UnifiedMessenger"
#define LegacyInstallDir "{localappdata}\UnifiedMessenger"
#define UserDataDir "{localappdata}\UnifiedMessenger"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "uninstallremoveaimodels"; Description: "Remove downloaded AI models (~2 GB+)"; GroupDescription: "Additional uninstall options:"; Flags: unchecked

[Code]
function IsPreservedRootFile(const FileName: String): Boolean;
begin
  Result :=
    (CompareText(FileName, 'settings.json') = 0) or
    (CompareText(FileName, 'instances.json') = 0) or
    (CompareText(FileName, 'analytics.json') = 0) or
    (CompareText(FileName, 'triage_v2.json') = 0);
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
