# Findings — Install, upgrade, uninstall, first run

All executed end-to-end on this machine against the real installer, with the owner's live data backed up
and hash-verified before and after. No owner data was lost: all 9 real stores hash-matched the pre-test
backup afterwards, and all 31 WebView2 profiles (signed-in accounts) survived.

## What was executed

| Path | Result |
|---|---|
| Installer compile (`ISCC`) | ✅ 57.6 MB, successful compile |
| **Upgrade** v4.99.0 → v4.99.13 over live data | ✅ exit 0; installed `FileVersion 4.99.13.0`; registry `DisplayVersion 4.99.13` |
| Data preservation across upgrade | ✅ all 9 `.json` stores byte-identical; 31 WebView2 profiles intact — ❌ but see F-INSTALL-01 |
| **Uninstall** | ✅ program dir removed; registry entry removed; Start Menu and Desktop shortcuts removed; no `Run` key left |
| **Clean install** (data directory moved aside) | ✅ exit 0 in **9.4 s**; correct version |
| **First run** on a clean machine | ✅ alive in 14.2 s at 186 MB — ❌ but see F-FIRSTRUN-01 |
| Stale-binary trap (`-p:Platform=x64`) | ✅ correct version installed every time; no stale binary observed |

---

### F-INSTALL-01 — Every upgrade silently deleted the log and the settings-recovery file

- **Severity:** S2
- **Confidence:** confirmed (observed on a real upgrade, fixed, and re-verified with marker files)
- **Where:** `installer-shared.iss:24-32` (`IsPreservedRootFile`), `:16-17`, `:89-90`
- **Status:** **FIXED** in `v4.99.14`.
- **User-visible symptom:** Two things a user needs precisely when something has gone wrong were destroyed
  by the act of updating — and since auto-update is on by default, without them ever choosing to update:
  1. **`app.log` and `app.old.log`.** This is the *only* diagnostic surface in the product. A user who
     updates in order to fix a problem was wiping the evidence of that problem, and support had nothing to
     work from. This got materially worse during this audit: v4.99.4–v4.99.8 deliberately routed
     corrupt-file recovery, failed account reads and parser shape-changes into `app.log`.
  2. **`<store>.corrupt-<timestamp>.bak`.** These are written when a data file cannot be read and are the
     user's only route back to their settings. **The v4.99.4 release notes explicitly tell them to look for
     this file** — and the next update deleted it.
- **Repro:** seed `app.log`, `app.old.log` and `settings.json.corrupt-20260811000000.bak` with marker
  content, run the installer over an existing install. Pre-fix: all three gone. Post-fix: all three present
  with markers intact, while a planted `StaleLegacy.dll` was still correctly removed.
- **Root cause:** `LegacyInstallDir` and `UserDataDir` are **the same path** (`{localappdata}\UnifiedMessenger`),
  because an older layout installed the program into what is now the data directory. `CurStepChanged` runs
  `CleanAppPayload` there on every install to strip stale binaries, preserving only files that
  `IsPreservedRootFile` approves — and that returned true for `.json` alone. `app.log` (`.log`) and the
  recovery files (`.bak`) fell through and were deleted.
  The comment above that function shows this class of bug had already bitten once: individual filenames
  were being enumerated, and `response-times`, `contact-history`, `kpi-trend`, `awaiting-overrides` and
  `oversight-snapshot` were all wiped on every update, so FRT/SLA never accumulated. That was fixed by
  preserving all `*.json` — which fixed the stores but left logs and recovery files exposed.
- **Fix applied:** `IsPreservedRootFile` now preserves `.json`, `.log` **and** `.bak`. Stale binaries from
  the legacy layout (`.exe`/`.dll`/`.pri`/`.xbf`/`.mui`) are still removed — verified by planting a
  `StaleLegacy.dll` and confirming it was deleted in the same run that preserved the markers.
- **Blast radius:** installer only; both x64 and ARM64 share `installer-shared.iss`.

---

### F-INSTALL-02 — Uninstall left 7.2 GB behind, including customer names and message previews, with no mention and no option

