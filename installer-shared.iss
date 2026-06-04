; Shared Inno Setup constants for Unified Messenger (included by installer.iss / installer-arm64.iss)

#define MyAppName "Unified Messenger"
#define MyAppFolderName "UnifiedMessenger"
#define MyAppExeName "UnifiedMessenger.exe"
#define MyAppPublisher "AnfalHaider"
#define MyAppURL "https://github.com/AnfalHaider/Unified-Messenger"
#define MyAppVersion "1.0.8"
#define MyAppMutex "UnifiedMessenger_AppMutex"

; Per-user install (no elevation). Matches ApplicationPaths.UserDataRoot / DefaultInstallRoot.
#define InstallDir "{localappdata}\UnifiedMessenger"
