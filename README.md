# Unified Messenger

Native WinUI 3 desktop hub for multiple web messaging accounts (WhatsApp, Telegram, Messenger, and custom URLs) with unified notifications and workspace split.

## Download (Windows)

**Latest installer:** [UnifiedMessengerSetup.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup.exe)

All releases: [github.com/AnfalHaider/Unified-Messenger/releases](https://github.com/AnfalHaider/Unified-Messenger/releases)

Requires Windows 10 1809+ or Windows 11 and the WebView2 Runtime (usually preinstalled on Windows 11).

## Requirements

- Windows 10 1809+ / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- WebView2 Runtime (usually preinstalled on Windows 11)

## Quick start

### Run from source (standalone .exe)

```powershell
cd "d:\Projects\Unified Messenger\UnifiedMessenger"
dotnet build
dotnet run
```

### Visual Studio

Open `UnifiedMessenger.sln` and run the **UnifiedMessenger** profile.

## Build a release installer

### 1. Publish self-contained binaries

```powershell
cd "d:\Projects\Unified Messenger\UnifiedMessenger"

dotnet publish `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:WindowsAppSDKSelfContained=true `
  -p:PublishReadyToRun=false `
  -o "bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
```

For ARM64:

```powershell
dotnet publish `
  -c Release `
  -r win-arm64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:WindowsAppSDKSelfContained=true `
  -p:PublishReadyToRun=false `
  -o "bin\Release\net8.0-windows10.0.19041.0\win-arm64\publish"
```

### 2. Compile the Inno Setup installer

1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php).
2. Update `#define MyAppVersion` in `installer.iss` and `<Version>` in `UnifiedMessenger.csproj` to match your release tag (e.g. `1.0.3`).
3. Compile:

```powershell
cd "d:\Projects\Unified Messenger"
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

Output: `dist\UnifiedMessengerSetup.exe`

### 3. GitHub Release

1. Tag the release (e.g. `v1.0.3`) — version must be newer than the built assembly version.
2. Upload `dist\UnifiedMessengerSetup.exe` as a release asset with that exact filename.
3. On next launch, the app checks `AnfalHaider/Unified-Messenger` releases and silently applies updates when a newer tag is found.

## Auto-update

`GitHubUpdateService` runs on startup (non-blocking). When a newer GitHub release is detected, it downloads `UnifiedMessengerSetup.exe` and runs it with Inno silent flags, then exits so files can be replaced.

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+D | Dashboard |
| Ctrl+, (comma) | Settings |
| Ctrl+Shift+N | Toggle notification panel |
| Ctrl+K | Command palette |
| Ctrl+1–9 | Switch to instance (sidebar order) |

## User data

| Data | Location |
|------|----------|
| Instance registry | `%LocalAppData%\UnifiedMessenger\instances.json` |
| Analytics | `%LocalAppData%\UnifiedMessenger\analytics.json` |
| Settings | `%LocalAppData%\UnifiedMessenger\settings.json` |
| WebView profiles | `%LocalAppData%\UnifiedMessenger\WebView2\` |

## Enhancement roadmap

See [ENHANCEMENT_ROADMAP.md](ENHANCEMENT_ROADMAP.md) for planned features and multi-agent execution batches.
