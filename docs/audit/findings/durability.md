# Findings — Data durability and recovery

Audited directly by the orchestrator. Findings here were **reproduced against the shipping Release
binary** using the owner's real data directory, which was backed up and hash-verified before the test and
hash-verified again after restore (all 11 files matched byte-for-byte). No owner data was lost.

## Findings

### F-DURA-01 — A corrupt settings file silently reverts every preference to defaults, with no trace in the shipping build

- **Severity:** S1
- **Confidence:** confirmed (reproduced end-to-end against the published Release binary; before/after
  values captured by diffing the live file against a verified backup)
- **Where:** `UnifiedMessenger/Services/AppSettingsService.cs:62-74` (the load path)
- **Where:** `UnifiedMessenger/Services/AppSettingsService.cs:158-174` (`BackupCorruptFile`)
- **Status:** **CLOSED** in `v4.99.24`. Logging and preservation landed in `v4.99.4`; the user-facing
  notice — the part that mattered, and which this document was right to keep open — landed in
  `v4.99.24`. See "F-DURA-01 closed" at the end of this document.
- **User-visible symptom:** The owner's saved configuration silently reverts to factory defaults and
  nothing tells them. Measured on real data, these flipped:

  | Setting | Was | Became | Why it matters |
  |---|---|---|---|
  | `enableLocalAi` | `true` | `false` | AI insights silently stop appearing. The owner concludes the AI feature is broken. |
  | `promptBeforeAutoUpdate` | `true` | `false` | **Consent-relevant.** They explicitly asked to be consulted before updates; the app now updates without asking. |
  | `maxConcurrentWebViews` | `6` | `0` (unlimited) | The memory cap they deliberately set is gone. On a machine with many accounts this is a real resource change. |
  | `enableImportExportInstances` | `true` | `false` | A feature they turned on disappears from Settings. |
  | `enableInstanceNotesTags` | `true` | `false` | Same. |
  | `sidebarPinnedExpanded` | `true` | `false` | Layout resets. |
  | `workspaceProfiles` | **5 profiles** | **4 profiles** | **A whole location's SLA threshold and business-hours configuration was lost.** This one directly changes what the metrics mean for that branch. |

  The most damaging property is that the reset is **indistinguishable from the app misbehaving**. The
  owner sees AI stop working and a branch's SLA change, with no event to connect it to.
- **Repro (executed, start to finish):**
  1. Close the app.
  2. Truncate `%LOCALAPPDATA%\UnifiedMessenger\settings.json` mid-token, e.g. `{ "EnableLocalAi": true, "OllamaEndp`
  3. Launch the published Release binary.
  4. App starts normally — **ALIVE**, no dialog, no banner, no indication of any problem.
  5. `Select-String` over `app.log` for `corrupt|Settings|JsonException|Deserialize` returns **nothing**.
  6. Diff the live `settings.json` against a pre-test backup: the table above.
- **Root cause:** Two independent defects.
  1. **The diagnostic is compiled out of the shipping build.** The handler logged via
     `Debug.WriteLine($"Settings file is corrupt; resetting to defaults: …")`. `Debug.WriteLine` is
     removed by the compiler in Release, so in the binary customers actually run, the corruption produced
     **no record anywhere** — not in `app.log`, not on screen. Every other error path in this service uses
     `AppLogger`; this one did not. That is why it went unnoticed.
  2. **The catch was too narrow.** `catch (JsonException)` only. A settings file locked by a backup tool
     or antivirus, or on an unavailable network profile, throws `IOException` /
     `UnauthorizedAccessException`, which escaped `LoadAsync` entirely — a startup failure rather than a
     graceful fallback.
  **Mitigation that already existed and should be credited:** `BackupCorruptFile()` moves the unreadable
  file aside as `settings.json.corrupt-<timestamp>.bak`, so the original is **not destroyed** and a
  technical person can recover the values. That is genuinely good design. It is undermined only by the
  fact that the user is never told the file exists.
- **Fix applied (v4.99.4):**
  - `Debug.WriteLine` → `AppLogger.LogError("Settings.Load.Corrupt", ex)`, so the event survives into the
    Release build. **Verified:** re-running the identical repro against the rebuilt binary now produces
    `[ERR] [Settings.Load.Corrupt] System.Text.Json.JsonException: Expected end of string…` in `app.log`,
    where previously there was nothing.
  - Catch broadened to `JsonException or IOException or UnauthorizedAccessException or NotSupportedException`,
    so a locked or unreachable file degrades to defaults instead of failing startup.
  - `BackupCorruptFile()` now returns the path it wrote, surfaced as `CorruptFileBackupPath`, alongside a
    `RecoveredFromCorruptFile` flag — the state a UI notice needs.
