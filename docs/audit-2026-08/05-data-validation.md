# Increment 105 — data validation

**Run:** 2026-08-29 · **Against:** v4.99.71, the owner's **live** store · **Method:** figures recomputed
independently from the JSON stores and compared to what was on screen.

This is the first time any displayed figure in this product has been checked against its underlying data.
`remaining-work.md` §0.4 had conceded: *"not one displayed figure has been checked against reality."*

**Store reads were verified trustworthy first** — no container shadow of `%LOCALAPPDATA%\UnifiedMessenger`
exists, so reads fall through to the real files. Nothing was written to the store.

---

## Result: every figure checked is correct

| Figure | On screen | Recomputed from store | Verdict |
|---|---|---|---|
| Reply-time median, **Today** | `31m` | 30.98 → 31 | ✅ |
| Reply samples, **Today** | `1 reply` | 1 | ✅ |
| SLA met, **Today** | `0%` | 0/1 = 0% | ✅ |
| Reply-time median, **7 days** | `1 min` | 1.45 (nearest-rank) | ✅ |
| SLA met, **7 days** | `83%` | 25/30 = 83% | ✅ |
| Accounts with data | `3 accounts` | 3 | ✅ |
| Live + backlog = total | `40 + 68 = 108` | internally consistent | ✅ |
| SLA threshold | 15 min | `settings.json` = 15 | ✅ |
| Backlog cutoff | 7 days | `settings.json` = 7 | ✅ |

---

## The one apparent contradiction, resolved

The dashboard read **`SLA met 0%` · `Response time 31m`** while Analytics read **`SLA Met 83%` ·
`Response Time 1 min`** — the same two nouns, an order of magnitude apart, on two screens.

**Both are correct.** The dashboard's metrics selector was on **Today**; Analytics was on **This week**.
At 03:00, "today" held exactly one reply, and that reply took 31 minutes. The week held 30.

Recomputed both ways from the same 30 samples, and both match the screen exactly. **No defect.**

What this does expose is a presentation asymmetry worth noting: the response-time tile discloses its
denominator (`median · 1 reply`), and the **SLA tile does not** — it says `0%` with the sub-label
`replied within 15 min` and no sample count. A 0% computed from one reply and a 0% computed from two
hundred look identical. Recorded in §Findings.

---

## A near-miss worth recording

The 7-day median first computed as **1.64** against a screen reading **1 min**, which looked like a
rounding defect — `FormatMinutes(1.64)` returns `"2 min"`.

It was not a defect. `ResponseTimeTracker.Percentile` uses the **nearest-rank** method
(`ceil(0.5 × n) − 1`), giving `1.45 → "1 min"`; the first calculation used mean-of-middles. Both are
legitimate definitions of a median, and the app applies nearest-rank consistently to median *and* p90.

**Recorded because the wrong conclusion was one step away.** A figure that disagrees with a hand
calculation is not yet a bug — the definition has to be checked first.

---

## The awaiting counts: 298 raw → 108 shown, and why that is right

Counting `isAwaiting` directly in `oversight-snapshot.json` gives **298** across 2,098 chats. The screen
shows **108**. That gap is the whole design of the feature, not a loss:

`IsEffectivelyAwaiting` = `IsAwaiting` **and not** automatically closed **and not** suppressed by an
override. Overrides are empty (`awaiting-overrides.json` holds `{"instances":{}}`), so the reduction is
entirely `IsAutomaticallyClosed` → `ReplyNeed.Classify`, gated on `filterClosedConversations` (confirmed
`true` in settings).

Of the 298, **135 have no readable preview** — and their message types show the classifier is discriminating
rather than blanket-closing:

| Type | Count | Should it need a reply? |
|---|---|---|
| `call_log` | 67 | Depends on the call outcome — which the classifier takes |
| `notification_template` | 32 | No — WhatsApp system message |
| `protocol`, `e2e_notification`, `pinned_message`, `ciphertext` | 16 | No — protocol noise |
| `image`, `ptt`, `video`, `sticker` | 18 | **Yes** — and the code says so explicitly |

The remaining ~163 have text and are closed or kept by the word rules.

That last row matters: the class docstring records that an earlier version of this fix *"nearly got it badly
wrong"*, because an uncaptioned photo and a vanished message both produce an empty preview and need opposite
treatment. The data confirms the current code distinguishes them.

**Limit stated honestly:** `ReplyNeed.Classify` combines word rules with a cached local-model verdict and
cannot be reproduced outside the app, so the 298 → 108 reduction is verified as *attributable and
well-founded*, not reproduced sample-by-sample.

---

## Findings

| # | Finding | Severity |
|---|---|---|
| **V1** | The **SLA tile does not disclose its denominator**. `SLA met 0%` from one reply is visually identical to 0% from two hundred. The response-time tile beside it already shows `median · N replies`; the SLA tile should match. | **S3** |
| **V2** | The dashboard defaults its metrics window to **Today**. Opened early in the day it headlines figures built from one or two samples, while the same nouns on Analytics show the week. Both are labelled, neither is wrong, and the pairing still misleads. Related to owner-decision §1 (the SLA tile). | **S3** |
| **V3** | No calculation error found in any figure checked. | — |

**Not covered:** the Reviews page figures (rating, lifetime total, unanswered, reply rate) were not
recomputed — they come from a live scrape rather than a local store, so validating them needs a Re-sync
observed end to end. Messages/day, busiest window and the per-account cards were checked for internal
consistency only.
