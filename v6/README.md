# Unified Messenger v6

The Electron + TypeScript rebuild (MASTER-PLAN D-11). The v5 app in `UnifiedMessenger/` keeps shipping
until v6 reaches parity. Roadmap: the "Revamp Blueprint" artifact; Phase 1 results: `docs/revamp/phase-1-proof.md`.

| Folder | Holds |
|---|---|
| `core/` | Pure logic: who is waiting, reply times, SLA, rollups. Every number the UI shows is computed here. |
| `app/` | The Electron shell: one signed-in session per account, the read scheduler, the JSON stores, lost-login logging. |
| `channels/<name>/` | One module per channel: reader, find-a-chat, health check, tests. |
| `ui/` | React screens. |
| `assistant/` | Local Ollama chat. Off by default. |
| `cloud/` | Firebase sign-in, membership and configuration sync. |

Folders appear when their first file does.

```
npm install
npm test               # node --test, runs *.test.ts directly (Node strips the types)
npm run typecheck      # tsc --noEmit
npm start              # opens the window from source and starts reading
npm run smoke          # Playwright: the window renders, navigates and quits (own temp data folder)
npm run dist           # builds dist\UnifiedMessenger6Setup.exe
npm run install-local  # builds it, installs it on this PC and opens the installed app
```

## Installing on this PC

`npm run install-local` is the loop after every change: it rebuilds the screens, packages the app with
`@electron/packager` (no asar, so Electron runs the same TypeScript it runs from source), wraps it with Inno
Setup (`installer.iss`, needs Inno Setup 6), and installs it silently to `%LOCALAPPDATA%\Programs\UnifiedMessenger6`
with Start Menu and desktop shortcuts. A running copy is closed the normal way first, never killed.

The installed app and `npm start` share one data folder, `%APPDATA%\unified-messenger-v6`, named explicitly in
`app/main.ts`, so logins and history carry across every reinstall and survive an uninstall. Only one copy runs
at a time: a second launch brings the first window forward.

Run it from your own terminal. An agent's shell sits in an MSIX sandbox that redirects the install to a private
copy the shortcuts never see; from there, run the built Setup through `Win32_Process` instead.

## Running the shell

`UM_SELFTEST=1 npm start` starts the app, waits for the pages to bring their readers up, reads every readable
account, writes a verdict to `app.log` and ends — the unattended check. `UM_SELFTEST_WAIT` sets the wait in
milliseconds (30 seconds by default); reading sooner only measures how fast WhatsApp Web loads. It exits through
the same shutdown as the close button, so a zero exit code also proves closing works. Electron runs the TypeScript directly, with no build step, which is why `core/` is written to stay
strippable (`erasableSyntaxOnly` in `tsconfig.json`). Adding a syntax that cannot be erased would quietly
require a build.

Data lives in Electron's own user-data folder. Set `UM_DATA` to put it elsewhere — an agent shell **must**,
because it runs inside an MSIX container that silently redirects writes to a private copy, so the app and the
shell would disagree about what is on disk.

**First launch on a PC that already runs v5 imports it**: accounts, locations and settings, plus the history —
the chats that were on screen, the reply-time samples and watch start, and the chats already marked handled or
snoozed. v5's files are only read, never written, and a config already present means it is not a first run, so
this never happens twice. `UM_V5` points at a different v5 folder, which is how it is exercised without a real
install.

**Closing closes the pages first, then quits normally.** A `WebContentsView`'s page is not destroyed with its
window, so shutdown stops the read timer, flushes every session, closes each page and waits for it to be gone,
then calls `app.quit()`. Nothing is killed: an earlier build ended its own process instead, and a page stopped
mid-write left damaged browser storage behind — which cost two WhatsApp logins, and was also what made that
build's quit hang in the first place. With clean storage and all nine pages open, the self-test exits with code
0 about a second after the check.

`app.log` is the file support would ask a customer to send, so it carries counts only: never a name, a number
or message text. An empty read is never reported as a quiet account — the page is asked whether it is signed
out, and a lost login is recorded with what the previous read saw.

The screens are the complete shell from the approved Front Desk renders (`docs/design/v6-front-desk`). The
line, the docked account page, Needs you, the accounts grid, one account's figures, the channel readers and the
reading settings run on real data. Every other screen — set aside, the morning digest, reviews, reports, the
assistant, workspace, owner and the full-window states — shows its final layout with sample figures from
`ui/sample.ts`, and says so on screen, until its feature is wired. Settings › About lists the moment-only
screens so they can be reviewed. Opening `npm run ui` in a browser draws every screen with sample data.

## What is in `core/` today

Each module is a port of the v5 logic named beside it, with v5's own test cases. Nothing here touches the
disk, the network or the clock without being told the time: stores are plain objects that the `app/` layer
saves as JSON, and "now" is always a parameter, which is what makes every rule testable.

| Module | What it decides | Ported from |
|---|---|---|
| `chat-entry` | A channel's scan JSON becomes chat entries; a bad row costs only itself. | `ChatEntryParser` |
| `reply-need` | Whether a customer's last message still needs an answer, and why in plain English. | `ReplyNeed` |
| `snapshot` | The one "is this chat waiting" rule, plus windowed counts, the awaiting split and the digest. | `OversightChatSnapshotService` |
| `rollup` | Per account or per location: caught up, waiting, past target, at risk, worst first. | `OversightRollupBuilder` |
| `response-times` | First response time, measured forward from what we actually see happen. | `ResponseTimeTracker` |
| `awaiting-overrides` | Marked handled or snoozed, both expiring on their own. | `AwaitingOverrideStore` |
| `business-hours` | Elapsed minutes inside a location's working hours. | `BusinessHoursCalculator` |
| `days` | Local calendar days that survive a clock change. | `LocalDayBoundary` |
| `percent` | A percentage that never rounds up to 100 or down to 0. | `MetricMath` |
| `freshness` | How old the numbers are, said the way a person would. | `DataFreshness` |
| `config` | Accounts, locations and settings in one object; parsing never throws. | `AppSettings` + `InstanceRegistryService` |
| `import-v5` | A v5 install becomes a v6 config and its history, with a report of what was guessed or left behind. | — |
| `schedule` | Which account to read next, what may sleep, when to stay quiet. | `InstanceSessionManager` + `OversightAlertMonitor` |
| `history` | One record per account per local day, for reports and the digest. | — |
| `report` | A range of whole days for the Reports screen: replies, traffic, backlog, calls, busy hours. | `BusinessReport` |

Rules: follow the ponytail guideline (built-ins before dependencies, no abstractions without a second
user); port v5 behaviour with its test cases before changing it; never commit `firebase-config.json`,
`oauth-client.json` or any other credential.
