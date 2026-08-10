# Findings — OversightSnapshotReader (scraped-data ingestion)

`OversightSnapshotReader` parses JSON produced by injected scrapers straight into the oversight metrics.
It was targeted because a schema change on WhatsApp Web's side produces **wrong numbers rather than a
crash**, which is the failure mode this product can least afford — a wrong on-time % is silent and gets
trusted.

## Headline: the feared failure mode is guarded against, and the guard is real

**The concern:** a WhatsApp Web change makes the scan succeed but parse zero conversations. The rollup
would then compute a healthy-looking account, and the owner would be told they are caught up when the app
simply cannot see anything.

That concern is **well-founded at the arithmetic level** — `OversightRollupBuilder.cs:149` genuinely does
compute a false 100%:

```csharp
onTimePercent = snapActive > 0 ? (int)Math.Round((double)snapCaught / snapActive * 100) : 100;
```

A zero-conversation scan produces `OnTimePercent == 100`. **But that number never reaches the screen**,
because every consumer gates on `MeasuredCount`, which is 0 in exactly that case:

| Consumer | Guard | Result on an empty scan |
|---|---|---|
| Account card metric row | `CommandCenterPanel.xaml.cs:2069` — `!HasChatData \|\| !hasLiveData` | renders `"no activity <window>"`, not `100%` |
| Awaiting pill | `CommandCenterPanel.xaml.cs:1962` | renders `"—"` |
| Insight strip | `CommandCenterPanel.xaml.cs:1746` | suppressed |
| Caught-up KPI tile | `CommandCenterPanel.xaml.cs:1255` — `entities.Where(e => e.MeasuredCount > 0)` | account excluded from the weighted average; tile shows `"—"` when nothing is measured |
| Charts | `ChartSeriesBuilder.cs:105,141` — `MeasuredCount <= 0` | series dropped |

`hasLiveData` is `entity.MeasuredCount > 0` at all three sites that compute it
(`CommandCenterPanel.xaml.cs:589,1745,1855`). The aggregate is weighted and filtered, so a broken account
cannot drag the headline toward 100% — it is excluded outright, and `overallPct` is `int?` that stays
`null` (rendering `"—"`) when nothing is measured.

**Verdict: no wrong number is presented as fact.** This is a genuine clean result, and it is load-bearing
enough that `EmptyScanNoFalseMetricTests` (5 tests) now pins it — including that a healthy account still
measures normally, so the tests cannot silently degrade into asserting "always zero".

Also checked and clean: `awaitingCount = Math.Max(0, snapActive - snapCaught)`
(`OversightRollupBuilder.cs:151`) cannot go negative even on an inverted snapshot. Parsing throughout
`RunStoreBridgeScanAsync` uses `TryGetProperty` with `ValueKind` checks rather than blind accessors.

---

### F-SNAP-01 — The last-resort IndexedDB scan failed with no log line and no health record anywhere

- **Severity:** S2
- **Confidence:** confirmed
- **Where:** `UnifiedMessenger/Services/Oversight/OversightSnapshotReader.cs:307-310` (pre-fix bare catch)
- **Where:** `…:275` and `…:288` (pre-fix silent `return null`)
- **Status:** **FIXED** in `v4.99.6`.
- **User-visible symptom:** An account stops contributing oversight data entirely and the owner has no way
  to find out why. The card says "no activity" — which also means "this account is quiet" — so the honest
  reading is "nothing happened today" when the truth is "the app cannot read this account at all". Support
  has nothing to work from either: the log is empty.
- **Repro:** `static-analysis-only` (the fix is verified by build and by the asymmetry being closed; I did
  not force a live scan failure)
- **Root cause:** Asymmetric error reporting between the two ingestion paths. The preferred store-bridge
  path records every failure mode to `StoreBridgeHealth` via `RecordBridgeFailure` — `not-injected`,
  `parse-error`, `parsed-empty`, `exception` — and that surfaces in Settings → Data. The **fallback**
  IndexedDB path, which is what runs when the bridge has already failed, had:
  - `catch { return null; }` — a bare catch swallowing every exception with no logging at all;
  - `return null` on `NOFN` (scan function not injected) with no logging;
  - `return null` on `stage != "done"` with no logging.
  So the path that only ever runs *after something has already gone wrong* was the one path that reported
  nothing. A total oversight failure left no evidence anywhere on the machine.
