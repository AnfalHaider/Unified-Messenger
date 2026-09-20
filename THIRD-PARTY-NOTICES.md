# Third-party notices

Unified Messenger is not affiliated with, endorsed by, or sponsored by WhatsApp, Meta, Google, or any
other service it embeds. All product names and logos are the property of their respective owners.

## Runtime dependencies

The app is Electron + TypeScript + React. Nothing from `node_modules` ships inside it except the Electron
runtime itself and the two fonts: the screens are bundled by Vite, and the main process uses only Node and
Electron.

| Package | License | |
|---|---|---|
| [Electron](https://github.com/electron/electron) | MIT | the app itself |
| [React](https://github.com/facebook/react) and React DOM | MIT | the screens |
| [Bricolage Grotesque](https://fontsource.org/fonts/bricolage-grotesque) and [Instrument Sans](https://fontsource.org/fonts/instrument-sans) | OFL-1.1 | the two typefaces, bundled so nothing is fetched |
| [Firebase JS SDK](https://github.com/firebase/firebase-js-sdk) | Apache-2.0 | tests only: the rules and sync run against the emulator |
| [Vite](https://github.com/vitejs/vite) and [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react) | MIT | build only |
| [TypeScript](https://github.com/microsoft/TypeScript) | Apache-2.0 | build only |
| [@electron/packager](https://github.com/electron/packager), [@electron/fuses](https://github.com/electron/fuses) | BSD-2-Clause, MIT | packaging |
| [Playwright](https://github.com/microsoft/playwright) and [@axe-core/playwright](https://github.com/dequelabs/axe-core-npm) | Apache-2.0, MPL-2.0 | tests only |
| [firebase-tools](https://github.com/firebase/firebase-tools), [@firebase/rules-unit-testing](https://github.com/firebase/firebase-js-sdk) | MIT, Apache-2.0 | tests and deploys only |
| [Inno Setup](https://jrsoftware.org/isinfo.php) | Inno Setup License | the installer |
| [Ollama](https://github.com/ollama/ollama) | MIT | the assistant, downloaded on the owner's own machine only if they switch it on; never bundled |

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
clients in their own Electron sessions.

GPL/AGPL-licensed clients (e.g. Telegram Web A/K, mautrix) are treated as **reference-only**: they may be
read to understand a platform's behaviour, but no code from them is copied or adapted into this project.

## Fonts and icons

The two typefaces are bundled (OFL-1.1, above), so the app fetches no font. Every icon is drawn in the app's
own SVG (`v6/ui/icons.tsx`). Platform brand colours are used nominatively, to identify each service.
