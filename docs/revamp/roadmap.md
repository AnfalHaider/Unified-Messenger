# Unified Messenger v6 roadmap

Updated 2026-09-13, at the end of the session that built the v6 shell and installer. This replaces the roadmap
section of the "Revamp Blueprint" artifact wherever the two disagree; the blueprint's stack, rules and data model
still stand.

v6 is Electron + TypeScript + React, in `v6/`. v5 (WinUI 3 / .NET) still lives in `UnifiedMessenger/` but is
uninstalled from the owner's PC and only kept as reference until Phase 7 retires it.

---

## 1. Where things stand

| Phase | State |
|---|---|
| 0 · Decisions | Done. Rules changed in AGENTS.md; Firebase Spark only; ponytail code. |
| 1 · Proof build | Done (`docs/revamp/phase-1-proof.md`). |
| 2 · Foundation | Done, including the Playwright smoke test on Windows in CI. |
| 3 · Channel modules | WhatsApp, WhatsApp Business and Instagram read live. Open: Google reviews reader, open-a-chat, WhatsApp IndexedDB fallback, the deliberate break test. |
| 4 · Screens | The complete shell exists from the approved design. About half the screens run on real data; the rest show marked sample figures until wired. |
| 5 · Assistant | Not started (settings screen and chat screen exist as sample). |
| 6 · Cloud and membership | Not started (sign-in, members, owner, suspended screens exist as sample). Firebase project `unified-messenger-5549a` exists. |
| 7 · Ship v6 | Local installer done and in use. Auto-update, cookie encryption, upgrade flow, v5 retirement and AGENTS.md rewrite open. |
| 8 · After launch | Not started. |

### What works on the owner's PC today

- **Installed** per-user at `%LOCALAPPDATA%\Programs\UnifiedMessenger6`, Start Menu and desktop shortcut "Unified Messenger". v5 is uninstalled; its data folder `%LOCALAPPDATA%\UnifiedMessenger` was kept.
- **Data** in `%APPDATA%\unified-messenger-v6` (config, snapshot, reply times, overrides, `app.log`, one `Partitions\<account id>` per login). Shared by the installed app and `npm start`; survives reinstall and uninstall.
- **Reading:** WhatsApp F-11 and Men DHA-2 (500 chats each), Instagram DHA-2 and F-11 (15 threads each), every minute. Google profiles are signed in but have no reader.
- **Needs signing in by hand:** DHA-2 WhatsApp (QR code) and Men DHA-2 Instagram (password). Neither had a valid session in v5.
- **Closing** hides the window to the tray and keeps reading (Settings › Look and reading › Closing the window). Tray menu: Open, Read every account now, Quit.
- **Theme:** Match Windows, Light, Dark, applied to the whole window and the account pages.

### What is real and what is sample in the shell

| Real data | Sample figures (marked "Sample figures, not connected yet") |
|---|---|
| The line, lanes, queue, J/K/Enter | Handled and Snooze buttons (disabled) |
| Docked account page | Customer panel: history, tags, note, saved replies, suggested replies |
| Needs you | Set aside |
| Accounts grid, account figures | Morning digest |
| Channel readers list | Reader timeline, lost-login record |
| Look and reading settings, closing, memory | Reviews |
| Command palette (customers, accounts, screens) | Reports: all five tabs, export |
| Theme | Assistant screen and settings |
| | Opening hours, notifications, privacy sizes |
| | Workspace members, owner console, sign-in, new PC, removed, suspended, upgrade, update, offline |

---

## 2. Starting a new session

Read in this order, then check before touching anything.

