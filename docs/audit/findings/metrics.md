# Findings — Metric correctness

Audited directly by the orchestrator after two delegated agents for this domain died without producing
findings. Coverage is **partial** — see "What was not covered". Findings here were observed against the
owner's real live data (3 WhatsApp accounts, 380 awaiting chats), not constructed fixtures.

## Metric ledger

Only metrics actually traced are listed. An empty verdict means not examined — not "fine".

| Metric | Computed where | Rendered where | Verdict |
|---|---|---|---|
| Hero "oldest wait" + account attribution | `CommandCenterPanel.BuildHeroSubtext` | Command centre hero line | **BROKEN → fixed** (F-METRICS-01) |
| Per-account "Longest wait" | `CommandCenterPanel.xaml.cs:1810` via `GetAwaiting(id, windowStart, windowEnd)` | Account card | Correct — window-bounded, agrees with the awaiting pill by design |
| Awaiting total vs per-account sum | `OversightRollupBuilder` / snapshot | Hero + cards | Correct — verified live: 165 + 131 + 84 = 380, matches the hero's "380 total across 3 accounts" |
| Caught-up % overall | not traced | Hero | Not examined |
| On-time % per account | not traced | Card pill | Not examined |
| First Response Time / SLA met % | not traced | KPI row | Not examined |
| Messages/day, trend deltas, sparklines | not traced | KPI row | Not examined |
| Review counts / Google rating | not traced | Reviews page | Not examined |
| Weekly report + anomaly detection | not traced | Reports page | Not examined |

## Findings

### F-METRICS-01 — The command centre hero attributes the oldest waiting customer to the wrong account, contradicting that account's own card

- **Severity:** S1
- **Confidence:** confirmed (observed live, on real data, before and after the fix)
- **Where:** `UnifiedMessenger/Controls/CommandCenterPanel.xaml.cs:1506` (`BuildHeroSubtext`)
- **Status:** **FIXED** in `v4.99.2`. Regression tests: `HeroSubtextAttributionTests` (6, green).
- **User-visible symptom:** The most prominent line on the product's main screen made a false statement
  about which branch had the worst-neglected customer. The owner reads "oldest 75d · Depilex DHA-2
  WhatsApp" and calls that branch manager about a 75-day-old customer who is not theirs — their oldest is
  50 days. Meanwhile the branch that *does* have the 75-day customer is not named. This is the precise
  failure mode the product exists to prevent, on its own headline.
- **Repro (before fix):**
  1. Connect several WhatsApp accounts where the account with the most awaiting chats is **not** the
     account holding the single oldest waiting chat.
  2. Open the command centre.
  3. Read the hero supporting line, then the per-account cards.
  4. Observed live: hero read `oldest 75d · Depilex DHA-2 WhatsApp · 12% caught up overall`, while that
     account's own card read `Longest wait: 50d`. The 75d belonged to *Depilex Men DHA-2 WhatsApp*.
- **Root cause:** Two independent facts were appended to a `parts` list and joined with `" · "`, which
  renders as one sentence. `parts[0]` was the oldest wait **across all accounts**; `parts[1]` was the
  `DisplayName` of the account with the **highest awaiting count** (`OrderByDescending(e => e.AwaitingCount)`).
  Nothing connected them, but the layout implied they were connected, and they coincide only when the
  busiest account also happens to hold the oldest chat.
  A **second, compounding defect**: the hero's oldest came from `OversightChatSnapshotService.BuildDigest(ids, null)`
  — an **unbounded** window — while each card's "Longest wait" uses `GetAwaiting(id, windowStart, windowEnd)`
  with the panel's `WindowRange()`. So the two figures could disagree even when they referred to the same
  account, purely from the window mismatch.