- **Still open — deliberately deferred, and this is the part that matters most:** the user is **still not
  told**. The event is now on the record and recoverable, but a non-technical owner does not read
  `app.log`. Closing this properly needs a first-run-after-recovery notice that names the backup file and
  offers to restore it, which is a UI change with its own design and test surface. Until that ships, this
  finding is **mitigated, not closed**. I am not claiming otherwise.
- **Blast radius:** `AppSettingsService` only. The broadened catch changes startup behaviour in the
  locked-file case from "crash" to "defaults", which is strictly better but does mean a transient lock now
  silently costs the user their settings for that session — which is precisely why the notice matters.
- **Evidence:**
  ```
  BEFORE FIX (published Release binary):
    app: ALIVE
    app.log grep corrupt|Settings|JsonException|Deserialize  ->  (NONE — nothing logged)

  AFTER FIX (same repro, rebuilt binary):
    app: ALIVE
    2026-08-10 12:07:50Z [ERR] [Settings.Load.Corrupt] System.Text.Json.JsonException:
      Expected end of string, but instead reached end of data. Path: $ | LineNumber: 0 | BytePositionInLine: 36.
    preserved: settings.json.corrupt-20260810120750.bak  (39 bytes)

  Owner data safety: 11/11 files SHA256-matched the pre-test backup after restore.
  ```

---

### F-DURA-02 — The awaiting-overrides and KPI-trend stores discarded an unreadable file without preserving it, and reported nothing in the shipping build

- **Severity:** S1
- **Confidence:** **split — read this carefully, the two halves are not equally proven.**
  - *Confirmed:* the silence, the too-narrow catch, and the absence of any file preservation.
  - *Likely, NOT demonstrated:* that the next flush then overwrites the file and destroys it permanently.
    See "What I did not prove" below. I attempted this and my test was invalid.
- **Where:** `UnifiedMessenger/Services/Oversight/AwaitingOverrideStore.cs:140` (pre-fix)
- **Where:** `UnifiedMessenger/Services/Oversight/KpiTrendStore.cs:126` (pre-fix)
- **Status:** **FIXED** in `v4.99.5`.
- **User-visible symptom:** Every chat the owner marked handled or snoozed silently comes back as
  "awaiting". This is worse than it sounds: the whole point of mark-handled is to let the owner close out
  a chat that genuinely needs no reply, and `AwaitingOverrideStore` is what stops the backlog being
  permanently faked. If it empties, the owner's triage work is undone and the awaiting count jumps with no
  explanation — they will assume the metric is broken, which is the one thing this product cannot afford.
  The KPI-trend equivalent is milder: sparklines and week-over-week comparisons lose their history.
- **Repro (executed against the shipping binary):**
  1. Close the app.
  2. Truncate `%LOCALAPPDATA%\UnifiedMessenger\awaiting-overrides.json` mid-value, leaving recognisable
     content (`{ "instances": { "acct-REAL-DATA-MARKER": { "chat-123": { "kind": "Handled"`).
  3. Do the same to `kpi-trend.json`.
  4. Launch. App starts normally. Pre-fix: **nothing** in `app.log`, and **no** `.bak` written.
- **Root cause:** Same shape as F-DURA-01 but without its saving grace. Both stores reported via
  `Debug.WriteLine` (stripped from Release), caught `JsonException` only, and — unlike
  `AppSettingsService` — had **no equivalent of `BackupCorruptFile`**. A grep for `BackupCorruptFile|\.bak`
  across both files returned **0**. The unreadable file was left exactly where it was, while
  `_isLoaded` had already been set to `true` *before* the try block, leaving the store "loaded" and empty.
- **Fix applied (v4.99.5):** Introduced `Services/CorruptFileRecovery.cs`, one shared helper answering the
  three questions all three stores were answering differently — what counts as unreadable, where it gets
  recorded, and whether the bytes are kept. All three stores now route through it, so they cannot drift
  apart again. `AppSettingsService.BackupCorruptFile` was deleted as dead code once it did.
  `IsUnreadable` deliberately **excludes** `OperationCanceledException` and programmer errors like
  `NullReferenceException`: a load cancelled during shutdown is not a damaged file, and moving a perfectly
  good store aside because of a cancellation would itself cause the data loss this is meant to prevent.
- **Blast radius:** Three load paths. The behaviour change is additive (log + preserve); no store's
  success path was touched.
