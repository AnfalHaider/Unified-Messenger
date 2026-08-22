# Review Desk — build spec

**The design is fixed.** The approved mock is the Review Desk artifact; this file is the wiring contract for
it. Nothing here re-opens layout decisions — it maps every element on that page to the data behind it, says
what exists today, and says what the element renders **before** its data exists.

> **The rule that governs every row below.** A tile whose data we cannot yet compute renders as a stated
> gap — never as a plausible number. The brief is "no wrong numbers", and a fabricated median reply time is
> worse than an empty one, because the owner cannot tell it is fabricated.

---

## 1. What the mock shows that we cannot currently know

Listed first because it is the part that decides the build order. None of this is a layout problem.

| Element in the mock | Why it is not obtainable today |
|---|---|
| `covers all 239 reviews` | We read one page of 50. Pagination is off (`MaxPages = 1`) after it produced counts 2–3× too high. |
| `4.6 · 239 reviews · all locations` | 239 was a placeholder. Real totals are **992 / 435 / 244 = 1,671**, and the three ratings differ (4.6 / 4.7 / 4.6). A single "all locations" rating has to be a computed weighted mean, and must be labelled as one — Google does not publish it. |
| Rating sparkline, `4.4 → 4.6 up 0.2` | Nothing is stored between scrapes. The service holds a `ConcurrentDictionary` that dies with the process. |
| `New this month 14 ▲3` | Needs per-review dates plus history. We scrape a relative age string ("2 days ago") for pending reviews only. |
| `Median reply time 1.4d` | We never see when a reply was sent. Not on the reviews manager page in any form we have found. **This tile may be undeliverable** — see §5. |
| `Quietest branch · no review in 19 days` | Needs history. |
| `Reply rate 88% ▲9` | The rate ships; the **▲9** delta needs history. |
| Themes strip (`Three reviews mention waiting time…`) | Needs Ollama plus review bodies. We currently capture text for pending reviews only (~8/account), not the answered ones. |
| Suggested reply | Needs Ollama. The text we have is enough. |
| Ask for a review | Needs a WhatsApp contact matched to a recent visit. "visited Tuesday · thanked you" is a real inference from `ContactHistoryStore`, but the join does not exist. |
| Sidebar unanswered badges | Tier 5. |

---

## 2. Element → data map

### Header
| Element | Source | State |
|---|---|---|
| `Reviews` / `3 Google locations` | `Registry.Instances` filtered to `googlebusiness` | **ships** |
| `checked 4 min ago` | `GoogleReviewSnapshotService.LastCapturedUtc` | **exists, not surfaced** |
| `covers all 239 reviews` | `ReviewCoverage.Describe` + `ProfileRating.Total` | **reword** — must say "covers the 50 most recent of 1,671" until pagination is fixed |
| `Last 30 days ▾` | — | **defer to Tier 2**; meaningless without history |
| `Check now` | `ReviewHealthPanel.RefreshAsync(allowNavigate: true)` | **ships** |

### Hero
| Element | Source | State |
|---|---|---|
| Big rating + stars | `ProfileRating.Rating` per account, weighted by `Total` | **new** — compute + label as an average |
| `239 reviews · all locations` | sum of `ProfileRating.Total` | **ships** once summed |
| Sparkline + `up 0.2` | — | **Tier 2** · renders as "no history yet — building from today" |
| `Needs a reply 7` | `sum(ReviewHealth.Unanswered)` | **ships** |
| `oldest waiting 6 days` | `ReviewAge.SortKey` max over the queue | **ships** |

### Critical alert strip
| Element | Source | State |
|---|---|---|
| One-star callout | top of `ReviewQueue.Build` when `Urgency == Critical` | **ships** — queue already ranks it first |
| `mentions a refund` | keyword scan of review text | **new, cheap** — same shape as `ConversationTopic` |
| `Open it →` | `FocusReviewAsync` | **ships** |

### KPI strip
| Tile | State |
|---|---|
| Unanswered · `2 are 3 stars or below` | **ships** — counts over the queue |
| Oldest waiting | **ships** |
| Reply rate (value) | **ships** · delta is **Tier 2** |
| New this month | **Tier 2** |
| Median reply time | **blocked** — see §5 |
| Quietest branch | **Tier 2** |

### Queue rows
| Element | State |
|---|---|
| Avatar, name, branch, age | **ships** |
| `★1 · Needs you personally` | **ships** — `ReviewQueue.UrgencyOf` + rank |
| `Refund mentioned` / `Waiting time` / `Praises staff` | **new, cheap** — keyword topics over review text |
| Quote | **ships** |
| Suggested reply block | **Tier 3** |
| `Done` / `Snooze` | **new** — needs a review override store mirroring `AwaitingOverrideStore` |
| `J/K/Enter/R/D/S` | **partly ships** — ↑↓/Home/End/Enter verified live; J/K/R/D/S to add |

### Bottom panels
| Panel | State |
|---|---|
| By branch (rating + gained) | rating **ships**; `+7 / +5 / +2` is **Tier 2** |
| Ask for a review | **Tier 4** |

---

## 3. Build order

Each step leaves the page shippable.

1. **Layout to spec.** Rebuild `ReviewsPage` as hero + alert + KPI strip + queue + panels, replacing
   `ReviewHealthPanel`'s card list. Un-wired tiles render their stated-gap state. Nothing invented.
2. **Aggregate hero + honest coverage.** Weighted rating, summed totals, real coverage wording.
3. **Topic chips + refund detection.** Keyword pass over review text; reuse the `ConversationTopic` approach.
4. **Done / Snooze + full triage keys.** `ReviewOverrideStore`, then J/K/R/D/S.
5. **Tier 2 — history.** Persist a daily snapshot per account; unlocks sparkline, deltas, new-this-month,
   quietest branch, branch gains, and the date filter. Everything reads as "building from today" until it
   has data, and says so.
6. **Tier 3 — themes + drafted replies** via Ollama, gated on `EnableLocalAi`.
7. **Tier 4 — ask for a review.**
8. **Tier 5 — badges, toast, palette.**

---

## 4. Honest states for un-wired tiles

Not placeholders — sentences.

- Sparkline: `No history yet — starts building today.`
- New this month / quietest branch: `Needs a few days of history.`
- Ask for a review: `Not built yet.` (panel hidden rather than teasing)

---

## 5. The one tile that may never be honest

**Median reply time.** It requires knowing when each reply was posted. That is not on the reviews manager
page in any form found so far, and Google shows only the review's own age. Options: drop the tile, or
replace it with **time-to-first-reply measured from when *we* first saw the review unanswered** — which is
honest but only measures replies made after the app was installed, and must be labelled that way.

Recommendation: replace it, labelled `since install`. Do not ship a number that implies historic coverage.