- **Fix applied:** The hero now derives its oldest wait from the **same window-bounded awaiting snapshot
  the cards use**, tracks which entity that wait belongs to, and names that entity — `oldest 75d (Depilex
  Men DHA-2 WhatsApp)`. The furthest-behind account is now explicitly labelled `furthest behind: <name>`
  so it reads as its own fact rather than as a qualifier on the duration. When both are the same account
  the parenthetical is dropped so the line does not repeat itself. Composition was extracted to
  `CommandCenterPanel.ComposeHeroSubtext` so the attribution contract is unit-testable without a live
  snapshot service.
  **Tradeoff:** the hero now iterates every entity's awaiting chats rather than reading one precomputed
  digest. On the live data (3 accounts, 380 awaiting) this was not perceptible, but it is O(awaiting
  chats) per render. If the render signature check ever stops suppressing redundant redraws at large
  account counts, this is a place to look.
- **Blast radius:** `BuildHeroSubtext` only; no other caller of `BuildDigest` was changed. The per-account
  cards were already correct and were not touched.
- **Evidence:**
  ```
  BEFORE (live, via UI Automation against the running app):
    hero: "oldest 75d · Depilex DHA-2 WhatsApp · 12% caught up overall"
    card: "Longest wait: 50d — expand to see who's waiting."      <- Depilex DHA-2 WhatsApp
    card: "Longest wait: 75d — expand to see who's waiting."      <- Depilex Men DHA-2 WhatsApp

  AFTER (same method, same data):
    hero: "oldest 75d (Depilex Men DHA-2 WhatsApp) · furthest behind: Depilex DHA-2 WhatsApp · 12% caught up overall"
    card: "Longest wait: 75d"                                     <- Depilex Men DHA-2 WhatsApp  ✓ agrees
  ```
  Cross-check that came back clean in the same capture: the hero's `380 total across 3 accounts` equals
  the sum of the three cards' awaiting counts (165 + 131 + 84 = 380), and `oldest since May 27, 2:58 PM`
  is consistent with 75 days before the capture date.

---

### F-METRICS-02 — A card could show "100% caught up" beside "4 awaiting", because the percentage rounds up

- **Severity:** S1
- **Confidence:** confirmed (reproduced by test against the real rollup, before and after)
- **Where:** `UnifiedMessenger/Services/Oversight/OversightRollupBuilder.cs:118` and `:150` (both percentage sites)
- **Where:** `UnifiedMessenger/Controls/CommandCenterPanel.xaml.cs:1258` (the weighted aggregate)
- **Status:** **FIXED** in `v4.99.9`.
- **User-visible symptom:** An account with 996 of 1000 chats handled computes 99.6%, which `Math.Round`
  turns into **100**. The same card renders "4 awaiting" from the exact counts, and the status glyph turns
  into a success tick. So the owner sees a green *100% caught up* immediately beside *4 awaiting* — two
  figures on one card contradicting each other, and the reassuring one is the wrong one. A busy owner
  reads the big green number and moves on, leaving four customers waiting.
  The defect runs in **both** directions: 1 of 1000 handled computes 0.1%, which rounds to **0**, telling
  an account that did some work that it did none.
- **Repro:**
  1. Build a rollup with `chatSnapshot: _ => (1000, 996)`.
  2. Pre-fix: `OnTimePercent == 100` while `AwaitingCount == 4`.
  3. Post-fix: `OnTimePercent == 99`, `AwaitingCount == 4`.
  Pinned by `CaughtUpRoundingTests` (10 tests), including the exact 99.5 midpoint and a 10,000-chat case.
- **Root cause:** `(int)Math.Round((double)part / total * 100)` with no reservation of the endpoints. 100
  and 0 are not ordinary values on this scale — they are claims ("nothing outstanding" / "nothing done")
  that the rounding could manufacture from counts that did not support them. Present at **two** rollup
  sites (the thread-based path and the snapshot-based path), and then **a third time** in the KPI tile,
  where a weighted average re-introduced it: a large fully-caught-up account beside a small one at 90%
  averages to 99.9 and rounds back to 100, so the headline tile would read "100% caught up" while the hero
  line beside it read "1 customer is waiting".
