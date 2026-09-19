# Unified Messenger v6 roadmap

Updated 2026-09-14. Since the shell and installer session: 2.1, 3.2, all of Phase 4 and Reports following the title bar's
location filter, all of Phase 5 (the assistant, 30 of 30 on the real model) and 6.1 (Google sign-in), each installed on the owner's PC. **Next step: 6.2** (Firestore rules and their tests), in the release plan below, unless an owner decision
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
| 3 · Channel modules | Done apart from the Google API (3.1b): WhatsApp, WhatsApp Business and Instagram read live, Open chat goes to the conversation (3.2), WhatsApp falls back to its saved chat list (3.3), the break test is observed (3.4), and the Google reviews reader is built (3.1) but Google blocks sign-in inside the app, so it waits on the official API. |
| 4 · Screens | Done: 4.1 Handled and Snooze, 4.2 Set aside, 4.3 notifications, 4.4 opening hours and holidays, 4.5 daily history, 4.6 and 4.6b Reports with the weekly report and exports (all following the location filter), 4.7 morning digest, 4.8 missed calls and whether they were returned, 4.9 accounts add / edit / remove, 4.10 the reading record behind the lost-login and reader screens, 4.13 the line's second pass, 4.11 the customer panel, 4.12 accessibility (automated; the owner's Narrator pass is open). Remaining sample screens are marked. |
| 5 · Assistant | Not started (settings screen and chat screen exist as sample). |
| 6 · Cloud and membership | 6.1 sign-in done. Members, owner and suspended screens are still sample. Firebase project `unified-messenger-5549a`. |
| 7 · Ship v6 | Local installer done and in use. Auto-update, cookie encryption, upgrade flow, v5 retirement and AGENTS.md rewrite open. |
| 8 · After launch | Not started. |

### What works on the owner's PC today

- **Installed** per-user at `%LOCALAPPDATA%\Programs\UnifiedMessenger6`, Start Menu and desktop shortcut "Unified Messenger". v5 is uninstalled; its data folder `%LOCALAPPDATA%\UnifiedMessenger` was kept.
- **Data** in `%APPDATA%\unified-messenger-v6` (config, snapshot, reply times, overrides, `alerts.json`, `history.json`, `exports.json`, `digest.json`, `calls.json`, `events.json`, `customers.json`, `app.log`, one `Partitions\<account id>` per login). Shared by the installed app and `npm start`; survives reinstall and uninstall.
- **Working day:** the morning digest on the first opening of each day; Handled and Snooze on the line and the dock; Set aside with Put back; Open chat goes to the conversation (WhatsApp opens it, Instagram filters Direct and stops).
- **Reports** on recorded days, following the title bar's location filter: day records began 2026-09-13, reply times imported from v5 reach further back, and the coverage sentence says both. The weekly report saves as PDF, CSV or image; the Monday auto-save is off.
- **Opening hours** can be edited per location and day, with holidays. All three locations carry v5's 11 am to 9 pm, Monday to Saturday, switched off, so waits still count around the clock (§5.3).
- **Notifications** appear (Windows lists the app as `UnifiedMessenger.v6`) outside the quiet hours imported from v5, 9 pm to 11 am.
- **Reading:** every minute. The Google reviews reader (3.1) found all three Google profiles on Google's "Choose an account" page on 2026-09-19: none is signed in on v6 yet, so the owner must sign in to each. - **All six accounts reading (2026-09-19):** the owner signed in DHA-2 WhatsApp, Men DHA-2 WhatsApp (which had lost its login after 2026-09-15) and Men DHA-2 Instagram on the new build. WhatsApp reads 500 chats per account, Instagram 15–17 threads.
- **Closing** hides the window to the tray and keeps reading (Settings › Look and reading › Closing the window). Tray menu: Open, Read every account now, Quit.
- **Theme:** Match Windows, Light, Dark, applied to the whole window and the account pages.