- **Severity:** S2
- **Confidence:** confirmed (measured after a real uninstall)
- **Where:** `installer-shared.iss` `[UninstallDelete]`
- **Status:** **FIXED** in `v4.99.14`.
- **User-visible symptom:** After uninstalling, **13,407 files / 7.17 GB** remained:
  | Left behind | Size |
  |---|---|
  | `WebView2/` (31 profiles — signed-in sessions) | 3.93 GB |
  | `ollama/` (AI models) | 3.24 GB |
  | `oversight-snapshot.json` | 901 KB — **customer names and message previews** |
  | `contact-history.json` | 315 KB — per-customer first/last contact |
  | `analytics.json`, `triage_v2.json`, `response-times.json`, … | — |
  For a product whose central promise is that customer data never leaves the machine, "and it stays there
  after you uninstall, with no way to say otherwise" is not a defensible default. A departing user has no
  indication their customers' names and messages are still on disk.
- **Repro:** run `unins000.exe /VERYSILENT`, then measure `%LOCALAPPDATA%\UnifiedMessenger`.
- **Root cause:** `[UninstallDelete]` covered `{app}` (the program directory) and the Ollama runtime, with
  an existing opt-in task for the AI models — but nothing covered the data root.
- **Fix applied:** a second opt-in uninstall task, following the exact pattern of the existing AI-models
  one: *"Also erase all app data — message history, signed-in accounts and settings"*, **unchecked by
  default**. Leaving the data is still the default, deliberately, because it means a reinstall picks up
  where the owner left off with their response-time history and logins intact. The defect was that the
  choice was invisible, not that the default was wrong. Ordering matters and is commented: the narrower
  Ollama deletes run before the data-root delete that subsumes them.
- **Blast radius:** installer only. **Not re-verified end-to-end** — see "What I did not verify".

---

### F-FIRSTRUN-01 — The first screen a stranger sees greeted them with "Welcome back" and claimed an account was connected

- **Severity:** S2
- **Confidence:** confirmed (observed on a genuinely clean install, fixed, re-verified on a second clean install)
- **Where:** `UnifiedMessenger/Pages/DashboardPage.xaml:30` (hardcoded greeting)
- **Where:** `UnifiedMessenger/Pages/DashboardPage.xaml.cs:170` (subtitle), `Services/InstanceRegistryService.cs:599` (the seeded account)
- **Status:** **FIXED** in `v4.99.14`.
- **User-visible symptom:** On a clean machine, the opening screen said all of this simultaneously:
  ```
  Welcome back
  1 personal account connected.
  ...
  No accounts connected yet
  ```
  A first-time user is greeted as a returning one, told they have an account connected when they have
  connected nothing, and then told — eight lines lower on the same screen — that no accounts are
  connected. This is the product's first impression, and §6.15's "is it obvious what this is within
  10 seconds" bar is not met when the screen contradicts itself.
- **Repro:**
  1. Move `%LOCALAPPDATA%\UnifiedMessenger` aside so there is no prior data.
  2. Install and launch.
  3. Read the dashboard header against the command-centre empty state.
- **Root cause:** two independent causes that happened to land together.
  1. **The greeting was a literal in XAML.** `Text="Welcome back"` is what shows until `RefreshAll()` runs,
     so the time-of-day logic (`Good morning`/`Good afternoon`/…) never got a chance on first paint. Note
     `"Welcome back"` was also the ≥21:00 branch of that logic, which is wrong for a different reason —
     it is a *greeting for a returning user*, not an evening greeting.
  2. **The registry seeds a placeholder account** (`whatsapp-default`, category Personal) on first run.
     It is a genuine registry entry, so `personalCount` counted it and the subtitle reported "1 personal
     account connected." The command centre's empty state counts only *professional* accounts, so it
     correctly said none — hence the contradiction between the two.
- **Fix applied:** `DashboardPageHelper.HasOnlySeededDefaultAccount` detects the "only the app's own
  placeholder is present" state. In that state the header reads **"Welcome to Unified Messenger"** and the
  subtitle reads **"Add an account to start receiving unified notifications."**, matching the empty state
  below it. The XAML literal is gone, so nothing can paint a stale greeting before refresh, and the
  ≥21:00 branch now says "Good evening". `AutomationProperties.Name` is kept in step with the visible text.
- **Verified after fix**, on a second genuinely clean install:
  ```
  Welcome to Unified Messenger
  Add an account to start receiving unified notifications.
  ...
  No accounts connected yet
  ```