- **Fix applied:** One shared `OversightRollupBuilder.HonestPercent(part, total)` used by both rollup
  sites: 100 only when `part >= total`, 0 only when `part <= 0`, everything else clamped into 1..99. The
  KPI aggregate applies the same rule again at its own level — 100 only when **every** measured account is
  at 100. The headline can now under-claim by less than a point but can never over-claim, which is the
  safe direction for a number the owner acts on.
- **Blast radius:** Both percentage paths in the rollup plus the KPI tile. 122 tests across the rollup,
  response-time and chart suites confirm nothing else moved.
- **Evidence:** test output before the fix —
  ```
  CaughtUpRoundingTests.NinetyNinePointSixPercentDoesNotDisplayAsOneHundred [FAIL]
    4 customers are waiting but the card claims 100% caught up
  CaughtUpRoundingTests.RoundingDownNeverProducesZeroForAnAccountDoingSomeWork [FAIL]
    reported 0% despite 1 chat handled
  ```

---

### F-METRICS-03 — The card sparkline bucketed by UTC day while every other daily figure buckets by local day

- **Severity:** S2
- **Confidence:** confirmed (reproduced by test; fails only in a non-UTC zone — see caveat)
- **Where:** `UnifiedMessenger/Services/Oversight/OversightRollupBuilder.cs:50` and `:243` (pre-fix)
- **Status:** **FIXED** in `v4.99.9`.
- **User-visible symptom:** For an owner at UTC+5, every message between local midnight and 05:00 was filed
  under the **previous** day in the account card's 7-day sparkline, while the Analytics daily chart filed
  it correctly under today. The two views of the same account and the same period disagreed, and today's
  sparkline bar read low every morning until 05:00. Roughly a fifth of each day's messages land in that
  window.
- **Repro:** a thread with `LastMessageTime` at 00:30 local — pre-fix the newest sparkline bucket was 0,
  post-fix it is 1. Pinned by `TrendDayKeyingTests`.
- **Root cause:** `BuildTrend` keyed both sides of its day subtraction with `UtcDateTime.Date`, while the
  rest of the product keys locally: `MessageAnalyticsService.cs:590,596` buckets on
  `receivedAtUtc.LocalDateTime`, `:560` prunes with `DateTime.Now.Date`, and `KpiTrendStore.cs:1478` keys
  with `LocalDateTime`. A single mixed clock is all it takes for two on-screen views to disagree.
- **Fix applied:** `today` and the per-thread date both use `LocalDateTime.Date`, matching analytics.
  Changing only one side would have moved the boundary rather than closing it, so the comment at the
  subtraction says so.
- **Blast radius:** `BuildTrend` only — `today` had no other consumer, which was verified before changing it.
- **Evidence:** pre-fix `AMessageJustAfterLocalMidnightCountsAsToday` failed with `Expected: 1, Actual: 0`,
  while the five surrounding tests passed — isolating the boundary rather than a general miscount.
- **Test sensitivity caveat, stated plainly:** these tests only discriminate in a non-UTC time zone. On a
  UTC machine (typical CI) local and UTC days coincide and they would pass against the old code too. They
  are written against `TimeZoneInfo.Local` so they are meaningful on a developer machine in a real zone —
  this one runs at UTC+5 — but **they are not a reliable CI guard for this defect class.** A
  timezone-injectable clock would be needed for that, and was not built.

---

### F-METRICS-04 — "SLA met 100%" while replies were breaching the target, in the metric the README advertises

- **Severity:** S1
- **Confidence:** confirmed (reproduced by test against the real tracker, before and after)
- **Where:** `UnifiedMessenger/Services/Oversight/ResponseTimeTracker.cs:218` (headline SLA %)
- **Where:** `UnifiedMessenger/Services/Oversight/ResponseTimeTracker.cs` `GetDailyWithinThreshold` (the per-day trend series)
- **Status:** **FIXED** in `v4.99.10`.
- **User-visible symptom:** 499 of 500 replies inside the 15-minute target is 99.8%, which rounded to
  **100**. The KPI tile read "SLA met 100%" with the breach still counted in the sample line beside it.
  This is the number an owner would use to judge whether a branch is meeting its commitments — and the
  README advertises it by name. It failed in both directions: 1 of 501 within target rounded to **0%**,
  reporting that nothing met the target when something did.
