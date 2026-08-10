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