- **Evidence (live, published Release binary, after fix):**
  ```
  2026-08-10 12:18:33Z [ERR] [AwaitingOverrides.Load.Corrupt] System.Text.Json.JsonException: The JSON
    value could not be converted to ...OverrideKind. Path: $.instances.acct-REAL-DATA-MARKER.chat-123.kind
  2026-08-10 12:18:33Z [ERR] [KpiTrends.Load.Corrupt] System.Text.Json.JsonException: Expected end of
    string, but instead reached end of data. Path: $.days.2026-08-09

  awaiting-overrides.json.corrupt-20260810121833.bak  [78 bytes]
    { "instances": { "acct-REAL-DATA-MARKER": { "chat-123": { "kind": "Handled"
  kpi-trend.json.corrupt-20260810121833.bak  [40 bytes]
    { "days": { "2026-08-09": { "awaiting
  ```
  The marker bytes survived — before the fix there was no `.bak` at all. Pre-fix code evidence:
  ```
  $ grep -c "BackupCorruptFile\|\.bak" AwaitingOverrideStore.cs KpiTrendStore.cs
  AwaitingOverrideStore.cs:0
  KpiTrendStore.cs:0
  ```
  Owner-data safety: 11/11 files SHA256-matched the pre-test backup after restore; no `.bak` debris left.

#### What I did not prove (F-DURA-02)

I claimed in an earlier report that the next flush overwrites the unreadable file and destroys it
permanently. **I did not demonstrate that, and my attempt to was invalid.** I killed the app with
`Stop-Process -Force`, which skips graceful shutdown entirely, so the flush never ran — the file was
absent both before and after, which proves nothing either way. The reasoning behind the claim is sound
(`_isLoaded = true` executes before the try, so a corrupt load leaves the store "loaded and empty", and
`FlushAsync` writes in-memory state to that path) but it remains **inference, not observation**. A proper
test would mark a chat handled, corrupt the file, restart, and exit via the app's own quit path. That is
worth doing and was not done.

---

## F-DURA-03 — Torn-write investigation: the feared failure mode does NOT exist

This was flagged across two earlier reports as the biggest open unknown, on the grounds that it could
**invalidate the recovery work in v4.99.4 and v4.99.5**: if an interrupted write could leave a file that
*parses cleanly but has lost records*, then corruption detection would never fire, no `.bak` would be
written, and the store would silently reset. That would be strictly worse than the bugs already fixed,
because none of the new machinery would engage.

**Result: it cannot happen through the normal write path.** Reported as a clean check with evidence,
because a negative result here is worth as much as a finding.

### Why the main file can never be partially written

Every durable store writes the same way. `AwaitingOverrideStore.cs:267-275` is representative:

```csharp
Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
var tempPath = _storePath + ".tmp";
await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
{
    await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken);
    await stream.FlushAsync(cancellationToken);
}
File.Move(tempPath, _storePath, overwrite: true);   // atomic rename, same volume
```

Serialize to a temp, flush, close, then atomically replace. The live file is only ever swapped for a
fully-written one. Kill the process at any point and the live file is either the old complete version or
the new complete version — never a blend. Confirmed present in **all** durable stores:
`AppSettingsService.cs:136-152`, `InstanceRegistryService.cs:548-564`, `ContactHistoryStore.cs:304-313`,
`MessageAnalyticsService.cs:1781-1796`, `OversightChatSnapshotService.cs:432-441`,
`AwaitingOverrideStore.cs:267-275`, `KpiTrendStore.cs:216-223`, `LocalBackupService.cs:58-73`.

**Real-world confirmation on this machine:** a **zero-byte `triage_v2.json.tmp`** was sitting in the
owner's data directory throughout this audit. That is the fingerprint of an interrupted write that
actually occurred here — and the design handled it exactly as intended: the temp was orphaned and
`triage_v2.json` itself was intact at 646,795 bytes. The mechanism is not theoretical; it has already
been exercised in production on the owner's own data.

### And why a truncated file still triggers recovery

The remaining worry was a file that parses to *nothing* rather than failing. Tested directly
(`TornWriteRecoveryTests`, 6 tests, green):

| File content an interruption could leave | `DeserializeAsync` | Recovery fires? |
|---|---|---|
| zero bytes | **throws `JsonException`** | yes — logged + `.bak` |
| whitespace only | **throws `JsonException`** | yes |
| truncated mid-value | **throws `JsonException`** | yes |
| literal `null` | returns `null`, no throw | no — but every store null-checks (below) |
| `{}` | returns empty object | correctly treated as real empty data |