- **Repro:** record 499 replies at 5 minutes and 1 at 60 minutes with a 15-minute threshold. Pre-fix
  `SlaCompliancePercent == 100`. Post-fix `99`. Pinned by `SlaPercentRoundingTests` (7 tests).
- **Root cause:** identical to F-METRICS-02 — `(int)Math.Round(part * 100.0 / total)` with the endpoints
  unreserved. This was the **third and fourth** occurrence of the same defect, in a second file. Each site
  looked innocuous alone; collectively the product could tell an owner they were finished when they were
  not, on more than one screen.
- **Fix applied:** the rounding rule was extracted to `Services/Oversight/MetricMath.HonestPercent` and all
  four sites now share it — the two in `OversightRollupBuilder` and the two here. Consolidating was the
  point: this defect had already been fixed once and recurred in a file I had not yet read, so leaving
  four copies would have guaranteed a fifth.
- **Blast radius:** SLA compliance headline and the daily SLA trend series. 129 tests across every metric
  suite (rollup, capability, response-time, chart-series, analytics presenter) confirm nothing else moved.
- **Evidence:** test output before the fix —
  ```
  SlaPercentRoundingTests.OneBreachAmongManyKeepsSlaBelowOneHundred [FAIL]
    a reply breached the 15-minute target but SLA met reads 100%
  SlaPercentRoundingTests.ASingleReplyWithinTargetDoesNotRoundAwayToZero [FAIL]
    one reply met the target but SLA met reads 0%
  SlaPercentRoundingTests.DailyWithinThresholdAppliesTheSameHonestyRule [FAIL]
    day 'Sun' had a breach among 500 replies but reads 100%
  ```

---

## FRT and SLA — everything else traced came back CLEAN

Recorded so these are not re-audited. All read directly at `ResponseTimeTracker` and `ChartSeriesBuilder`.

| Check | Verdict | Evidence |
|---|---|---|
| Median / p90 percentile maths | **correct** | `Percentile` uses a clamped ceiling rank; a single sample returns itself for both median and p90 rather than 0 or an index error. Pinned by `ASingleSampleGivesACoherentMedianAndPercentile`. |
| Empty sample set | **correct** | `GetStats` returns `HasData: false` with zero counts — it does not report a perfect score for an account with nothing measured. Pinned by `NoSamplesReportsNoDataRatherThanAPerfectScore`. |
| Outlier contamination | **correct** | `MaxCredibleResponse = 7 days` caps what can become a sample, so one chat answered after a fortnight cannot wreck the median. |
| Pre-existing backlog poisoning FRT | **correct, and thoughtfully done** | `Observe` records a `watchStart` per account and refuses to sample any inbound that predates it. Without this, the first sync after connecting an account would attribute the owner's entire historical backlog to their response time and report a huge misleading FRT. |
| "Answered today" day-keying | **correct** | `sample.AnsweredAtUtc.ToLocalTime().Date == DateTime.Today` — local on both sides, consistent with analytics (and with the F-METRICS-03 fix). |
| Daily SLA series day-keying | **correct** | `ToLocalTime().Date` against `DateTime.Today`. |
| Trend delta divide-by-zero | **correct** | `ComputeDelta` returns `MetricDelta.None` when `previous <= 0`; the comment reads *"dividing by zero is a lie."* No fabricated percentage from a zero baseline. |
| Trend arrow vs good/bad | **correct** | `MetricPolarity.LowerIsBetter` is applied to response time and `HigherIsBetter` to SLA. Crucially the arrow direction tracks the *actual* change while the sentiment (colour) tracks good/bad — they are separate fields, so a rising response time shows an up arrow styled as adverse rather than a misleading "improvement". |