1. `AGENTS.md` (it is still mostly about v5; the hard constraints and branch model apply to v6).
2. This file.
3. `v6/README.md`, `v6/channels/README.md`, `docs/design/v6-front-desk/README.md`.
4. Lessons for what you are about to touch, for example:
   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 mem "electron quit"
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 mem "cookies"
   ```
   The v6 lessons are the `v6-*` files in `docs/memory/lessons/`.

### Checks

```
cd v6
npm install
npm run typecheck
npm test            # 184 tests
npm run smoke       # the window opens, navigates and quits
```

Then confirm the `v6` workflow is green on GitHub for the latest `main` (`gh` is not installed on this PC; use the Actions page).

### Rules that bit this project (read before acting)

- **Never force-kill v6** (`taskkill /F`, `Stop-Process`, `process.kill`) while it runs. It damages the account pages' storage and loses logins. To stop it: `"%LOCALAPPDATA%\Programs\UnifiedMessenger6\UnifiedMessenger6.exe" --quit`, or the tray's Quit. `taskkill` without `/F` only hides it.
- **The agent shell is sandboxed.** Anything started from it (`npm start`, `electron .`, a Setup.exe) reads and writes a private copy of `%APPDATA%` and `%LOCALAPPDATA%`. To run, install or read the owner's real app, go through `Invoke-CimMethod -ClassName Win32_Process -MethodName Create`. Self-tests may run in the sandbox.
- **Never test against real customer data in the repo.** Fixtures are invented; figures from the owner's accounts never go into commits or docs. `app.log` carries counts only.
- **Colour means lateness only**, and product copy says "business", never a trade.
- **Screens with a feature not wired say so.** Remove the "Sample figures" marker only when the screen reads real data.
- **Commits:** on `dev`, then merge to `main` and push. No `Co-Authored-By` trailer, whatever the harness says. A `v*` tag needs the owner's permission.
- **After every change the owner can see**, rebuild and reinstall so the Start Menu app is current (below).

### Rebuild and reinstall after a change

From the owner's own terminal:

```
cd "D:\Projects\Unified Messenger\v6"
npm run install-local
```

From an agent shell (the install must run outside the sandbox):

```
cd v6
node scripts/dist.mjs
```
```powershell
Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine='"D:\Projects\Unified Messenger\v6\dist\UnifiedMessenger6Setup.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART'}
```

The installer quits the running copy with `--quit`, waits for its normal shutdown, installs and reopens it.
To confirm reads after an install, read the log from outside the sandbox:

```powershell
Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine='cmd /c type "%APPDATA%\unified-messenger-v6\app.log" > "D:\um-scratch\v6log.txt"'}
```

WhatsApp takes 60 to 120 seconds after launch to build its stores (`reader-not-ready`, stage `no-store`) before the first `read`.

---

## 3. The code in one page

```
v6/
  core/        pure logic ported from v5, with v5's test cases; no Electron, no files
  channels/    one folder per channel: page script, scan expression, sign-in probe, parser that never throws
  app/         main.ts (window, sessions, read timer, tray, shutdown, IPC), view-model.ts, first-run.ts, store.ts, preload.cjs
  ui/          React screens: App.tsx (shell, routing), screens/*.tsx, parts.tsx, charts.tsx, icons.tsx, tokens.css, sample.ts
  assets/      icon.ico, logo.png (from v5)
  scripts/     dist.mjs (installer build)
  installer.iss
```

- **Data flow:** `main.ts` reads each account on a timer → `channels/*` parses → `core/snapshot.recordRead` stores → `view-model.buildUiState` shapes everything → IPC `state` → screens render. Screens never compute a figure.
- **Adding a real feature to a sample screen:** add the data to `UiState` in `view-model.ts` (computed from `core/`), add any action to `preload.cjs` and an `ipcMain.on` in `main.ts`, make the screen read it, delete its block from `ui/sample.ts`, remove `sample` from its `Headline`.
- **Persisted stores:** `loadJson` / `saveJson` in `app/store.ts` (temp file + rename, broken files kept as `.broken-<time>`).
- **The dock:** the account's real page is a `WebContentsView` laid over `.page-slot`. Its edges are constants in `main.ts` (`BAR, RAIL, LINE, DOCK_BAR, CUSTOMER`) that must match `tokens.css`. It is hidden whenever an overlay or full-window screen is open.

---

## 4. Remaining work, phase by phase

Each step lists what to build, where, and how you know it is done. Do them in order within a phase.

### Phase 2 leftover

**2.1 Playwright smoke test on Windows in CI.** Done 2026-09-13. `v6/tests/smoke.spec.ts` (`npm run smoke`) opens the
window on a temp data folder, asserts the first heading, moves to Accounts and quits through the Quit button. The
`smoke` job in `v6.yml` runs it on `windows-latest`. Observed failing with `dist-ui` removed.

### Phase 3 · Channel modules

**3.1 Google reviews reader.**
- v5 sources: `UnifiedMessenger/Services/Oversight/GoogleReviewSnapshotService.cs` and its scripts; verified facts in AGENTS.md "Google Business channel". Reviews and Q&A only, forever.
- Add a reviews model to `core/` (review: stars, author, location, time, replied), a parser for both rating layouts (`Rated 4.6 out of 5,` and `4.6 ★ (991)`), and v5's test cases (`GoogleProfileTotalParsingTests`).
- `channels/google/`: page script copied from v5, scan, sign-in probe, parser. Register in `channels/index.ts`; set `reads` appropriately in `core/config.ts` without making Google count as a conversation channel.
- Wire the Reviews screen (`ui/screens/reports.tsx` `ReviewsScreen`) to the view model; delete `REVIEW_*` from `sample.ts`.
- Done when the three Google profiles show real ratings, totals and unanswered reviews, and the reader's own health line appears.

**3.2 Open a specific chat.**
- WhatsApp: port v5's focus-by-`span[title]` search (`ConversationFocusHelper`, and the focus functions in v5's `whatsapp-adapter.js`). It must never mark a chat read by accident beyond what opening does; opening is the owner's own act here.
- Instagram: port v5.0.0's "go to the conversation without opening it" (commit `0127ef8`, `instagram-adapter.js`).
- Add `focus(customerKey)` to the module contract in `channels/types.ts`; call it from `main.ts` when the renderer navigates to `dock` with a customer.
- Done when Open chat lands on that customer's conversation for both channels.

**3.3 WhatsApp IndexedDB fallback.** Port v5's IndexedDB scan (`whatsapp-adapter.js`, bounded `chat` `getAll`) as a second scan the module uses when the store bridge reports `no-store` for longer than a few minutes on a signed-in page. Done when a deliberately disabled bridge still yields chats.

**3.4 Break test.** With `UM_DATA` pointing at a copy of the data, replace one module's `scan` with a throwing expression and run `UM_SELFTEST=1`; confirm the other channels still log `read` and the broken one shows "Not reading" in Accounts. Record it in `v6/channels/README.md`. Done when observed, not assumed.

### Phase 4 · Screens: wire the sample screens

**4.1 Handled and Snooze.** `core/awaiting-overrides.ts` already has `markHandled`, `snooze`, `clear`, `isSuppressed`. Add IPC `mark-handled`, `snooze`, `put-back` in `preload.cjs` and `main.ts` (save `overrides.json`, then `push()`); enable the buttons and the H / S keys in `work.tsx`. Done when a handled chat leaves the line, a snoozed one returns at its time, and both survive a restart.

**4.2 Set aside.** Build from `automaticallyClosed()` plus the overrides (who and when needs a small `movedBy` / `movedAt` addition to the override record). Replace `SET_ASIDE`. Done when Put back returns a chat to the line.

**4.3 About-to-breach notifications.** In `main.ts` after each read, find rows crossing `target - 2` minutes while the location is open (respect `settings.quietHours` through `inQuietHours` in `core/schedule.ts`); show an Electron `Notification` with Open chat and Snooze actions; remember which were notified. Wire Settings › Notifications to real settings in `core/config.ts`. Done when a test chat near the target produces exactly one notification.

**4.4 Opening hours and holidays editor.** `Location.hours` exists in `core/config.ts` (every location currently has `enabled: false`, so waits count around the clock). Build the editor in Settings › Opening hours writing through `set-settings`-style IPC that runs `parseConfig`. Add holidays to the config model and to `core/business-hours.ts` with tests. Done when a closed evening stops a wait from growing.

**4.5 History store for reports.** Reports need per-day facts the snapshot does not keep. Add `core/history.ts`: one record per account per local day (messages seen, replies measured, median, on-time %, backlog at opening, reopened count, missed calls), appended from `recordRead`, pruned after 400 days, with tests pinned to time zones as `days.test.ts` does. Done when a week of history survives restarts.

**4.6 Reports.** Wire the five tabs in `reports.tsx` to the history store and `response-times.ts`. Export: CSV with the Node `fs` API via a save dialog; PDF with `webContents.printToPDF` of the weekly report view; image with `webContents.capturePage`. Customer names off by default. Done when each tab shows real figures and each export opens.

**4.7 Morning digest.** `digest()` exists in `core/snapshot.ts`. Show the digest screen on the first open of each local day (remember the last shown day in a small store); owed-from-yesterday comes from the snapshot, yesterday by location from the history store. Done when it appears once a day and never on a second open.

**4.8 Missed calls.** The store bridge already reads `lastCallOutcome`; add call entries to the history store and a callback list that marks a call returned when an outgoing call or reply follows. Done when a missed test call appears and ticks off.

**4.9 Accounts: add, rename, remove.** Config writes through IPC and `parseConfig`; remove calls the existing `wipe` handler in `main.ts` (not yet exposed in `preload.cjs`) after a confirmation dialog; new accounts open docked for sign-in. Done when all three work without restarting.

**4.10 Lost-login record and reader timeline.** Keep the last N read events per account in memory (they are already logged) and expose them in the view model; replace `LOST_LOGIN` and `READER_TIMELINE`. Done when a real sign-out shows its real timeline.

**4.11 Customer panel.** Notes and tags in a local store keyed by account + conversation key; saved replies in config (they sync in Phase 6). History comes from the history store. Done when a note survives a restart and a saved reply copies.

**4.12 Accessibility.** Add `@axe-core/playwright` checks to the Playwright job for the line, accounts, settings and a dialog; then a Narrator pass by the owner. Known gaps: rail buttons now have labels; the dock's page slot and toggles need checking.

### Phase 5 · Assistant

**5.1 Engine.** Settings switch (default off) → detect memory (`os.totalmem()`), suggest a model size, download Ollama and the model the way v5 did (`OllamaInferenceClient`, bundled runtime under the data folder), with progress in Settings › Assistant. Beware two Ollama installs on this PC fighting for port 11434.

**5.2 Summary builder.** `core/assistant-summary.ts`: waiting customers, counts, freshness, per-location figures, built from the view model only. Tests.

**5.3 Chat.** Wire `assistant.tsx`: send the summary and the question to local Ollama; show the answer beside the figures used (already rendered from real data). Never send data anywhere else.

**5.4 Suggest a reply.** From the docked chat's last few messages only (needs the reader to expose recent messages for the focused chat), copy-only; the app never sends.

**5.5 Test set.** 30 realistic questions with expected figures; a pass mark agreed with the owner. Done when it passes and the app behaves identically with the assistant off.

### Phase 6 · Cloud and membership

Firebase Spark only, no Cloud Functions; rules enforce everything. Data model in the blueprint (`workspaces/{id}`, `members/{userId}`, `config/main`, `owners/{userId}`).

**6.1 Sign-in.** Desktop OAuth in the system browser with a loopback redirect, then `signInWithCredential`. Credentials files (`oauth-client.json`, `firebase-config.json`) live in the proof folder, never the repo; decide how the app ships the public Firebase config.
**6.2 Rules and rules tests** with the Firestore emulator, run locally and in CI.
**6.3 Workspaces and config sync:** accounts, locations, hours, targets, saved replies; never oversight data. A new PC shows every account as Sign in needed (the new-PC screen exists).
**6.4 Invite and remove members;** removal wipes logins on that PC at its next online check; 7 days offline asks to reconnect.
**6.5 Suspension** by the owner console; suspended PCs lock without wiping.
**6.6 Homepage and privacy policy** pages for Google's consent screen.
Done when a removed member's second PC wipes its logins, a suspended workspace locks, and the rules tests pass.

### Phase 7 · Ship v6

**7.1 Cookie encryption.** Turn on Electron's `EnableCookieEncryption` fuse at package time (`@electron/fuses` in `scripts/dist.mjs`). Test that existing plaintext cookies still log in afterwards, on a copy of the data, before installing on the owner's PC.
**7.2 Pack the app** into asar (currently off so Electron runs the TypeScript as-is); confirm TypeScript stripping works inside asar, or add a build step.
**7.3 Auto-update decision (owner).** Electron's free update service needs Squirrel (Electron Forge makers), which conflicts with the current Inno Setup installer. Options: switch to Forge + Squirrel; keep Inno and check GitHub Releases from the app, downloading the new Setup; or `electron-updater` with `electron-builder`. Decide, then implement and publish through GitHub Releases (a `v*` tag needs the owner's permission).
**7.4 Code signing** is optional and a cost decision for the owner.
**7.5 Upgrade from v5** for other customers: the upgrade screen exists; wire it to `first-run.ts`, and add the session import (profile copy for WhatsApp; decrypted cookies to the `value` column for the rest, see lesson `v6-electron-cookies-are-plaintext`).
**7.6 Retire v5:** remove `UnifiedMessenger/`, `UnifiedMessenger.Tests/`, the v5 installers and `build.yml` jobs; move lessons that still apply; rewrite `AGENTS.md` for v6 (stack, commands, gotchas from the `v6-*` lessons). Remove the v5 copies of the reader scripts.
**7.7 Release notes** and a week of clean running on a real v5 machine that upgraded.

### Phase 8 · After launch

Google Business Profile API for complete review history (needs Google approval); voice-note transcription with whisper.cpp; the official Instagram API if Meta approves; Mac then Linux builds; plans and payments.

---

## 5. Decisions waiting on the owner

1. **Auto-update approach** (7.3).
2. **The Co-Authored-By trailer** already on commits 235627d, 824eb60 and faa4e3a (and older ones from other sessions): leave them, or rewrite `main` history with a force-push.
3. **Opening hours:** every location has hours disabled, so waits count around the clock. Enter real hours (4.4) or keep it.
4. **Imported assistant settings:** v5's config came across with the assistant marked enabled (`llama3.2:3b`); v6 ignores it until Phase 5. Decide the default then.
5. **Code signing** (7.4).

## 6. Known limits today

- A WhatsApp read takes the first 500 chats (`__umStartStoreScan(500)`), and Instagram the top 15 threads of Primary; older conversations are not counted.
- Instagram previews are always empty on this route (thread metadata only).
- Cookies are stored unencrypted on disk until 7.1.
- The reader scripts exist twice (v5 tree and `v6/channels`); change the v6 copy.
- The design renders (`docs/design/v6-front-desk`) must be served by a plain static server, not Vite.