- **Fix applied:** All three exits now log via `AppLogger.LogWarning` with the instance id and the specific
  reason (`not injected`, the actual settled stage, or the exception type and message). Also added a
  warning when the scan completes but parses **zero** conversations, since that is the exact signature of a
  schema change and was previously indistinguishable from a quiet account.
- **Blast radius:** `OversightSnapshotReader` only. Log volume is bounded — these fire once per refresh per
  account, not per poll iteration.
- **Evidence:** the pre-fix bare `catch { return null; }` at line 307, against
  `RecordBridgeFailure(instance.Id, "exception")` at line 153 in the sibling path.

---

### F-SNAP-02 — "This account is quiet" and "the scraper is broken" are the same message on screen

- **Severity:** S2
- **Confidence:** confirmed (rendering paths read directly; the two states provably converge on the same UI)
- **Where:** `UnifiedMessenger/Controls/CommandCenterPanel.xaml.cs:2069-2079`
- **Status:** **OPEN.** Partially mitigated by F-SNAP-01 (the distinction now exists in the log), but not
  fixed in the UI.
- **User-visible symptom:** Both a genuinely quiet account and an account the app can no longer read render
  as `"no activity <window>"` with an `"—"` awaiting pill. These demand opposite responses: one is good
  news, the other means the owner's oversight of that branch has stopped working and customers may be
  waiting unseen. A monitoring product that cannot distinguish "nothing to report" from "I have stopped
  looking" has a real gap, and it is precisely the gap a paying customer would be angriest about
  discovering late.
- **Repro:** `static-analysis-only` — both states produce `MeasuredCount == 0`, and the render branch keys
  only on that.
- **Root cause:** `MeasuredCount == 0` is overloaded. It is reached by "the scan worked and there were no
  active chats in the window" and by "the scan returned nothing usable", and the rollup does not carry
  which one happened. `StoreBridgeHealth` knows for the bridge path, but that state is not joined onto the
  entity the card renders.
- **Proposed fix:** Carry a distinct "could not read" signal from the reader through the rollup onto
  `OversightEntityHealth`, and render it as a warning ("can't read this account — click Re-sync") rather
  than the neutral "no activity". The plumbing already half-exists: `StoreBridgeHealth` records per-instance
  success/failure, and `IsStale` already drives a warning line on the same card. **Tradeoff:** a false
  positive here is costly — telling an owner their scraper is broken when their branch is simply quiet
  would erode trust in the opposite direction — so the signal must come from an actual recorded failure,
  never inferred from a zero count.
- **Blast radius:** `OversightSnapshotReader` → `OversightRollupBuilder` → `OversightEntityHealth` →
  `CommandCenterPanel`. Crosses the rollup's public shape, so it needs its own increment and its own tests.
- **Evidence:** `CommandCenterPanel.xaml.cs:2073` —
  `Text = !entity.HasChatData ? "syncing…" : $"no activity {_emptyStateWindowLabel}"`. There is no third
  branch, and nothing distinguishes a failed read from an empty one.

---

## What I could not determine

- **An account with no threads at all produces no entity, and I did not verify what the dashboard then
  shows.** Discovered while writing tests: `OversightRollupBuilder` builds entities by grouping
  `ThreadData`, so an account with zero threads yields **no entity whatsoever** rather than an empty one —
  the `chatSnapshot` overlay cannot resurrect it. Whether such an account silently disappears from the
  command centre, or is rendered from the instance list by a separate path, is **unverified**. It is worth
  checking: an account vanishing from an oversight dashboard is a worse dead end than one showing
  "no activity". I am flagging it rather than asserting it, because my first assumption about this code's
  shape was wrong and the tests caught me.
- **No live schema-break was simulated.** I did not modify the injected scraper to emit a changed shape and
  observe the result end-to-end. The guard analysis above is from reading the render paths plus unit tests
  against `OversightRollupBuilder`; it is not an end-to-end demonstration. That test would be the
  definitive one and it was not run.
- **`ChatEntryParser.ParseConversations` itself was not audited.** This file delegates all field extraction
  to it, so per-field tolerance to missing/renamed/wrong-typed values — the actual parsing — remains
  unexamined. AGENTS.md's warning that both producers (`whatsapp-adapter.js` and
  `whatsapp-store-bridge.js`) must emit every `ChatEntry` field is unverified; a field emitted by one and
  not the other would make metrics differ depending on which path ran. **This is now the highest-value
  remaining target in this area.**
- **`HarvestPreviewsAsync` (lines 204-250) was not reviewed.**