**Observation, not a finding:** `GetStats` treats `slaThresholdMinutes <= 0` as "everything is within SLA",
which would render 100% compliance for a disabled threshold. The settings default is 15 and I found no UI
path that sets 0, so this is not reachable today — but "disabled" arguably ought to suppress the metric
rather than score it perfect. Left alone deliberately rather than changed on speculation.

---

### F-METRICS-05 — WhatsApp's own notice account was counted as a customer waiting for a reply, on the default path

- **Severity:** S1
- **Confidence:** confirmed (found in the owner's live persisted snapshot, fixed, and re-verified there)
- **Where:** `UnifiedMessenger/Assets/Scripts/whatsapp-store-bridge.js:619-623` (pre-fix filter)
- **Where:** `UnifiedMessenger/Services/Oversight/OversightChatSnapshotService.cs:152` (unfiltered load path)
- **Status:** **FIXED** in `v4.99.11`.
- **User-visible symptom:** `0@c.us` — WhatsApp's official account, which sends one-way notices you
  **cannot reply to** — appeared in the awaiting count as a customer named "WhatsApp Business". Because
  replying is impossible, it could never be cleared: it had been sitting in the owner's backlog for
  **26 days** at the time of the audit. On a busy account it is +1 noise; on a well-run account showing
  "1 awaiting", it is 100% of the number, and the owner would go hunting for a customer who does not exist.
- **Repro:** verified directly in `%LOCALAPPDATA%\UnifiedMessenger\oversight-snapshot.json`:
  ```json
  {"conversationKey":"0@c.us","customerName":"WhatsApp Business","unread":0,
   "lastActivityUtc":"2026-07-15T06:07:23+00:00","isAwaiting":true, ...}
  ```
- **Root cause:** **producer divergence.** `whatsapp-adapter.js:1263-1270` excludes `@g.us`, `@broadcast`,
  `@newsletter`, `status@` **and `0@`**, with a comment explaining exactly why the last one matters.
  `whatsapp-store-bridge.js` excluded only the first three — and the store bridge is the **preferred**
  path (`UseStoreBridge` defaults true), so the unfiltered version is what users actually saw. The bridge
  also carried a `chat.isGroup === true` check which AGENTS.md explicitly documents as non-existent on the
  model: always `undefined === true`, i.e. dead code implying a safety net that was not there.
- **Fix applied:** the filter now lives in `ChatEntryParser.IsNonCustomerConversation` — the single point
  **both** producers funnel through — so it cannot diverge again. `whatsapp-store-bridge.js` was brought
  into line as well (so the row never even arrives) and the dead `isGroup` check removed with a note.
  Critically, the same guard was added to `OversightChatSnapshotService`'s **load** path: without it, a
  snapshot written by an older build keeps its bad rows across restarts until a re-scan happens to replace
  it, so an upgrading install would carry the defect forward.
- **Blast radius:** awaiting counts, caught-up %, the needs-reply list, and the oldest-wait attribution —
  everything downstream of the snapshot. 81 tests across the parser, persistence, snapshot-service, rollup
  and rounding suites confirm nothing else moved.
- **Evidence (owner's real data, before → after):** `0@` conversation keys **1 → 0**.

---

### F-METRICS-06 — 2.8% of message previews rendered as raw base64 image data

- **Severity:** S2
- **Confidence:** confirmed (measured in the owner's live snapshot, fixed, re-measured)
- **Where:** `UnifiedMessenger/Services/Oversight/ChatEntryParser.cs` (preview passthrough, pre-fix)
- **Status:** **FIXED** in `v4.99.11`.
- **User-visible symptom:** The needs-reply list showed
  `/9j/4AAQSkZJRgABAQAAAQABAAD/4gHYSUNDX1BST0ZJTEUAAQ…` where the README promises "the customer's name,
  phone number and **the actual text of their last message**". **85 of 3,027 stored previews (2.8%)** were
  base64 JPEG payloads. For those chats the owner cannot tell what the customer sent without opening the
  conversation, which defeats the purpose of the list.
- **Repro:** count previews matching a base64 image signature in `oversight-snapshot.json` — 85 before.
- **Root cause:** an image message's preview arrives as the encoded thumbnail payload rather than a text
  body, and the parser passed it straight through. Nothing validated that a "preview" was human-readable.
- **Fix applied:** `ChatEntryParser.SanitizePreview` replaces a preview carrying a JPEG (`/9j/`), PNG
  (`iVBORw0`), GIF (`R0lGOD`) or `data:image` signature with the label **"Photo"**. Detection is
  deliberately narrow — anchored on the standard signatures at the start of the string — so ordinary
  message text is never relabelled; four tests cover near-miss text such as
  `"/9 out of 10 would recommend"` and `"data on my bill looks wrong"`. Applied on the load path too, so
  existing snapshots are cleaned on upgrade rather than only on the next scan.
- **Blast radius:** preview display only; no metric depends on preview text.
- **Evidence (owner's real data, before → after):** base64 previews **85 → 0**, `"Photo"` labels **0 → 84**.
  The arithmetic cross-checks: one of the 85 belonged to the `0@c.us` row now removed entirely by
  F-METRICS-05, leaving exactly 84 to relabel.

---

## Message volumes and the end-of-day projection — CLEAN

| Check | Verdict | Evidence |
|---|---|---|
| Day bucketing for volumes | **correct** | `ApplyReceivedIncrement` keys on `receivedAtUtc.LocalDateTime` for both the daily and hourly-by-day buckets — consistent with the F-METRICS-03 fix. |
| End-of-day projection divide-by-zero | **correct** | `fraction = total > 0 ? … : 0`, and a `fraction <= 0.05` floor returns "just what's in so far" rather than extrapolating wildly from a nearly-empty day. |
| Projection undercutting actuals | **correct** | `Math.Max(soFar, …)` — the projection can never be below what has already arrived. |
| Projection day/hour clock consistency | **correct** | `todayKey` and `nowHour` both from `DateTime.Now` (local). |
| "Busier than usual" threshold | **correct** | Requires `HasData` on both sides and `AveragePerDay > 0` before comparing at 1.4×. |
| Group / Status / broadcast / channel exclusion | **correct in the snapshot** | Verified in the owner's real data: `@g.us` keys **0**, `status@` keys **0**. Only the `0@` case had escaped (F-METRICS-05). |

---

### F-METRICS-07 — The weekly report's share percentages could round into sentences that contradict themselves

- **Severity:** S2
- **Confidence:** confirmed (reproduced by test against the real report builder, before and after)
- **Where:** `UnifiedMessenger/Services/Analytics/BusinessReport.cs:168` (returning-customer rate)
- **Where:** `UnifiedMessenger/Services/Analytics/BusinessReport.cs:182` (busiest account's share of volume)
- **Status:** **FIXED** in `v4.99.12`.
- **User-visible symptom:** The report is the artefact an owner reads carefully and may forward to a
  manager, so self-contradiction there is disproportionately damaging. Both cases produce nonsense in a
  single sentence:
  - *"996 messages this week — **100%** of all customer volume"*, in a call-out that only fires when **two
    or more** accounts are active and therefore names one account as "busiest" among several.
  - *"**100%** of the 1000 customers who messaged this week had contacted you before; **3** reached out for
    the first time."*
  The inverse also occurred: 3 of 1000 returning rounds to **0%** while the sentence names returning
  customers.
- **Repro:** build a report with accounts `[Main: 996, Branch: 4]`, or with 997 returning and 3 new.
  Pinned by `BusinessReportSharePercentTests`.
- **Root cause:** the fifth and sixth instances of the rounding defect first fixed in `v4.99.9`. Both are
  shares bounded 0–100 that used plain `Math.Round`.
- **Fix applied:** both now use the shared `MetricMath.HonestPercent`. **Deliberately not changed:** the
  volume delta at `:92` (`MessagesThisWeek` vs `MessagesLastWeek`) is a *change* percentage, legitimately
  unbounded — tripling volume really is "up 200%" — so applying a 0–100 clamp there would have been a new
  bug. A test pins that it still reports 200%.
- **Blast radius:** two report strings. 153 tests across every metric and parser suite confirm nothing else
  moved.
- **Evidence:** three failing tests before the fix; `AnAllReturningWeekStillReportsOneHundredPercent`
  confirms the honest 100% survives.

---

### F-METRICS-08 — The report's headline sentence lower-cased the owner's own branch names

- **Severity:** S3
- **Confidence:** confirmed
- **Where:** `UnifiedMessenger/Services/Analytics/BusinessReport.cs:223` (pre-fix)
- **Status:** **FIXED** in `v4.99.12`.
- **User-visible symptom:** `BuildSummary` applied `ToLowerInvariant()` to insight titles when composing
  the report's opening line. That reads tidily until a title contains an account name — the owner's real
  data produces *"Focus this week: depilex dha-2 whatsapp may be neglected"*, mangling their own branch
  naming in the single most prominent sentence of a document they may forward to a manager.
- **Repro:** an account named "Depilex DHA-2 WhatsApp" with `AwaitingNow >= 3` and no measured replies
  triggers the neglected-account warning, whose title carries the name. Pinned by
  `TheSummaryPreservesAccountNameCasing`.
- **Root cause:** lower-casing was applied to make titles read as clause fragments, without accounting for
  titles that embed proper nouns. There is no way to lower-case safely here — the account name is
  user-supplied and unbounded.
- **Fix applied:** titles are joined verbatim. The sentence still reads correctly because insight titles
  are already written as standalone clauses.
- **Blast radius:** one string.

---

## Weekly report and anomaly detection — everything else CLEAN

| Check | Verdict | Evidence |
|---|---|---|
| Volume-trend divide-by-zero | **correct** | `MessagesLastWeek == 0` is special-cased into a "first period of tracked activity" insight before any division. |
| Volume-trend noise floor | **correct** | Only reported when `Math.Abs(deltaPct) >= 15`, so ordinary week-to-week wobble is not raised as a finding. |
| Response-time anomaly false alarms | **correct, and thoughtfully bounded** | "Slower" requires `FrtSamplesLastWeek > 0` **and** `lastWk > 0` **and** `thisWk >= lastWk * 1.5` **and** `thisWk >= 10` minutes. The floor is what stops a 1→3 minute change being reported as a degradation; the comment says exactly that. |
| Over-SLA anomaly | **correct** | Guarded by `SlaThresholdMinutes > 0`, so a disabled target cannot trigger it. |
| Quiet-account anomaly | **correct** | `AwaitingNow >= 3 && FrtSamples == 0` — requires a real backlog, so a genuinely idle account is not accused. |
| Insight ordering | **correct** | Warn → Info → Good via a stable `OrderBy`, so the most actionable item leads and insertion order breaks ties deterministically. |
| Markdown export arithmetic | **correct** | Uses absolute deltas (`+12 vs last week`), not percentages, so it sidesteps the rounding class entirely. |
| SLA figure fed into the report | **correct as of v4.99.10** | `SlaMetPercent` is supplied by `ResponseTimeTracker`, so the report inherited the F-METRICS-04 fix automatically. |

---

### F-METRICS-09 — The review reply rate could read "100% replied" beside a list of unanswered reviews

- **Severity:** S2
- **Confidence:** confirmed (reproduced by test, before and after)
- **Where:** `UnifiedMessenger/Services/Oversight/GoogleReviewSnapshotService.cs:35` (`ReplyRatePercent`)
- **Status:** **FIXED** in `v4.99.13`.
- **User-visible symptom:** The review panel's subtitle renders
  `"{ReplyRatePercent}% replied ({Total} on this page)"` while a separate branch of the same panel
  highlights reviews that still need a reply. With 996 of 1000 answered the subtitle read **"100% replied"**
  directly above a list of four reviews awaiting a response. The inverse also occurred: 1 of 1000 answered
  rounded to **0% replied**.
- **Repro:** `ReviewHealth` with `Answered: 996, Unanswered: 4` — pre-fix `ReplyRatePercent == 100`,
  post-fix `99`. Pinned by `ReviewReplyRateTests` (6 tests).
- **Root cause:** the **seventh and final** instance of the rounding defect first fixed in `v4.99.9`.
- **Fix applied:** `MetricMath.HonestPercent(Answered, Total)` **inside the existing `Total > 0` guard**.
  That guard is load-bearing and was deliberately kept: `HonestPercent` returns 100 for a zero total —
  correct for "nothing outstanding" in the oversight rollup, but here it would tell a business with **no
  reviews at all** that it had replied to 100% of them. A test pins the zero-review case at 0.
  This is worth recording as a general lesson: consolidating the six earlier sites was right, but applying
  the shared helper mechanically to the seventh would have introduced a new defect. The helper's
  `total <= 0` contract has to be read at each call site, not assumed.
- **Blast radius:** one computed property on the review-health record.
- **Evidence:** three failing tests before the fix, including
  `"4 reviews are unanswered but the panel reads 100% replied"`.

---

## Google rating and review scraping — CLEAN

| Check | Verdict | Evidence |
|---|---|---|
| Rating extraction | **correct, and matches the documented trap** | Read from an `aria-label` matching `/Rated\s+([0-5][.,]\d)\s+out\s+of\s+5/i`, not from `innerText`. AGENTS.md records that `innerText` renders rating and count run together (`"4.6239 Google reviews"`); the code takes the aria-label precisely to avoid that. |
| Lifetime review total | **correct** | Anchored on the rating — `/([0-5][.,]\d)[^\d]{0,6}([\d,]+)\s+Google\s+reviews/i` — so the two split correctly instead of a naive `([\d,]+)` yielding `6239` for `4.6` + `239`. The `[^\d]{0,6}` also tolerates a layout that separates them. |
| Total fallback safety | **correct** | The no-rating fallback requires a non-digit/non-dot boundary before the count, so it still cannot slice a number out of the middle of another one. |
| Decimal comma locales | **handled** | Both patterns accept `[.,]` and normalise with `.replace(',', '.')`. |
| Rating scrape cost | **throttled** | `RatingRefreshInterval` (6h), because each scrape costs a visible navigation round-trip. |
| Google message metrics | **correctly absent** | No awaiting-reply, FRT or message-count plumbing exists for the Google channel, consistent with Google Business Messages having been shut down in July 2024. |

**Scope note, stated honestly:** the review counts are explicitly labelled "on this page" in the UI, which
is accurate — they cover the loaded reviews page rather than the full lifetime history, and the code
comments say so. I did **not** verify the row-count bump (`__umGRBumpRows`) against a live Google page, so
how many reviews a "page" actually covers in practice is unverified.

## What was NOT covered

Stated plainly so this is not mistaken for a completed audit of the highest-stakes domain.

- **Not examined at all:** caught-up %, on-time % per account, First Response Time, SLA met %,
  answered-today, messages/day, trend deltas, KPI sparklines, review counts, the Google profile rating,
  the weekly business report and its anomaly detection. That is the majority of the product's numbers.
- **No boundary testing was performed.** Timezone/local-day keying, DST 23h and 25h days, empty and
  single-sample sets, division by zero / NaN reaching the UI, group/status/broadcast exclusion
  consistency, chats predating tracking, snooze and mark-handled overrides, quiet hours, and date-range
  re-keying are all **unverified**. The brief called these out specifically and none were checked.
- **Only one cross-figure contradiction was hunted and found.** There was no systematic sweep for others;
  the one found surfaced from a UI Automation dump taken for a different purpose. Given that a
  contradiction was found on the very first screen examined, the prior for more of them elsewhere should
  be treated as high, not low.
- **The window-mismatch class was not generalised.** `BuildDigest(ids, null)` (unbounded) versus
  `GetAwaiting(id, windowStart, windowEnd)` (bounded) is a trap that produced this S1. Other callers of
  `BuildDigest` with a null window were **not** audited for the same defect. This is the single highest-value
  next step in this domain.