The zero-byte case was the one that mattered, since it is the likeliest outcome of an interrupted write,
and it throws — so `CorruptFileRecovery` engages and the bytes are preserved. The only shape that slips
past detection is a literal `null`, and all three load paths already guard it:
`AppSettingsService.cs:67` (`?? new AppSettings()`), `AwaitingOverrideStore.cs:151`
(`store?.Instances ?? []`), `KpiTrendStore.cs:135` (`store?.Days ?? []`). Nothing anywhere reads a `.tmp`
file, so orphaned temps are inert.

### Residual gaps — real, but much smaller than feared

**(a) No write-through, so power-loss durability is not guaranteed. Severity: S3, confidence: confirmed.**
`stream.FlushAsync()` pushes the managed buffer to the OS, not to the physical disk. There is no
`FileOptions.WriteThrough` and no `Flush(flushToDisk: true)` anywhere in the codebase (grep returns
nothing). A process kill is therefore safe — the OS holds complete data and the rename is atomic — but a
**power cut or bug-check** could complete the rename in the NTFS journal while the file's data blocks are
still unwritten, yielding a zero-length or garbage file. The saving grace is that this lands squarely in
the case handled above: it parses as corrupt, gets logged, and the previous content is preserved. So the
worst outcome is "settings reset, recoverable from `.bak`", not silent record loss.
*Proposed fix:* add `FileOptions.WriteThrough` to the temp-file `FileStream` in the durable stores. Cheap,
localised. *Tradeoff:* write-through bypasses the OS cache and is measurably slower — on
`analytics.json` (168 KB) and `oversight-snapshot.json` (908 KB), flushed on every shutdown and on
hide-to-tray, that cost is paid frequently. Worth measuring before applying; I have **not** measured it,
so I am not applying it blind.

**(b) Orphaned `.tmp` files are never cleaned up. Severity: S4, confidence: confirmed.**
Nothing deletes them (grep for `.tmp` deletion returns nothing), so each interrupted write leaves one
behind permanently. Harmless — they are inert and small — but they accumulate, and a stray zero-byte
`.tmp` next to a user's data looks alarming to anyone who goes looking. *Proposed fix:* best-effort delete
of `<store>.tmp` on successful load.

### Verdict

The concern that motivated this investigation is **closed as not-a-defect**. The atomic write pattern is
correctly implemented everywhere, and the truncation shapes it can produce all route into the recovery
added in v4.99.4/.5 rather than around it. The durability work is not invalidated. Two small residual
gaps are recorded above; neither is a blocker, and (a) should be measured before it is fixed.

## What was NOT covered

- **Only `settings.json` was tested.** The same `Debug.WriteLine`-in-a-catch pattern appears in the other
  stores — `AwaitingOverrideStore.cs:140` catches `JsonException` and logs with `Debug.WriteLine`, and
  `KpiTrendStore.cs:126` follows the same shape. **These were not reproduced.** By inspection they share
  defect (1) above and are likely to lose their data just as silently; a corrupt `awaiting-overrides.json`
  would silently un-handle every chat the owner marked done. That is the obvious next test and it was not
  run.
- **23 `JsonSerializer.Deserialize` / `JsonDocument.Parse` sites exist** across the app. Two load paths
  were examined. The scraped-data parse paths (`OversightSnapshotReader.cs:100,280`) were **not** audited,
  and those consume input from a web page that can change without notice — the highest-risk category.
- **Partial-write / torn-file behaviour was not tested.** Note that a zero-byte `triage_v2.json.tmp` was
  sitting in the data directory during this audit, which suggests a write-then-rename path exists and may
  leave debris; whether an interrupted write can produce a *valid-but-truncated* JSON file (which would
  parse successfully and silently lose records, defeating the corruption detection entirely) is
  **unknown and worth checking**.
- **Disk-full, read-only-directory, and network-profile cases were not tested.**

---

# F-DURA-01 closed (session 2, `v4.99.24`)

The finding was recorded as **"mitigated, not closed"** and said so plainly: since `v4.99.4` the event is
logged and the unreadable file is preserved, and `AppSettingsService` exposes `RecoveredFromCorruptFile`
and `CorruptFileBackupPath` — "the state a UI notice needs". What it did not say, because it was not
checked, is that **nothing read either property**. A grep for both names returned only their own
declaration and assignment. The owner of a salon does not read `app.log`, so in practice the reset was
still silent.

## What shipped

A modal notice at startup, in the session where the recovery happened. Modal rather than a banner
deliberately: one of the settings that silently reverts is whether updates install without asking, so
this is consent-relevant, and a dismissible strip is exactly what a busy owner scrolls past.

The wording lives in `SettingsRecoveryNotice` so the copy contract is testable. Most of the eleven tests
assert what it must **not** say:

- **It never claims to know which settings changed.** The file could not be parsed, so there is no
  before-state to diff against. Any "the following settings were reset" would be invented.
