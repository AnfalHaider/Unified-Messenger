# Unified Messenger

A free, fully-local Windows desktop app for running **multiple isolated WhatsApp / WhatsApp Business
Web sessions** in one window — with a command centre that tells a multi-location business owner, at a
glance, **who is still waiting for a reply**.

Everything runs on your own PC. No cloud service, no API keys, no subscription, and no oversight data
ever leaves the machine.

**Current release:** v4.99.58 — see the [changelog](CHANGELOG.md) or the
[Releases page](https://github.com/AnfalHaider/Unified-Messenger/releases).

---

## What it does

Businesses that run several WhatsApp numbers across branches have no way to see whether customers are
actually being answered. Unified Messenger sits on top of the real web clients you already sign into and
answers three questions continuously:

- **Is anyone waiting?** — a live, cross-account list of customers whose last message was theirs.
- **How fast do we reply?** — first-response time, SLA-met %, answered-today, per account and per branch.
- **Which branch is falling behind?** — worst-first ranking, with trends over time.

It is an **oversight tool, not a messaging client**. It watches; it never types, sends, or replies.

## Features

### Multi-account sessions
- Run as many **WhatsApp** and **WhatsApp Business** accounts as you need, side by side.
- Each account gets a fully **isolated browser profile** — separate logins, cookies and storage, so
  accounts can never see each other.
- Group accounts by **location/branch**, and split them into **Professional** and **Personal** scopes.
- Memory-aware session management: background accounts are tiered down and idle ones reaped, so a
  dozen accounts don't cost a dozen browsers' worth of RAM.

### Command centre
- **Caught-up %**, **awaiting reply**, and **messages/day** per account and per branch, worst-first.
- **Needs-reply list** — every waiting customer across every account in one place, with the customer's
  name, phone number and the actual text of their last message. Click one to jump straight into that
  chat in the real WhatsApp Web view.
- **Awaiting is current-state**: a customer waiting since yesterday still shows today. Reading a chat on
  your phone doesn't mark it handled — only an actual reply does.
- **Mark handled** (replied elsewhere) or **snooze** (1h / 4h / tomorrow); both expire on their own so
  the backlog can't be permanently faked.
- **Aging bands** (<15m, 15m–1h, 1–4h, >4h) and a per-account drill-down.

### Response-time and SLA tracking
- Forward-tracked **first response time**, **SLA met %**, and **answered today**.
- **Business-hours aware** — a message that arrives at 11 PM and is answered at 9 AM counts as a fast
  reply, not a ten-hour one.
- **Quiet hours** so overnight alerts stay silent.

### Analytics
- **Activity patterns** by hour of day, day of week or month, filterable by account and date range.
- **Heat map** of your busiest weekday × hour windows, with a plain-language coverage nudge.
- **Week-over-week** comparison and KPI trend sparklines.
- Counts are customer-only — group chats, Status, broadcasts and channels are excluded.

### Google Business reviews
- Review health per location: **rating**, **lifetime review count**, **reply rate**, and how many
  reviews are still **waiting for a response**.
- Click a waiting review to jump to it on the page, briefly highlighted.

### Business report
- A plain-English report over any period with **anomaly detection** (replies slower than last week,
  rising backlog, neglected accounts), comparative call-outs, and a per-account table.
- Export as **Markdown**, **CSV**, or a **PNG** snapshot.
- Optional AI-written headline (see below).

### Notifications
- Unified feed across all accounts, plus Windows toasts, tray icon and taskbar badge.
- Grouped by account, with per-account mute and threshold alerts.

### Local AI (optional)
- On-device summaries and insight strips via **[Ollama](https://ollama.com)** — enable it in
  Settings → AI and pull a model.
- Dashboard insight strips send **aggregate counts only**. Message triage sends the customer name and
  up to 800 characters of the message — to your local Ollama instance, over loopback. Nothing in either
  path leaves the machine.
- Entirely optional: with AI off, everything falls back to deterministic heuristics.

### Browse tabs
- Add **any website** as its own tab with its own isolated sign-in.
- Editable address bar, back/forward/reload, and **Save site** to keep the page you're on as a new tab.
- Real service tabs (WhatsApp, Google Business…) stay pinned to their own site.

### Other
- Command palette (`Ctrl+K`), keyboard shortcuts, light/dark themes.
- One-click **local backup and restore** of all your data to a `.zip`.
- Automatic updates from GitHub Releases, with SHA-256 verification.

## Privacy and constraints

These are architectural guarantees, not settings:

| Guarantee | What it means |
|---|---|
| **Zero oversight data leaves your PC** | No telemetry, no analytics, no crash upload. Metrics, message content, customer identities and AI prompts never go off-box. |
| **The app never sends** | There is no composer and no send path anywhere in the codebase. Automation is read-only scraping. "Open in WhatsApp" focuses the real web client, where *you* type. |
| **No cloud AI** | All inference is local via Ollama. There is no cloud LLM integration. |
| **No unofficial protocol libraries** | No Baileys/whatsmeow-style reimplementations — those risk your account being banned. Only the real web clients, in WebView2. |
| **No accounts or roles** | Single-user, local app. Nothing to sign up for. |

The only network traffic the app itself makes is the GitHub update check. Your browsing inside a tab is
your own traffic, on its own isolated profile.

## How it works

Each account is a real, logged-in web client running in its own **WebView2** profile. Small read-only
scripts are injected into the page to read what is already on screen.

For WhatsApp, oversight data comes from an **in-page store bridge** that reads WhatsApp Web's own
in-memory model collections — the same data the page is rendering, already decrypted. That is what makes
message previews available for *every* chat rather than only the few dozen rows drawn on screen, and what
lets a reply you sent from your phone clear the "waiting" flag quickly.

If a WhatsApp update ever moves those internals, the bridge **fails soft**: it falls back to reading the
persisted IndexedDB store, so your numbers keep working and only preview coverage degrades.
Settings → Data shows which reader is currently live.

## Requirements

- Windows 10 1809+ or Windows 11 (x64 or ARM64)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) — preinstalled on most
  Windows 11 systems
- Optional: [Ollama](https://ollama.com) for local AI features

## Download

| Platform | Installer |
|---|---|
| **x64** | [UnifiedMessengerSetup.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup.exe) |
| **ARM64** | [UnifiedMessengerSetup-arm64.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup-arm64.exe) |

Each release ships a `.sha256` sidecar next to the installer. The app verifies it before applying an
automatic update.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+K` | Command palette |
| `Ctrl+D` | Dashboard |
| `Ctrl+,` | Settings |
| `Ctrl+Shift+N` | Toggle notification panel |
| `Ctrl+Shift+W` | Workspace management |
| `Ctrl+1`–`Ctrl+9` | Switch to account by sidebar position |

## Your data

Everything lives under `%LocalAppData%\UnifiedMessenger\`:

| Data | File |
|---|---|
| Accounts | `instances.json` |
| Settings | `settings.json` |
| Message analytics | `analytics.json` |
| Response times | `response-times.json` |
| Oversight snapshot | `oversight-snapshot.json` |
| KPI trends | `kpi-trend.json` |
| Contact history | `contact-history.json` |
| Handled/snoozed overrides | `awaiting-overrides.json` |
| Browser profiles | `WebView2\` |

Settings → Data & Privacy can back all of this up to a single `.zip`, restore it, or delete it.

## Build from source

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Windows 10/11,
and [Inno Setup 6](https://jrsoftware.org/isinfo.php) if you want installers.

```powershell
dotnet build UnifiedMessenger/UnifiedMessenger.csproj -c Release
```

Publish a shipping binary (the `-p:Platform=x64` matters — the installer packages from
`bin\x64\Release\...`, and without it publish writes elsewhere and the installer silently ships a stale
build):

```powershell
dotnet publish UnifiedMessenger/UnifiedMessenger.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true
```

Compile the installer (ISCC is not on `PATH`; adjust if yours is a machine-wide install):

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

Run the tests:

```powershell
dotnet test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release
```

ARM64: repeat with `-r win-arm64 -p:Platform=ARM64` and `installer-arm64.iss`.

## CI/CD

GitHub Actions (`.github/workflows/build.yml`):

1. **verify** — build + unit tests with a line-coverage floor
2. **package** — publish x64 and ARM64, compile both installers, write SHA-256 sidecars
3. **ui-smoke** — FlaUI harness against the published x64 binary
4. **release** — on a `v*` tag only: attaches both installers and sidecars to a GitHub Release

Pushing to `main` builds and tests but publishes nothing. **Pushing a `v*` tag is what creates a release.**

## Contributing

`AGENTS.md` is the working agreement for this repository — architecture, hard constraints, the build and
release ritual, and a long list of hard-won gotchas. Read it before changing anything.

## Third-party

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Not affiliated with, endorsed by, or sponsored by WhatsApp, Meta, or Google.

## License

No licence has been published for this project yet, so all rights are reserved. Third-party components
keep their own licences — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