### What is real and what is sample in the shell

| Real data | Sample figures (marked "Sample figures, not connected yet") |
|---|---|
| The line, lanes, queue, J/K/Enter | |
| Handled and Snooze (buttons, H / S) on the line and the dock | Suggested replies and review drafts (they arrive with the assistant, Phase 5) |
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
| The line's indicators, per-channel counts and click-to-open chart | |
| Customer panel: note, tags, what the reads have seen, saved replies | |
| Not a customer: the owner's mark and the staff-name and team-number rules | |
| Help: a page per screen (? or F1) and the Help screen | |
| Reviews: rating, total and the latest reviews per Google profile (built; waiting on the owner's Google sign-in to show real figures) | |

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
npm test            # 299 tests
npm run smoke       # 19 Playwright tests on invented data (plus one skipped: npm run help:shots): shell, marks, alerts, reports, Open chat,
                    # weekly report and exports, opening hours, digest, location filter, accounts,
                    # the reading record, the line, the customer panel, WCAG 2.1 AA on every
                    # main screen and a dialog in light and dark
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
- **After every change the owner can see**, rebuild and reinstall so the Start Menu app is current (below), and update that screen's help page in `v6/help/`, refreshing the pictures with `npm run help:shots`.

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

## Release plan (owner, 2026-09-19)

Finish everything that remains, **ship v6 to v5's users as an update**, let them use it, then connect Google's API and
send a second update. In order:

1. **3.3** WhatsApp IndexedDB fallback, **3.4** break test.
2. **Phase 5**, the assistant.
3. **Phase 6**, cloud and membership. Its browser sign-in (6.1) and homepage and privacy pages (6.6) are what Google's
   API needs too.
4. **3.1b** the Google reviews API reader, built and switched off until Google approves access.
5. **Phase 7**, ship: the first v6 update to v5 users. Needs the owner's decisions on auto-update (7.3) and code signing
   (7.4) by then.
6. **At the end, with the owner:** the Google Cloud setup in [`google-api-checklist.md`](google-api-checklist.md). The
   owner signs in; Claude configures, asking before each change. Then the second update switches the API reader on.

Google blocks sign-in inside the app's pages ("Couldn't sign you in. This browser or app may not be secure"),
which is why reviews move to the API. Pretending to be Chrome was discussed and set aside: every customer would
meet the same block, and a disguise Google closes breaks all of them at once.

## 4. Remaining work, phase by phase

Each step lists what to build, where, and how you know it is done. Do them in order within a phase.

### Phase 2 leftover

**2.1 Playwright smoke test on Windows in CI.** Done 2026-09-13. `v6/tests/smoke.spec.ts` (`npm run smoke`) opens the
window on a temp data folder, asserts the first heading, moves to Accounts and quits through the Quit button. The
`smoke` job in `v6.yml` runs it on `windows-latest`. Observed failing with `dist-ui` removed.

### Phase 3 · Channel modules

**3.1 Google reviews reader.** Built and tested on invented pages 2026-09-19; **live figures wait on the owner signing in the three Google profiles**, which the first live read found on Google's "Choose an account" page. Google is not a ChannelModule (it has no conversations): `channels/google/google-reviews.js` is v5's reader, rule for rule — Reply buttons are unanswered and Edit buttons answered; stars are the leading run of the first star's **colour**, because every star is the same glyph; the page size is raised to 50 once, and only once a control exists; only unanswered reviews are expanded, and only once; no paging, because v5's paging inflated ~239 reviews to 2,000. The merchant view's text and "Rated" labels go back to `core/reviews.ts parseProfile`, which pairs the rating with the total (both layouts, "4.6 ★ (991)" and "435 Google reviews"), with v5's test cases on invented names. Main reads each profile every 30 minutes (rating and total every 6 hours, which takes the page to Google Search and back), never while its page is on screen, one at a time, off the chat reads' flag; a failed or signed-out profile is tried again after 10 minutes, and a page on Google's sign-in is left there. `reviews.json` (names and text stay on the PC); `app.log` gets counts, the rating and total, and the page's host and path on a failure. Reviews screen is real: one card per profile (rating, total, the latest reviews' spread, how many without a reply), unanswered worst-first then oldest, All recent, full text, Open on Google; unanswered reviews are all one tone, because colour means lateness only. Tests: 8 core; Playwright reads invented Google pages (every test launch now points Google's two addresses at `tests/fixtures/google`, so no test can reach Google) and runs axe on the screen, which found and fixed star labels on unlabelled spans and the amber text at 4.31:1 (`--due` light #8A6208 → #845E08). Not built: an unhappy-review notification, Q&A, and drafted replies (Phase 5).

**3.1b Google reviews through the official API.** Decided with the owner 2026-09-19, after Google refused sign-in inside
the app's page. Desktop OAuth in the system browser (loopback, PKCE; shared with 6.1), scope `business.manage`, the
token encrypted with `safeStorage` on the PC, `accounts.locations.reviews.list` every half hour into the existing
`core/reviews.ts` records and Reviews screen (every review, not the latest 50). No cost; quota 0 until Google approves
the project, then 300 a minute. Built and switched off until approval; the Google Cloud setup is done with the owner at
the end, from `docs/revamp/google-api-checklist.md`. The page reader from 3.1 stays as the fallback it already is.

**3.2 Open a specific chat.** Done 2026-09-13, verified on the owner's live pages. Every dock navigation carries the conversation key; main takes name and number from the snapshot and asks the page for a step every 700 ms, up to 20 s (`focusChat`, logged as `focus` with arrived / not-found / replaced / no-target, never the customer).
- WhatsApp (`channels/whatsapp/whatsapp-focus.js`) opens the chat: a drawn row whose title is exactly the name or carries the number gets the pointer sequence; a chat not drawn is opened with WhatsApp's own `Cmd.openChatBottom({ chat })` from `ChatCollection.get(key)`; done only when the open chat's header matches. Live: drawn 3.6 s, not drawn 3.5 s, unsaved number 8.5 s, both accounts. v5's typed search no longer filters the list (lesson `v6-whatsapp-search-ignores-typed-text`).
- Instagram (`channels/instagram/instagram-focus.js`) goes to Direct, focuses the search box, types the name and stops; it never opens a thread (a Seen, and the unread that marks waiting, would go). Live on both signed-in accounts; the reader still reads on Direct (more threads there than on the feed). A title made only of emoji now gives way to the `@handle`.
- Tests: `channels/focus.test.ts` holds both scripts away from message boxes, typing (WhatsApp) and thread links (Instagram); a Playwright test drives both against invented pages in `tests/fixtures`.

**3.3 WhatsApp IndexedDB fallback.** Done 2026-09-19. `channels/whatsapp/whatsapp-idb.js` wraps the store bridge: when the bridge finds nothing for three minutes on a signed-in page (WhatsApp's `last-wid-md` marker, no QR code), the scan reads WhatsApp's saved chat list (`model-storage`: one bounded `getAll` of `chat`, one of `contact`, never a cursor over `message`), with v5's rules: no groups, broadcasts, status, channels, `0@c.us` or the self-chat; privacy ids resolved through the contact list, and dropped when they have neither a number nor a name; a revoked last message is not waiting; a cold read's "no message" becomes unknown. Same output shape as the bridge, so the parser is unchanged; a read from it logs `"source":"saved-list"`. A signed-out page never falls back (the saved list outlives a sign-out). Weaker by nature: previews are mostly missing (bodies are encrypted at rest) and a phone reply shows once WhatsApp syncs it. Test: a Playwright run on an invented page with no bridge stores at all and a saved list of five chats reads exactly the two waiting customers.

**3.4 Break test.** Done 2026-09-19, observed and kept as the Playwright "break test": an Instagram page whose reader throws beside a WhatsApp page that reads. Every pass fails Instagram and still reads WhatsApp; the line carries on; Accounts, Needs you and the reader screen all name the Instagram reader. It found two defects, both fixed: a broken reader's account showed "0 waiting" (now "not being counted. This is not zero", and the headline counts it as not being read), and Electron's raw error text reached the reader screen (now `plainError` in main: the page did not answer in time / the page has changed / could not be reached; the raw text stays in `app.log`). Run on invented pages, not a copy of the owner's data, because a second app opening the owner's real WhatsApp sessions could disturb their logins. Recorded in `v6/channels/README.md`.

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

**4.13 The line, second pass.** Done 2026-09-18, from the owner's own screenshots. Six indicators above the chart (waiting now with the count per channel, past target, longest wait and who it is, caught up, answered on time, first reply), counted from the rows on screen so the title bar's location filter moves them too. The chart carries each channel: counts per channel in its top bar and in every lane label, and a channel badge on every token. Clicking a token opens that conversation instead of only selecting it. Everyone past an hour is one chip per lane ("N over 1 h, longest 9 h", opening the longest) rather than a pile of tokens whose positions no longer say anything, and the axis ends "over 1 h". A customer saved only as a number gets its last two digits instead of "+3". The list below fills the window (`.screen > .main` takes the height it is given). **Corrected while doing it:** the headline figure labelled "Answered on time" was the rollup's caught-up share (answered ÷ active), not replies against the target — the owner's own screen read "93% answered on time" beside "median first reply 130 minutes". They are now two figures, and the measured one says "—" until something is measured rather than 0%. Tests: a Playwright test for the channels, the chip and both clicks.

**4.11 Customer panel.** Notes and tags in a local store keyed by account + conversation key; saved replies in config (they sync in Phase 6). History comes from the history store. Done when a note survives a restart and a saved reply copies.

**4.15 Not a customer.** Done 2026-09-19, decided with the owner ("mark + rules"). Group chats, broadcasts, status and channels were already left out by the WhatsApp reader; what was missing was one-to-one chats with staff and the team — the owner's own line showed "Bilal Staff Depilex" waiting 166 h. `notCustomerWhy` in `core/snapshot.ts` is the one question every count asks (the line, the split, caught up, reports, alerts, the digest and missed calls): the owner's permanent mark (override kind `excluded`, from **Not a customer** in the dock; unlike Handled it survives new messages and never expires), a whole word in the name (`settings.notCustomers.words`, "Staff" matches "Bilal Staff" but not "Staffordshire"), or one of the team's numbers (`settings.notCustomers.numbers`, compared on the last ten digits, so +92 300…, 0300… and the @c.us key are one number). A chat left out is in no figure at all, not counted as caught up. Set aside lists every one with the reason and a "Not a customer" filter; Put back undoes a mark, a rule is changed in Settings › Look and reading › Not customers. Main's history and call-alert judges now come from the view model's `judgeFor`, so they cannot disagree with the screens. Tests: 6 core, and a Playwright test for the rule, the mark, Set aside, Put back and a number typed with a +92.

**4.16 Help, written once.** Done 2026-09-19, decided with the owner. 18 Markdown pages in `v6/help/`: one per screen, plus Getting started, For the owner, For the front desk, Keyboard and What stays on this PC. `ui/help-index.ts` maps every route to its page; `ui/help.tsx` draws a small fixed Markdown subset as React elements (no HTML is injected, no dependency): headings, lists, bold, `keys`, `[links](help:page)`, `![pictures](shot:name)` and `> notes`. The **?** in the title bar or **F1** opens the current screen's page in a drawer; **Help** in the rail (above Settings) lists every page; the search finds each one. The 22 pictures in `help/shots/` are taken by a Playwright run on invented data (`npm run help:shots`, skipped in an ordinary run), JPEG, about 2 MB together. A unit test fails when a screen has no page, a page is not listed, a link or picture is missing, or a page uses a trade word or "instance"; the Playwright test opens help with F1 and ?, follows a link, opens every page and checks every picture loads; the accessibility test covers the drawer and the Help screen in both themes. **Refresh the pictures after changing a screen** (`npm run help:shots`) and update its page.

### Phase 5 · Assistant

**5.1 Engine.** Done 2026-09-19. Owner decisions: **off until switched on**, **reuse an Ollama already on the PC and download one only when there is none**, never a second Ollama on the same port. `core/assistant.ts` decides (candidates in order: the Ollama the owner installed, `%LOCALAPPDATA%\Programs\Ollama`; v5's bundled copy with its own models folder; the app's own download under the data folder), suggests a model from memory (gemma3:1b below 8 GB, gemma3:4b otherwise; 12b offered, never suggested), reads pull progress and says every state in a sentence. `app/assistant.ts` carries it out: an Ollama already answering is used as it is; otherwise the first installed one is started with `serve` on the settings' port (and stopped at quit; one it did not start is left alone); Ollama itself (v5's pinned v0.30.8, checked against its SHA-256, unpacked with `tar`) and the model (`/api/pull`, with progress) are downloaded only when the owner presses the button in Settings › Assistant, which is real now. The owner's PC has Ollama 0.34 with gemma3:4b already, so it needs no download. Tests: 5 core, and Playwright against a fake Ollama inside the test (off → switched on → model downloaded on request → ready → off) and with none at all (says so, offers the download, starts nothing). Every test launch points the engine's `LOCALAPPDATA` at its own folder (`UM_LOCALAPPDATA`), so no test can start the owner's real Ollama. Not exercised by a test: the real 1.2 GB download.

**5.2 Facts.** Done 2026-09-19, reworked in 5.5. `core/assistant-summary.ts` turns the view model into numbered facts, one sentence each (F1…, and C1… per waiting customer), worded topic first so a small model can find them: waiting now with the count per channel, past target and due soon, the longest wait and who, answered on time, median first reply, caught up, backlog, closed by rule, set aside by kind, every location (waiting, per channel, past target, longest), accounts needing sign-in, readers with problems, and up to 25 waiting customers with their account, wait and last message. Main gives it the whole waiting queue, not the view model's first 60 rows. The model never writes an answer (see 5.5). Tests: 6.

**5.3 Chat.** Done 2026-09-19. The Assistant screen is real: questions (typed or suggested) go through IPC `assistant-ask`, which answers with `answerQuestion` (5.5): the answer is the app's own facts, word for word, beside "At a glance, from the app"; customers the answer names are matched against the queue and offered as Open chat buttons. Nothing is kept: not the question, not the answer, not in the log (which records only that a question was asked and how long it took). A plain message when Ollama is not ready. Test: Playwright against the fake Ollama checks what was sent, the answer, the Open chat button, and that no file holds the question or the answer afterwards.

**5.4 Suggest a reply.** Done 2026-09-19 for WhatsApp. `channels/whatsapp/whatsapp-messages.js` reads the open chat's messages from WhatsApp's own in-memory chat when the owner presses Draft replies (media by name and caption, since a photo's `body` is its thumbnail; system notices skipped). `core/assistant-reply.ts` keeps the newest messages that fit (owner: the whole conversation may be read), asks for two drafts in a fixed form, forbids invented prices, times and promises (placeholders like [price] instead), and parses the answer, keeping one draft when the model ignores the form. The dock's panel is real: Draft replies, two drafts, Copy; the owner sends. Nothing is saved or logged but counts. Instagram: not offered (its messages are not read). Tests: 5 core; Playwright with an invented chat checks exactly what the model received and that nothing is kept.

**5.5 Test set.** Done 2026-09-19: **30 of 30 on gemma3:4b**, the owner's pass mark, and 10 of 10 on reworded questions outside the set. `v6/tests/assistant-set.ts` is one invented day (three locations, ten waiting) and 30 questions, four of them for figures the app does not have (reviews, missed calls, last week, yesterday), whose only right answer is "The app doesn't have that figure."; `core/assistant-grade.ts` grades an answer (whole numbers only, and any number the facts do not contain fails it); `npm run assistant:test` runs the set against the local model the way the app asks and exits 1 below 30. How it got there, each step measured: the model writing its own sentences reached 24 of 30 and misread figures (3 as 5, 74 as 84), so **the model only chooses facts by id and the app shows them word for word** (no invented figure is possible); choosing alone never declined, and offering "none" made it decline answerable questions, so **each chosen fact is checked on its own** ("does this answer the question?", naming both topics first and requiring the very thing asked for), and a fact turned down is **taken off the list and the model chooses again**, up to three rounds (`answerQuestion`, shared by the app and the test run). Narrowing the list by word overlap first made it worse and was dropped. Cost: at least two model calls a question, and up to eight; the 30 took 167 s on the owner's PC, about 5.5 s a question. Off: the Playwright tests run with it off and with no Ollama at all.

### Phase 6 · Cloud and membership

Firebase Spark only, no Cloud Functions; rules enforce everything. Data model in the blueprint (`workspaces/{id}`, `members/{userId}`, `config/main`, `owners/{userId}`).

**6.1 Sign-in.** Done 2026-09-19. `core/cloud-auth.ts` (pure: PKCE, the authorize URL, Google's reply, request bodies, Firebase's replies in words) and `app/cloud.ts` (carries it out): Google in the owner's own browser, `openid email profile` only, a one-time loopback port on 127.0.0.1, then Firebase `accounts:signInWithIdp` over REST, no SDK, as the Phase 1 proof did. Kept in `cloud.json`: user id, name, email, sign-in time, and the refresh token encrypted with `safeStorage` (Windows DPAPI; where that is unavailable, kept only while the app runs). Refreshed at startup and hourly; only Firebase's final codes (expired, disabled, not found) end the sign-in, so offline keeps it. **How the config ships (decided):** `scripts/cloud-config.mjs` writes `v6/cloud-config.json` (gitignored) from the proof folder's `firebase-config.json` and `oauth-client.json` (or `UM_CLOUD_SOURCE`); `dist.mjs` runs it, and the packager copies it beside the app. Both are public identifiers by Google's own definition; they stay out of the repository all the same. A build without them says "Sign-in is not available in this build"; `startup` logs `signIn: available|unavailable`. Screens: the sign-in screen is real (waiting, cancel, errors in words, back to the app by itself when done) and Settings › Workspace › Your sign-in (who, since when, Sign out). **Nothing is locked behind it yet:** until workspaces exist (6.3) signing in unlocks nothing. Tests: 7 core; Playwright against a fake Google and Firebase in the test (cancelled, signed in with the PKCE challenge checked, token encrypted on disk and no email in the log, kept across a restart, signed out, again from the sign-in screen, ended by Firebase, and a build without config); every test launch points sign-in at an invented project and a closed port. **Not exercised by a test:** the real Google page; the owner's first real sign-in checks it.
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
4. ~~**Imported assistant settings.**~~ Decided 2026-09-19: the assistant starts **off** on every PC, whatever v5 had; Ollama already on the PC is reused; the test set's pass mark is **30 of 30**; Suggest a reply may read the whole open conversation (what WhatsApp has loaded for it), on the PC only.
5. **Code signing** (7.4). Smart App Control blocked an unsigned build for two hours on 2026-09-13; until this is decided, an install can be held up with nothing to do but wait.
6. ~~**Alert volume.**~~ Decided 2026-09-18: keep **one notification per customer**, as it is. A busy Instagram hour can fill the notification centre; the owner would rather see each real customer than a summary. Revisit only if it becomes a nuisance in practice.
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
