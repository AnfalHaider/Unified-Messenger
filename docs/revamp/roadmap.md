# Unified Messenger v6 roadmap

Updated 2026-09-14. Since the shell and installer session: 2.1, 3.2, 4.1–4.10 and Reports following the title bar's
location filter, each installed on the owner's PC. **Next step: 4.11** (the customer panel), unless an owner decision
in §5 changes the order. This replaces the roadmap section of the "Revamp Blueprint" artifact wherever the two
disagree; the blueprint's stack, rules and data model still stand.

v6 is Electron + TypeScript + React, in `v6/`. v5 (WinUI 3 / .NET) still lives in `UnifiedMessenger/` but is
uninstalled from the owner's PC and only kept as reference until Phase 7 retires it.

---

## 1. Where things stand

| Phase | State |
|---|---|
| 0 · Decisions | Done. Rules changed in AGENTS.md; Firebase Spark only; ponytail code. |
| 1 · Proof build | Done (`docs/revamp/phase-1-proof.md`). |
| 2 · Foundation | Done, including the Playwright smoke test on Windows in CI. |
| 3 · Channel modules | WhatsApp, WhatsApp Business and Instagram read live, and Open chat goes to the conversation (3.2). Open: Google reviews reader, WhatsApp IndexedDB fallback, the deliberate break test. |
| 4 · Screens | Done: 4.1 Handled and Snooze, 4.2 Set aside, 4.3 notifications, 4.4 opening hours and holidays, 4.5 daily history, 4.6 and 4.6b Reports with the weekly report and exports (all following the location filter), 4.7 morning digest, 4.8 missed calls and whether they were returned, 4.9 accounts add / edit / remove, 4.10 the reading record behind the lost-login and reader screens. Open: 4.11 customer panel, 4.12 accessibility. Remaining sample screens are marked. |
| 5 · Assistant | Not started (settings screen and chat screen exist as sample). |
| 6 · Cloud and membership | Not started (sign-in, members, owner, suspended screens exist as sample). Firebase project `unified-messenger-5549a` exists. |
| 7 · Ship v6 | Local installer done and in use. Auto-update, cookie encryption, upgrade flow, v5 retirement and AGENTS.md rewrite open. |
| 8 · After launch | Not started. |

### What works on the owner's PC today

- **Installed** per-user at `%LOCALAPPDATA%\Programs\UnifiedMessenger6`, Start Menu and desktop shortcut "Unified Messenger". v5 is uninstalled; its data folder `%LOCALAPPDATA%\UnifiedMessenger` was kept.
- **Data** in `%APPDATA%\unified-messenger-v6` (config, snapshot, reply times, overrides, `alerts.json`, `history.json`, `exports.json`, `digest.json`, `calls.json`, `events.json`, `app.log`, one `Partitions\<account id>` per login). Shared by the installed app and `npm start`; survives reinstall and uninstall.
- **Working day:** the morning digest on the first opening of each day; Handled and Snooze on the line and the dock; Set aside with Put back; Open chat goes to the conversation (WhatsApp opens it, Instagram filters Direct and stops).
- **Reports** on recorded days, following the title bar's location filter: day records began 2026-09-13, reply times imported from v5 reach further back, and the coverage sentence says both. The weekly report saves as PDF, CSV or image; the Monday auto-save is off.
- **Opening hours** can be edited per location and day, with holidays. All three locations carry v5's 11 am to 9 pm, Monday to Saturday, switched off, so waits still count around the clock (§5.3).
- **Notifications** appear (Windows lists the app as `UnifiedMessenger.v6`) outside the quiet hours imported from v5, 9 pm to 11 am.
- **Reading:** WhatsApp F-11 and Men DHA-2 (500 chats each), Instagram DHA-2 and F-11 (15 threads each), every minute. Google profiles are signed in but have no reader.
- **Needs signing in by hand:** DHA-2 WhatsApp (QR code) and Men DHA-2 Instagram (password). Neither had a valid session in v5.
- **Closing** hides the window to the tray and keeps reading (Settings › Look and reading › Closing the window). Tray menu: Open, Read every account now, Quit.
- **Theme:** Match Windows, Light, Dark, applied to the whole window and the account pages.

