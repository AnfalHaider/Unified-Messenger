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
- **Status:** **FIXED** in `v4.99.4` — see "Fix applied". The user-facing notice remains **deferred**.
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