- **Residual, deliberately left:** the "Personal · 1" filter chip still counts the seeded account. That is
  arguably correct — the placeholder account *does* exist in the sidebar and is clickable, which is its
  purpose — and suppressing it risked hiding a real account count. Recorded rather than over-corrected.

---

---

### F-INSTALL-03 — The ARM64 installer shipped a binary 16 versions stale, stamped as the current release, with no warning

- **Severity:** S1
- **Confidence:** confirmed (observed, fixed, and the fix demonstrated to block it)
- **Where:** `installer-arm64.iss:5` / `installer.iss:6` (`PublishDir`); no payload verification existed
- **Status:** **FIXED** in `v4.99.14` by a new compile-time guard, `installer-verify-payload.iss`.
- **User-visible symptom:** `UnifiedMessengerSetup-arm64.exe` compiled cleanly, was stamped
  **4.99.14**, and contained a **4.98.0.0** binary from a week earlier. An ARM64 customer would install
  "4.99.14", run 4.98.0 code, and every version readout would agree with the wrong number — the About
  page, Add/Remove Programs, and the update check. Every fix in this audit would be silently absent while
  appearing present. There was **no warning of any kind** at compile time.
- **Repro:** publish x64 only, then run ISCC on `installer-arm64.iss`. Pre-fix it succeeded and produced a
  55 MB installer around a stale payload.
- **Root cause:** each `.iss` packages `{#PublishDir}\*` for its architecture, and `AppVersion` comes from
  `MyAppVersion` in `installer-shared.iss` — the two are **completely independent**. Nothing ever compared
  them. Publishing only one architecture (or forgetting `-p:Platform`) leaves whatever an earlier release
  wrote in the other folder, and ISCC packages it without complaint. AGENTS.md documents the CI form of
  this trap; the local form was undefended.
- **Fix applied:** `installer-verify-payload.iss`, included by both installers after each defines its own
  `PublishDir`. It fails the compile when the packaged `UnifiedMessenger.exe`'s file version does not equal
  `MyAppVersion + ".0"`, printing both versions and the offending path, and fails separately with a
  publish command when the payload is missing entirely.
- **The guard caught a live instance on its very first run.** Compiling x64 immediately failed with
  `installer claims : 4.99.14.0 / payload actually : 4.99.13.0` — I had bumped the version but not
  re-published, which is precisely the mistake the guard exists to catch, made by the person writing it.
- **Verified both directions:**
  ```
  x64,  payload republished at 4.99.14 -> Successful compile          (exit 0)
  ARM64, payload stale at 4.98.0.0     -> PAYLOAD VERSION MISMATCH    (exit 2)
                                          installer claims : 4.99.14.0
                                          payload actually : 4.98.0.0
  ```
- **Also removed:** the stale `dist\UnifiedMessengerSetup-arm64.exe` built before the guard existed. It
  carried the 4.98.0 payload under the current release name and was one upload away from shipping.
- **Blast radius:** build tooling only; no application code. ARM64 releases now **cannot** be built without
  publishing ARM64 first — which is the correct, if louder, behaviour.

## What I did not verify

- **F-INSTALL-02's fix was not re-run end-to-end.** The opt-in uninstall task is added and the installer
  compiles, but I did not uninstall again with the box ticked to confirm the data root is actually erased,
  because that would have destroyed the owner's 7.2 GB of live data and signed-in sessions for a test I
  could not cheaply undo. **This is the one change in this increment that is unverified at runtime.**
- **Only the x64 installer was exercised.** `installer-arm64.iss` shares `installer-shared.iss` so both
  fixes apply, but no ARM64 install was performed.
- **No true clean-*machine* test.** "Clean install" here meant moving the data directory aside on this
  machine. WebView2 runtime, .NET state and any machine-level prerequisites were already present, so a
  genuinely bare Windows install could still surface prerequisite failures this test cannot see.
- **The upgrade path tested was v4.99.0 → v4.99.13.** Older schema versions were not tested; `instances.json`
  carries `"version": 5` and `settings.json` `"version": 19`, so migrations from earlier versions exist and
  are unexercised.
- **The auto-updater itself was not tested** — only the installer it invokes. The GitHub download,
  download verification and failure-when-unreachable paths remain unverified.
- **First-run onboarding beyond the dashboard** — adding a first account, signing into WhatsApp Web, and
  reaching a first real metric — was not walked through. The clean-install test stopped at the dashboard.