### What is real and what is sample in the shell

| Real data | Sample figures (marked "Sample figures, not connected yet") |
|---|---|
| The line, lanes, queue, J/K/Enter | |
| Handled and Snooze (buttons, H / S) on the line and the dock | Customer panel: history, tags, note, saved replies, suggested replies |
| Set aside, with Put back | Privacy sizes |
| Needs you | |
| Accounts grid, account figures | Reviews |
| Channel readers list | |
| Look and reading settings, closing, memory, notifications and quiet hours | Assistant screen and settings |
| Reports: Overview, Reply times, Backlog and reopened, Missed calls (Today, 7 and 30 days) | Workspace members, owner console, sign-in, new PC, removed, suspended, upgrade, update, offline |
| Weekly report, PDF / CSV / image export, Monday auto-save; Export (CSV) on every report tab; all of it follows the title bar's location filter | |
| Open chat from the line, the dock, reports, the digest and notifications | |
| Command palette (customers, accounts, screens) | |
| Theme | |
| Opening hours per location and day, holidays | |
| Morning digest, once a day | |
| Missed calls: returned or not, how and how soon; the not-returned alert | |
| Add, edit (name, location, counted) and remove accounts | |
| The reading record: the lost-login timeline and the reader timeline | |

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
npm test            # 269 tests
npm run smoke       # 11 Playwright tests on invented data: shell, marks, alerts, reports, Open chat,
                    # weekly report and exports, opening hours, digest, location filter, accounts,
                    # the reading record
