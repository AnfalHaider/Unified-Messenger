# Unified Messenger

Native WinUI 3 desktop hub for multiple web messaging accounts (WhatsApp, Telegram, Messenger, and custom URLs) with unified notifications and workspace split.

## Requirements

- Windows 10 1809+ / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- WebView2 Runtime (usually preinstalled on Windows 11)

## Quick start

### Recommended — packaged run (MSIX identity)

```powershell
cd "d:\Projects\Unified Messenger\UnifiedMessenger"
dotnet build
dotnet run --launch-profile "UnifiedMessenger (Package)"
```

If `dotnet run` fails with package registration errors, use the WinApp launcher:

```powershell
Get-Process UnifiedMessenger -ErrorAction SilentlyContinue | Stop-Process -Force
& "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools.winapp\0.3.2\tools\win-x64\winapp.exe" run ".\bin\Debug\net8.0-windows10.0.26100.0\win-x64" --manifest ".\Package.appxmanifest" --executable UnifiedMessenger.exe
```

### Visual Studio

Open `UnifiedMessenger.sln` and run the **UnifiedMessenger (Package)** profile.

### Unpackaged (limited)

The **UnifiedMessenger (Unpackaged)** profile runs without MSIX. Windows App SDK features (toasts, taskbar badge) may not work fully.

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+D | Dashboard |
| Ctrl+, (comma) | Settings |
| Ctrl+Shift+N | Toggle notification panel |
| Ctrl+1–9 | Switch to instance (sidebar order) |

## User data

| Data | Location |
|------|----------|
| Instance registry | `%LocalAppData%\UnifiedMessenger\instances.json` |
| Analytics | `%LocalAppData%\UnifiedMessenger\analytics.json` |
| Settings | `%LocalAppData%\UnifiedMessenger\settings.json` |
| WebView profiles | `%LocalAppData%\UnifiedMessenger\WebView2\` |

## Enhancement roadmap

See [ENHANCEMENT_ROADMAP.md](../ENHANCEMENT_ROADMAP.md) for planned features and multi-agent execution batches.
