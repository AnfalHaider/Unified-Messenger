# Findings — Per-view state matrix

§6.4 asks for 6 pages, ~20 controls and ~15 dialogs each exercised across ~14 states. **That full matrix
was not covered.** What follows is what *was* exercised, what it showed, and — stated plainly — what was
not touched, so this is not mistaken for a completed sweep.

## Approach

Rather than sample the matrix evenly, I targeted the cells most likely to hide defects **and impossible to
reach with the owner's own data**: many accounts, very long names, and non-Latin / right-to-left text. The
owner's 11 accounts are all short Latin names, so none of that had ever been rendered.

The registry was replaced with a synthetic 20-account set (owner data backed up and hash-verified first,
restored and re-verified after — all files matched):

| Stress case | Value used |
|---|---|
| Very long name | `Depilex Gulberg III Main Boulevard Lahore Flagship Salon and Spa Reception Desk Line Two` (87 chars) |
| Right-to-left | `ڈیپیلیکس ایف الیون اسلام آباد` (Urdu — the owner's own market) |
| CJK | `深圳市宝安区美容院前台` |
| Accents + apostrophe | `Café Ñandú — Señor Müller's Salon` |
| Emoji incl. ZWJ sequence | `Depilex 💇‍♀️ F-11 ✨ VIP` |
| Minimal | `A` |
| Volume | padded to **20 accounts** |

## Result: this cell of the matrix is CLEAN

| Check | Result |
|---|---|
| App survives 20 accounts incl. RTL/CJK/emoji | **yes** — alive, 255 MB, no crash, no error in `app.log` |
| All stress names render | **yes** — every one present in the UI tree |
| Accessible names correct for non-Latin | **yes** — e.g. `深圳市宝安区美容院前台, WhatsApp` |
| Layout overflow past the window | **0 elements** extend beyond window bounds |
| Clipping of the 87-character name | none observed; no element exceeded the window rect |
| Sidebar reachability at 20 accounts | **all reachable** — 6 laid out initially, 12 after scrolling to the end, 11 newly materialised |
| Virtualisation | working — unrendered rows report infinite bounding rects and materialise on scroll |

**AGENTS.md lists "Sidebar-rail density at very large counts" as an open Phase 3 item.** At 20 accounts it
holds up: the rail scrolls, virtualises correctly, and every account — including the 87-character and RTL
names — is reachable. That item can be considered addressed at this scale. It was **not** tested at 50+.

A methodological note worth keeping: the first measurement pass reported "3 of 20 accounts" and looked like
a serious finding. It was an artefact — unrendered rows return `∞` bounding rectangles, which the naive
cast silently dropped. Handling infinity explicitly turned a false S2 into a clean result. **Virtualised UI
must be measured by scrolling, not by a single snapshot.**

## What was NOT covered — the majority of the matrix

States exercised somewhere in this audit:
- **first-run / empty** (v4.99.14, on a genuinely clean install)
- **no-data-yet** (empty states observed: "No accounts connected yet", "No professional accounts yet",
  "No Google Business account connected", "Reviews — Not scanned yet")
- **one account** and **many accounts (20)**
- **long names, non-Latin, RTL, CJK, emoji**
- **stale / degraded session** (v4.99.3, v4.99.8)

States **not exercised at all**:
- **offline** — no network-loss test was run. For an app whose entire input is web clients, this is the
  most conspicuous gap in the list.
- **error** states beyond corrupt-file and failed-read
- **quiet hours active**
- **AI on vs AI off** — `EnableLocalAi` was left at its stored value throughout; the AI-off degradation
  path (heuristic fallback) was never observed
- **all-caught-up** — the owner's data never reached zero awaiting, and no synthetic all-caught-up state
  was constructed, so the "No customers are waiting" hero branch is **unverified in the running app**
- **everything-on-fire** beyond the owner's real 343-awaiting backlog
- **date-range interactions** across the window selector

Surfaces **not enumerated for state at all**:
- **~15 dialogs.** Only `AddInstanceDialog` was opened and inspected (v4.99.2). The other ~14 —
  delete, rename, edit-metadata, set-location, workspace-management, change-icon, weekly-report,
  account-detail, auto-update, pin-to-taskbar, confirm-permanent-delete — were **never opened**. Dialogs
  are where dead ends and missing empty states typically hide, and this is the single largest untested
  area of the product.
- **Reviews and Reports pages** were navigated through during the leak stress but never inspected
  state-by-state.
- **~20 controls** were not individually exercised.

## Honest summary

One high-value cell of the matrix was covered thoroughly and came back clean. The bulk — dialogs, offline,
AI-off, all-caught-up, quiet hours — remains untested. Treating this domain as "done" would be wrong.

---

# Remaining state-matrix cells (session 2, `v4.99.25`)

The four cells this document listed as never exercised — **all-caught-up**, **AI on vs off**, **quiet
hours**, **date-range interactions**. Reaching the first one is what found the defect below.

## F-STATE-01 — A green tick and "You're all caught up" while a branch was not being measured

- **Severity:** S2
- **Confidence:** confirmed by test; three tests fail against the original rule
- **Where:** `CommandCenterPanel.RenderHero` and `RenderBriefing`
- **Status:** **FIXED** in `v4.99.25`. Guard: `CaughtUpClaimTests` (15, green).

Both surfaces decided this with `totalAwaiting == 0` alone. **An account whose read fails contributes
zero awaiting**, because there is no data to count — so a branch dropping out of the rollup pushes the
total *down*, towards the reassuring answer. With the other branches genuinely quiet, the dashboard showed
a green tick and *"You're all caught up"* while a branch was not being measured at all.

The per-account card directly underneath was already rendering `ReadFailed` as "couldn't read". The
headline above it never looked: a grep for `ReadFailed` in the panel found it in the render signature and
in the card builder, and nowhere in `RenderHero`.

This is the same shape as the two worst findings of session 1 — the rounding lie and the hero
misattribution. **A figure that reassures precisely because data is missing.** That the owner's live data
has never reached zero awaiting is the only reason it had not been seen.

**The fix** is one shared decision, `Services/Oversight/CaughtUpClaim.cs`, used by the hero headline, the
hero subtext, the briefing heuristic **and** the AI prompt — the four places that could otherwise
contradict each other. It distinguishes:

| State | Claim | Wording |
|---|---|---|
| Nothing waiting, everything read | allowed | "You're all caught up" |
| Nothing waiting, an account failed to read | **blocked** | "Nothing waiting — but not everything was counted" + "1 account could not be read" |
| Nothing waiting, an account not loaded yet | **blocked** | …+ "1 account not loaded yet" |
| Nothing waiting in range, older threads still open | **scoped** | "Caught up on this range" + "300 older conversations are still open from before this range" |

Two deliberate decisions, both pinned by test:

- **Staleness does not block the claim.** A stale account has real data that is merely old, and sessions
  go stale routinely as the LRU reaper works. Blocking on it would make the claim almost never appear —
  trading a rare overclaim for a permanently useless headline.
- **Not-loaded is worded differently from failed-to-read.** v4.99.19 was specifically about not calling a
  sleeping account broken. It still cannot be counted as caught up, but it is not described as a failure.

The hero's "nothing waiting but incomplete" branch also stops rendering the big `0`. Showing a confident
zero there is the same overclaim in a different font — it states a count the app does not have.

**The AI path needed two changes, not one.** The prompt now tells the model about the unmeasured accounts,
because feeding it only the counts would have it write the same falsely-reassuring briefing in better
prose — and the AI line *replaces* the heuristic rather than sitting beside it. The insight cache
signature also gained the two counts: an account going from readable to unreadable changes none of the
other terms, so without them the cached briefing, written before anything broke, would be served unchanged
and the warning would never appear.

## The date-range cell — F-STATE-02, folded into the same fix

With **Today** selected, a conversation last active a week ago is deliberately kept out of the window's
awaiting count so it does not saturate today's number. That is the documented design and it is right. But
the hero's supporting line read *"No customers are waiting on a reply"* — unqualified — while those
customers were still waiting. `HistoricalOpenCount` existed and was surfaced only in a card's freshness
line ("N chats tracked").

Now the claim is **scoped rather than absolute** when older threads are open: "Caught up on this range",
with the count of what predates it. An unbounded window ("All time") has `HistoricalOpenCount == 0` by
construction, so it can never produce the scoped wording — pinned by test so the two stay consistent.

## Quiet hours — CLEAN, and the interesting part now has coverage

`QuietHoursTests` covered `IsQuiet` in isolation (wrap past midnight, same-day, zero-length, disabled).
What had no coverage was the claim in `OversightAlertMonitor`'s own comment: quiet hours must suppress the
toast **without consuming the alert edge**, so the backlog is announced once quiet hours end.

The ordering is correct — the `continue` sits *before* `Evaluate`, so `_alerted` is never set while quiet.
Get that backwards and an overnight backlog is swallowed permanently: the edge is consumed at 03:00, and
the morning tick sees "already alerted" and stays silent forever.

`QuietHoursInteractionTests` (7, green) composes the two pure pieces in the monitor's order and pins:
overnight suppression then a single morning announcement; the edge surviving; no repeat on later ticks;
re-arming after the backlog clears; and that the `threshold <= 0` guard has to stay *above* quiet hours,
because `Evaluate` itself fires on any count.

## AI on vs off — CLEAN

The degradation path is sound and needed no change beyond the honesty fix above:

- `EnableLocalAi` off → the heuristic string is used and the badge reads `✦`, not `✦ AI`.
- AI on but nothing cached yet → **the same heuristic**, with the AI text swapped in later via
  `OnInsightReady`. There is no empty state, no spinner, and no blank strip while the model thinks.
- `BuildInsightStrip` returns null when an entity has no chat data, no live data, or nothing awaiting, so
  quiet accounts stay quiet rather than emitting filler.

## What was NOT covered

- **The all-caught-up hero was not rendered live.** Reaching it needs zero awaiting across every account,
  which cannot be staged on this machine — the owner's real backlog is in the hundreds and the only way to
  fake it would be to overwrite the live snapshot. The decision is fully covered by test; the *rendering*
  of the three headline branches has not been seen on screen.
- **AI on/off was not toggled in the running app.** The heuristic-fallback path is still unobserved live,
  exactly as this document said before — the reasoning above is from the code, not from watching it.
- **Quiet hours was not observed suppressing a real toast**; the tests compose the pure decisions the
  monitor composes, not the monitor's own loop, which needs live instances and a UI dispatcher.
- **"Everything on fire"** beyond the owner's real backlog, and the remaining ~20 controls, are still
  untouched.