```

Read every Playwright summary line: it prints `N failed` above `N passed`, so `tail -1` shows a red run as green
(lesson `v6-tail-one-hides-playwright-failures`).

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

An install is only done when all three hold, because each has failed silently once:
1. The `Create` call's `ReturnValue` is 0. An 8 with no process id means Smart App Control blocked the unsigned Setup (lesson `v6-smart-app-control-block-can-lift`): tell the owner and retry the same file later.
2. `app.log` shows `quitting`, `quit`, then a new `startup`. No `startup` means the installer gave up waiting for a process named `UnifiedMessenger6.exe`: look for a main process (no `--type=` in its command line) older than the last `quit` (lesson `v6-quit-can-leave-a-husk-that-blocks-install`).
3. Both WhatsApp accounts log `read` with 500 chats after the new `startup`. A `startup` followed by no `read` or `reader-not-ready` at all means reads are stalled (lesson `v6-one-silent-page-stopped-every-read`): look for `read-failed`, `page-gone` or `page-load-failed`.

On the first launch of a local day the app opens on the morning digest, not the line (lesson `v6-digest-opens-first-on-a-new-day`).

Nested quoting inside `Win32_Process` command lines breaks easily. For anything beyond one command, write a `.ps1` into `D:\um-scratch` and run it with `powershell -NoProfile -ExecutionPolicy Bypass -File`.

---

## 3. The code in one page

```
v6/
  core/        pure logic ported from v5, with v5's test cases; no Electron, no files
  channels/    one folder per channel: page script, scan expression, sign-in probe, parser that never throws
  app/         main.ts (window, sessions, read timer, tray, shutdown, IPC), view-model.ts, first-run.ts, store.ts, preload.cjs
  ui/          React screens: App.tsx (shell, routing), screens/*.tsx, parts.tsx, charts.tsx, icons.tsx, tokens.css, sample.ts,
               print.tsx (the hidden page a weekly report is saved from)
  tests/       smoke.spec.ts (Playwright on Electron) and fixtures/ (invented WhatsApp and Instagram pages)
  assets/      icon.ico, logo.png (from v5)
  scripts/     dist.mjs (installer build)
  installer.iss
```

- **Rules that span files:** a location's hours come from `hoursFor(config, location)`, never `location.hours`, or holidays stop counting. Anything main computes for a screen with the title bar's location filter reads `ctx.scope` (`reportAccounts(config, scope)`). Counts shown on screen come from the whole queue, never from `state.queue`, which is cut to 60 rows. Page calls in a read go through `pageAnswer` (30 s limit).

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

**3.2 Open a specific chat.** Done 2026-09-13, verified on the owner's live pages. Every dock navigation carries the conversation key; main takes name and number from the snapshot and asks the page for a step every 700 ms, up to 20 s (`focusChat`, logged as `focus` with arrived / not-found / replaced / no-target, never the customer).
- WhatsApp (`channels/whatsapp/whatsapp-focus.js`) opens the chat: a drawn row whose title is exactly the name or carries the number gets the pointer sequence; a chat not drawn is opened with WhatsApp's own `Cmd.openChatBottom({ chat })` from `ChatCollection.get(key)`; done only when the open chat's header matches. Live: drawn 3.6 s, not drawn 3.5 s, unsaved number 8.5 s, both accounts. v5's typed search no longer filters the list (lesson `v6-whatsapp-search-ignores-typed-text`).
- Instagram (`channels/instagram/instagram-focus.js`) goes to Direct, focuses the search box, types the name and stops; it never opens a thread (a Seen, and the unread that marks waiting, would go). Live on both signed-in accounts; the reader still reads on Direct (more threads there than on the feed). A title made only of emoji now gives way to the `@handle`.
- Tests: `channels/focus.test.ts` holds both scripts away from message boxes, typing (WhatsApp) and thread links (Instagram); a Playwright test drives both against invented pages in `tests/fixtures`.

**3.3 WhatsApp IndexedDB fallback.** Port v5's IndexedDB scan (`whatsapp-adapter.js`, bounded `chat` `getAll`) as a second scan the module uses when the store bridge reports `no-store` for longer than a few minutes on a signed-in page. Done when a deliberately disabled bridge still yields chats.

**3.4 Break test.** With `UM_DATA` pointing at a copy of the data, replace one module's `scan` with a throwing expression and run `UM_SELFTEST=1`; confirm the other channels still log `read` and the broken one shows "Not reading" in Accounts. Record it in `v6/channels/README.md`. Done when observed, not assumed.

### Phase 4 · Screens: wire the sample screens

**4.1 Handled and Snooze.** Done 2026-09-13. IPC `mark-handled`, `snooze`, `put-back` (main takes `lastActivity` from
the snapshot, never the screen; the log names the account only). Buttons and H / S work on the line and in the dock;
the dock moves on to the next customer. `QueueRow.key` carries the conversation key. The Playwright test proves both
marks leave the line and survive a restart; snooze expiry is covered by the core tests and the 5-second push.
`put-back` has no button yet: that is 4.2.

**4.2 Set aside.** Done 2026-09-13. `core/snapshot.setAside()` lists the owner's marks (still-waiting chats only) and the rule's closures, each once, newest move first; marks now carry `at` (when made; v5 imports have none). "Moved by" says You or Automatic until Phase 6 adds members. Put back only on marks; a closure comes back by turning the rule off. Playwright checks the rows and that Put back returns a chat to the line.

**4.3 About-to-breach notifications.** Done 2026-09-13. `core/alerts.ts` decides, on every 5-second push rather than per read: near target (2 min before, location open via `isOpen` in `core/business-hours.ts`), waited over an hour (only as the hour is crossed), and account signed out; quiet hours hold them back without using them up; more than 3 of a kind become one counting toast. Shown ids live in `alerts.json` (forgotten after 2 days), so a restart never repeats one. Toasts have Open chat and Snooze 1 hour buttons and name the customer and account, never the message. The toast app id `UnifiedMessenger.v6` is set in `main.ts` and on both installer shortcuts; a run from `npm start` has no such shortcut, so Windows may not show its toasts. Settings › Notifications switches the three alerts and edits quiet hours; reader-stopped, low-star review and missed-call alerts say "Not connected yet". Playwright proves exactly one alert across passes and a restart, and that the open message lands in the dock.

**4.4 Opening hours and holidays editor.** Done 2026-09-14. `BusinessHours` gains `week` (seven day windows, 0 = Sunday, null closed) and `closedDates`; hours v5 wrote (one window, working days) still work, and hours that can never open count as off. `config.holidays` holds name, date and locations (empty = all); `hoursFor(config, location)` merges them into closed dates, and every wait, alert and backlog figure goes through it. Settings › Opening hours edits each location's switch and day windows (15-minute steps), copies hours to every location, and adds and removes holidays; main runs each change through `parseConfig` before saving. Playwright proves a closed day and a holiday stop a 30-minute wait at 0 and that switching hours off brings it back, all saved in `config.json`. Not built: a closed afternoon (holidays are whole days), quiet hours "also on holidays", and hours that run past midnight (a day closing after midnight has to close at 23:45).

**4.5 History store for reports.** Built 2026-09-13. `core/history.ts` keeps one record per account per local day (`YYYY-MM-DD`): customers who wrote or called (also by local hour, for the busy-hours chart), first replies measured with median and within-target (target in force that day), waiting over a day at the first read of the day, reopened (answered, then waiting again), missed calls. Only activity after the account came under watch counts; identities are kept for today and yesterday only to de-duplicate; records pruned after 400 days. Main records after every read into `history.json`; a failure there is logged as `history-failed` and never touches the read. Tests pin New York across the autumn change. Reports start from the install unless v5 history is imported (see 4.6). On the owner's PC records appeared for all four read accounts and survived a normal restart; a full week is still to be observed.

**4.6 Reports.** Four tabs done 2026-09-13. `core/report.ts` builds a range of whole local days (Today, 7, 30): reply measures from the response-times samples against each account's target, traffic, reopened, missed calls and the morning backlog from `history.json`, busy hours averaged per weekday, the previous equal range for up/down notes; null wherever nothing was measured, and a coverage sentence naming when the day records begin and, when reply times imported from v5 reach further back, when those begin (`repliesSince`). The view model builds it only while Reports is open, plus the backlog list and unanswered missed calls from the snapshot. Since 2026-09-14 Reports, the weekly report and the exports follow the title bar's location filter (`set-scope`, `ctx.scope`), and export file names carry the location. `LineChart` draws gaps for null days. Custom range was dropped. Whether a missed call was returned is 4.8. Playwright seeds invented history and replies and checks every tab. **Open decision for this step:** v5 kept per-day history that could fill the charts from before the install: `analytics.json` (messages sent and received per day per account, plus one lifetime received-by-hour count, `MessageAnalyticsService.InstanceMessageStats`) and `kpi-trend.json` (per-day waiting count and caught-up %, `KpiTrendStore`), both still in the owner's v5 data folder. Their measures differ from `core/history.ts` (messages, not customers), so either import them as a clearly labelled "before v6" series or leave them.

**4.6b Weekly report and export.** Done 2026-09-13. Weeks run Monday to Sunday (`weekEnding` in `core/report.ts`); the tab offers Last week and This week and opens on the one with data. `weeklyDoc` in `view-model.ts` computes the title, lede, four facts, "What to look at" and "What went well" from the week's report; nothing is phrased by a model. Includes (figures, locations, accounts, missed calls, names) and Monday auto-save live in `settings.weeklyReport`; names and auto-save are off by default. PDF and image are made from the same `WeeklyDocument` component drawn in a hidden window (`ui/print.tsx`, `#print=weekly`), sized to the page; the PDF is one tall page, the image goes to the clipboard. CSV (`reportCsv`) is figures only, one row per account per recorded day, never names; the Export button on the other tabs saves the chosen range. Auto-save writes last week's PDF to Documents › Unified Messenger reports from Monday 10 am, once per week (`weeklyDue`, `exports.json`), skipping a week with nothing recorded. Playwright checks the text, the three files (PDF header, PNG size, CSV row, no names) and the names switch; `UM_EXPORT_DIR` replaces the save dialog and clipboard in tests, so the dialog, the clipboard copy and the Monday save have not been exercised by a test.

**4.7 Morning digest.** Done 2026-09-14. `core/digest.ts` splits the waiting customers (inside the backlog line, closed-by-rule excluded) into still owed, wrote before the location's last closing (`lastClosing` in `core/business-hours.ts`) or before midnight when hours are off, and wrote since. Yesterday by location (on time, median, 14-day trend) comes from `buildReport` over the history store. The view model builds it only while the digest is open, with computed sentences ("Good morning. 4 customers wrote while you were closed."). Main opens it the first time the window is shown on a local day, at start or back from the tray, when `settings.morningDigest` is on (default) and an account is read; `digest.json` remembers the day, so a second opening goes to the line. Settings › Notifications › Summaries switches it. Playwright checks the owed row, the count since, Open chat, and that a second launch the same day opens on the line. The Playwright helper turns the digest off for every other test.

**4.8 Missed calls.** Done 2026-09-14. `core/calls.ts` writes each inbound missed call down while it is the chat's latest message (`calls.json`, keys and times only, a first read reaches back 7 days, kept 31) and marks it returned when a later read shows our own message or call after the call time, recording how (message or call) and when; a customer writing or calling again is not a return. Main records after every read (a failure there is `calls-failed` and never touches the read). Reports › Missed calls lists the range's calls with Returned / Not returned, how and how soon, the median time to return, per-location counts, and Open chat on calls not returned; the Missed calls fact and the weekly report's "What to look at" use the same records; the digest says how many calls from the last two days were not returned. A new alert, "A missed call has not been returned", fires once, 30 to 90 minutes after the call (Settings › Notifications, on by default; a call already older when the app opens is not announced). Calls come from the WhatsApp store bridge only; Instagram has no calls. The per-day missed-call count in `history.json` (CSV, weekly figures) is kept as it was. Tests: 7 core, 1 alert; Playwright seeds `calls.json` and checks the tab. Not yet observed: a real call on the owner's accounts.

**4.9 Accounts: add, edit, remove.** Done 2026-09-14. `core/accounts.ts`: `addAccount` (a channel with a reader is counted by default; "Another page" needs an http(s) address; a new location is created, an existing one keeps its spelling and rules), `editAccount` (name, location, and "Count its customers", which is `professional`), `removeAccount` (the location and its hours stay), and `forgetAccount`, which deletes everything stored under the id in place: snapshot, marks, day records, reply times, calls and alert ids. Every change goes through `parseConfig`. IPC `add-account`, `edit-account`, `remove-account` (the old unexposed `wipe` handle is gone); main wakes a new account and the screen docks its page for sign-in; removal wipes the session's storage, forgets the data and saves every store. Dialogs in `overlays.tsx`: Add (six channels), Edit (from each Accounts cell and the account's figures screen), Remove (says the login is wiped, the figures deleted, and where to unlink this PC on the phone). The Accounts grid shows an uncounted account as "Not counted". Logs carry the id and channel only. Playwright adds an account at a new location (`.invalid` address, no real site), renames and uncounts one, removes it, and checks `config.json`, `snapshot.json` and `calls.json`. Not built: reordering accounts, and deleting a location (it stays after its last account goes).

**4.10 Lost-login record and reader timeline.** Done 2026-09-18. `core/events.ts` keeps the last 60 outcomes per account (`events.json`: read, empty, not-ready, signed-out, signed-in, failed, awake, asleep, reload, page-gone, with chat and waiting counts and the reader's stage — no names, numbers or message text, the `app.log` rule, because these lines are on screen). Saved as each one happens, so a sign-out at midnight is still explained in the morning; forgotten with the account by `forgetAccount`. `lostLoginTimeline` shows the reads before the sign-out, the sign-out itself and how long since; `readerTimeline` tells one story across every account on a channel, opening with "N accounts stopped reading" when more than one failed, and saying so when nothing has been read for five minutes. Runs of the same outcome collapse ("12 good reads"). Both screens lost their "Sample figures" marker; the record is reachable for any account from its figures screen ("Reading record"), and the headline follows the record rather than this minute's flags, because just after a restart nothing has been read yet. Tests: 8 core, and a Playwright test that seeds `events.json` and reads both screens. Not built: a support report to save from the reader screen (still disabled).

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
**7.4 Code signing** is a cost decision for the owner, and no longer only cosmetic: on 2026-09-13 Windows Smart App Control on the owner's PC blocked the unsigned Setup for the 4.5 build after allowing three earlier builds, then allowed the same file two hours later (lesson `v6-smart-app-control-block-can-lift`). Unsigned builds may be blocked again at any time.
**7.5 Upgrade from v5** for other customers: the upgrade screen exists; wire it to `first-run.ts`, and add the session import (profile copy for WhatsApp; decrypted cookies to the `value` column for the rest, see lesson `v6-electron-cookies-are-plaintext`).
**7.6 Retire v5:** remove `UnifiedMessenger/`, `UnifiedMessenger.Tests/`, the v5 installers and `build.yml` jobs; move lessons that still apply; rewrite `AGENTS.md` for v6 (stack, commands, gotchas from the `v6-*` lessons). Remove the v5 copies of the reader scripts.
**7.7 Release notes** and a week of clean running on a real v5 machine that upgraded.

### Phase 8 · After launch

Google Business Profile API for complete review history (needs Google approval); voice-note transcription with whisper.cpp; the official Instagram API if Meta approves; Mac then Linux builds; plans and payments.

---

## 5. Decisions waiting on the owner

1. **Auto-update approach** (7.3).
2. **The Co-Authored-By trailer** already on commits 235627d, 824eb60 and faa4e3a (and older ones from other sessions): leave them, or rewrite `main` history with a force-push.
3. **Opening hours:** the editor exists (Settings › Opening hours). On the owner's PC (checked 2026-09-14) all three locations carry v5's hours, 11 am to 9 pm Monday to Saturday, with the switch off, so waits count around the clock; there are no holidays. Switching a location on uses those hours until edited. Only the owner knows whether they are right.
4. **Imported assistant settings:** v5's config came across with the assistant marked enabled (`llama3.2:3b`); v6 ignores it until Phase 5. Decide the default then.
5. **Code signing** (7.4). Smart App Control blocked an unsigned build for two hours on 2026-09-13; until this is decided, an install can be held up with nothing to do but wait.
6. **Alert volume.** A busy Instagram account can raise a notification every minute or so. Keep one per customer, or cap per account (for example one summary every 10 minutes)?
7. **v5's daily history** (4.6): import `analytics.json` and `kpi-trend.json` as a clearly labelled "before v6" series in Reports, or leave Reports starting from 13 September 2026.

## 6. Known limits today

- A WhatsApp read takes the first 500 chats (`__umStartStoreScan(500)`), and Instagram the top 15 threads of Primary; older conversations are not counted.
- Instagram previews are always empty on this route (thread metadata only).
- Notifications fire on the owner's PC (seen 2026-09-13 after 11 am; Windows lists the app as UnifiedMessenger.v6). Quiet hours came across from v5 as 9 pm to 11 am. Instagram counts every unread thread as waiting, so one busy Instagram account produced 13 near-target and 10 hour alerts in about 15 minutes, all for real chats: whether to rate-limit or summarise per account is an owner decision (§5).
- A quit once left the main process alive after logging `quit`, and the installer, which checks for any process with the app's name, then installed nothing without a message (lesson `v6-quit-can-leave-a-husk-that-blocks-install`). Cause unknown. Worth making the installer say when it gives up, and checking `startup` in the log after every install.
- On 2026-09-14 reads stalled for about ten minutes after an install: one account page never answered and reads run one at a time. A 30-second limit per page call now contains it; why the page went silent is unknown (lesson `v6-one-silent-page-stopped-every-read`).
- Reviews is sample data until the Google reader (3.1). Holidays are whole days only, and opening hours cannot run past midnight (4.4).
- Not exercised by any test: the export save dialog, the clipboard copy, the Monday auto-save, and a click on a real Windows notification (the test sends the same message the click would).
- Cookies are stored unencrypted on disk until 7.1.
- The reader scripts exist twice (v5 tree and `v6/channels`); change the v6 copy.
- The design renders (`docs/design/v6-front-desk`) must be served by a plain static server, not Vite.