- **It never offers a restore it cannot perform.** The only bytes available are the ones that failed to
  parse.
- **It reassures that accounts and message history are untouched** — those live in separate stores.
  Without that line the reasonable fear is "have I lost my conversations?", which is a much bigger worry
  than what actually happened.
- **It names the auto-update setting specifically**, because that is the one that changes what the app
  does without asking.
- **It speaks about settings, not about JSON.** Checked against the prose with the backup path removed —
  the preserved file is genuinely called `settings.json.corrupt-<timestamp>.bak` and the owner needs that
  name verbatim, so a blanket jargon scan flagged the one piece of actionable detail in the notice.

`CanRevealBackup` checks the disk rather than trusting the recorded path, so "Show me the file" is only
offered when there is a file to show.

## Verified live, against the real corruption path

The original repro, run twice against the published binary, with the owner's user-data directory backed
up and SHA256-verified before and restored and re-verified after (11 files, hashes identical both times;
the test's `.bak` artefacts removed).

```
[ERR] [Settings.Load.Corrupt] System.Text.Json.JsonException: Expected end of string,
      but instead reached end of data. Path: $ | LineNumber: 0 | BytePositionInLine: 36.
[INF] [Settings.Recovery] Telling the user their settings were reset
      (preserved copy: …\settings.json.corrupt-20260814174910.bak).
```

UI Automation capture of the dialog as a screen reader would read it:

```
=== DIALOG: 'Your settings were reset' (6 controls) ===
  [Text] 'Your settings were reset'
  [Text] 'Unified Messenger could not read your saved settings when it started, so it is running on
          default settings.
          Anything you had changed is affected — reply-time targets and business hours for each
          location, notification choices, and whether on-device AI is switched on. Your accounts and
          message history are stored separately and are untouched.
          Worth checking first: whether updates install automatically, under Settings.
          Your previous settings file was not deleted. A copy was kept as:
          C:\Users\anfal\AppData\Local\UnifiedMessenger\settings.json.corrupt-20260814174910.bak'
  [Button] 'Show me the file'
  [Button] 'OK'
```

**A note on how this was verified, because the first attempt failed misleadingly.** The initial run
logged the pre-call line and then showed nothing in the UIA tree, and the app briefly had no window
handle. That looked like the notice was broken. It was not — the capture had simply run at the wrong
moment. The fix was to log the dialog's *result* after the `await`, which distinguishes "shown and
dismissed" from "never displayed": on the second run the absence of that line proved the dialog was still
open and waiting, and the capture above followed immediately. That line is kept, because the same
ambiguity would face anyone diagnosing this later.

## F-DURA-03 — Two startup prompts raced, and one was silently lost

- **Severity:** S3 · **Confidence:** confirmed by inspection; the sequencing fix observed working live
- **Status:** **FIXED** in `v4.99.24`

Found while placing the notice. The two existing startup prompts were started as separate fire-and-forget
calls:

```csharp
_ = MaybeShowWorkspaceOnboardingAsync();
_ = MaybePromptPinToTaskbarAsync();
```

WinUI permits only one `ContentDialog` open at a time, and the second `ShowAsync` throws. Both call sites
catch and log, so nothing crashes — the prompt simply never appears. For the onboarding wizard that is
worse than it sounds, because its `finally` sets `HasCompletedWorkspaceOnboarding = true` whether or not
the wizard was ever seen. **A swallowed wizard is marked done for good.**

This is not hypothetical and it is not a tidy-up: after a settings reset *every* flag is back to its
default, so onboarding and the taskbar prompt both come due in the same session — the exact session the
recovery notice also wants to speak in. Three modals would have raced.

Now sequenced in `RunStartupPromptsAsync`: recovery notice → onboarding → taskbar pin, each awaited, so
the owner learns *why* they are being asked to set the app up again before they are asked. Observed live:
dismissing the notice let the onboarding flow proceed to the "Add account" dialog.

## What was NOT covered

- **The "Show me the file" button was not clicked in the live test**, so the Explorer reveal is
  unexercised. The failure path is caught and logged, and `CanRevealBackup` is unit-tested.
- **The no-backup wording** (`CorruptFileBackupPath` null, when preservation itself fails) is unit-tested
  but was not reproduced live — it needs a directory the app can read but not write.
- **The notice was not seen by an actual screen reader**, only through the UIA tree it consumes.
- **`HasCompletedWorkspaceOnboarding` being set after a swallowed wizard** is fixed by removing the race,
  not by making the flag conditional on the wizard actually being seen. If a wizard ever fails for some
  other reason, it will still be marked complete.
