# Third-party notices

Unified Messenger is not affiliated with, endorsed by, or sponsored by WhatsApp, Meta, Google, or any
other service it embeds. All product names and logos are the property of their respective owners.

## Runtime dependencies

| Package | License |
|---|---|
| [Windows App SDK / WinUI 3](https://github.com/microsoft/WindowsAppSDK) | MIT |
| [Microsoft.Web.WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) | Microsoft Software License Terms |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT |
| [OllamaSharp](https://github.com/awaescher/OllamaSharp) | MIT |
| [H.NotifyIcon.WinUI](https://github.com/HavenDV/H.NotifyIcon) | MIT |
| [xUnit](https://github.com/xunit/xunit) (tests only) | Apache-2.0 |
| [FlaUI](https://github.com/FlaUI/FlaUI) (UI smoke tests only) | MIT |
| [Inno Setup](https://jrsoftware.org/isinfo.php) (installer tooling) | Inno Setup License |

## Techniques adapted from open-source projects

No third-party code is vendored into this repository. The projects below were **read** for technical
knowledge about how WhatsApp Web and multi-service desktop clients work, and the resulting
implementations were written from scratch against this codebase's own constraints (read-only, local-only,
never-send).

| Project | License | What was adapted |
|---|---|---|
| [wppconnect-team/wa-js](https://github.com/wppconnect-team/wa-js) | Apache-2.0 | The technique of reaching WhatsApp Web's in-page webpack module registry to read its in-memory model collections, rather than parsing rendered DOM or the encrypted persisted store. Our `whatsapp-store-bridge.js` is an independent, read-only implementation of that idea — no bundle is shipped or copied. |
| [pedroslopez/whatsapp-web.js](https://github.com/pedroslopez/whatsapp-web.js) | Apache-2.0 | The `moduleRaid` style of module discovery (pushing a synthetic webpack chunk to obtain the require function) used as one of our fallback discovery strategies. |
| [devlikeapro/waha](https://github.com/devlikeapro/waha) | Apache-2.0 | The session-lifecycle state model (`STARTING → SCAN_QR → WORKING → FAILED`), adapted into our `SessionState` projection with an added `Degraded` state. |
| [Evolution API](https://github.com/evolution-foundation/evolution-api) | Apache-2.0 | The shape of a normalized per-channel event payload, adapted into our in-process `IChannelEvent` / `ChannelEventBus`. No HTTP or webhook transport is used. |
| [Ferdium](https://github.com/ferdium/ferdium-app) / [ferdium-recipes](https://github.com/ferdium/ferdium-recipes) | Apache-2.0 / MIT | The "service recipe" model for defining an embedded web service, and user-added custom websites as first-class tabs. |

Deliberately **not** used: Baileys, whatsmeow, and other unofficial WhatsApp protocol reimplementations.
They carry a real risk of the user's account being banned, so this app only ever drives the official web
clients inside WebView2.

GPL/AGPL-licensed clients (e.g. Telegram Web A/K, mautrix) are treated as **reference-only**: they may be
read to understand a platform's behaviour, but no code from them is copied or adapted into this project.

## Fonts and icons

Icon glyphs are from the Windows **Segoe Fluent Icons** / **Segoe MDL2 Assets** system fonts, used under
the Windows font licensing terms. Platform brand colours are used nominatively to identify each service.
