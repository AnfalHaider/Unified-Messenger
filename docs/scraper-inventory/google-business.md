# Google Business — scraper inventory

**Observed:** 2026-09-02 · three live logged-in profiles, app WebView2 via CDP.

**Reviews and Q&A only, permanently.** Google Business Messages was shut down in July 2024 and the data
deleted. There is no message channel here and never will be — do not add awaiting-reply, FRT or
message-count plumbing for it. (`docs/MASTER-PLAN.md` §channel table, and the AGENTS.md verified-facts
section, already record this.)

This is the **only channel with real URLs**, which makes it the cheapest to navigate correctly.

---

## Two surfaces, on two different hosts

| Surface | Host | What lives there |
|---|---|---|
| **Reviews manager** | `business.google.com/reviews` | The review queue: individual reviews, star ratings, reply affordances, pagination |
| **Search merchant view** | `www.google.com/search?q=<business>&stick=…` | The **profile rating** and the **lifetime review total** — which are *not* on the manager page |

**CONFIRMED live:** all three accounts were sitting on the Search merchant view when the app was
launched, exactly as the AGENTS.md note describes — the rating scrape parks the WebView there. Anything
that runs afterwards and needs the reviews manager must be able to navigate back **from any google.com
host**. A `business.google.com`-only guard strands it, and the reviews scrape then reports `notreviews`
and gives up.

Navigating one tab back with `location.assign('https://business.google.com/reviews')` worked and loaded
the manager cleanly — **CONFIRMED** the return path is a plain navigation, not a redirect dance.

---

## The review-count layout is not a per-profile property — it changes over time

AGENTS.md records that Depilex Men shows the labelled layout (`… 435 Google reviews`) while DHA-2 and
F-11 show the bracketed one (`4.6 ★ (991) · Beauty salon`), with the words "Google reviews" appearing
nowhere.

**Measured today, DHA-2 renders BOTH:** its `aria-label` shapes were `"Rated N.N out of 5, "` *and*
`"Rated N.N out of 5, (N) …"`, and the page text contains `994 Google reviews` — the labelled layout,
on a profile previously recorded as bracketed-only.

The conclusion is not that the old note was wrong. It is that **layout is a Google-side experiment that
varies per profile *and over time*.** Both regexes must stay, both must stay pinned by
`GoogleProfileTotalParsingTests`, and neither may be described as "the layout for profile X".

This is precisely the failure mode a selector manifest with **ordered fallbacks and a recorded hit
index** is for: had the app been logging which pattern matched, the flip from bracketed to labelled on
DHA-2 would have been visible the day it happened.

---

## The Material-icon star trap — CONFIRMED live, exactly as documented

On `business.google.com/reviews`, 250 star glyphs were present:

- **distinct codepoints: 1** — every star is `U+E838`
- **distinct colours: 2** — `rgb(251, 188, 4)` (gold, filled) and `rgb(218, 220, 224)` (grey, empty)

**The rating is carried entirely in colour.** Reading codepoints reports five stars for every review,
which is how five unanswered one-star reviews sat in the queue labelled "Positive" for the life of the
feature. Any manifest entry for a star rating must specify **the computed colour**, not the character,
and must record the two colour values as data so a Google palette change is a manifest bump rather than
a silent mislabelling.

---

## Anchor inventory

Google's markup is machine-generated: `jsname` (773 elements), `jscontroller` (490), `jsaction`,
`data-ved`, `data-hveid`. Treat all of these as **FRAGILE** — they are build artifacts, not contracts.

The one genuinely **STABLE** anchor found:

| Anchor | What it identifies |
|---|---|
| `data-review-id` | A single review. Stable identity — the natural key for "which reviews need a reply", for the review-ask dedupe, and for a per-review deep link in Phase 3. |

Rating anchor (Search merchant view), **SEMI**: `[aria-label]` matching `Rated <N.N> out of 5,`. The
lifetime total must be anchored **off the rating**, because `innerText` renders them run together
(`"4.6239 Google reviews"` → a naive `([\d,]+)` yields `6239`), and the bracketed pattern must stay
anchored on the rating so it cannot match `(closes 9 PM)` in the opening hours above it.

---

## View inventory

| View | URL-addressable? | How reached | DOM anchors | Side effects of visiting | Oversight data yielded | State required | Notes / traps |
|---|---|---|---|---|---|---|---|
| **Reviews manager** | **Yes** — `business.google.com/reviews` | Direct navigation | `[data-review-id]` — **STABLE**. Stars: `U+E838` glyph, rating in **colour** | None customer-visible | Per-review rating, reply state, review identity | Logged in, profile selected | The default start URL. The bare `business.google.com` root redirects single-location managers into a raw Search page. |
| **Review pagination** | Within the manager | `navigate_before` / `navigate_next` buttons — **SEMI** (icon-ligature text) | — | None | The next page of reviews | Manager loaded | **Paged, not infinite scroll** — `hasLoadMore` false, `scrollHeight` 39. This is why walking every page over-counted by two to three times; the current 50-at-a-time read is the honest compromise and the UI says so. |
| **Search merchant view** | **Yes** — `google.com/search?q=…&stick=…` | business.google.com's own redirect | `[aria-label*="Rated"]` — **SEMI** | Navigates the WebView away from the manager | **Profile rating** and **lifetime review total** | Logged in | Throttled to `RatingRefreshInterval` (6h) — each scrape costs a visible round-trip. `ScrapeRatingAsync` must not run on the account currently on screen unless the user asked. |
| **Merchant-view actions** | — | — | Link/button text: `Add photos`, `Get more reviews`, `<N> Google reviews`, `Add a photo` — **SEMI** | Navigation only | The `<N> Google reviews` link text is a **second, independent** source for the lifetime total | Logged in | Worth adding as a fallback candidate in the manifest — it is a link label, not run-together `innerText`, so it does not need the anchoring workaround. |
| **Q&A** | **No — it is not there** | — | — | — | **None** | — | **CONFIRMED absent (A1).** See below. |
| **Reply composer** | — | Per review in the manager | Not mapped | Would post publicly | — | — | **Out of scope under D1.** The app never sends. Reply drafting stays on-device; publishing is the owner's action in Google's own UI. |
| **Empty / no-reviews / logged-out** | — | — | UNKNOWN | — | — | — | Not observed. Same requirement as every channel: a logged-out state must not read as "zero unanswered reviews". |

---

## There is no Q&A surface on either page the app uses — CONFIRMED negative (A1)

The channel is described throughout the product as "reviews **and Q&A**". Measured 2026-09-02:

**On `business.google.com/reviews`** the entire navigation is `Businesses · Reviews · Linked accounts ·
Settings · Support`, and the only in-app hrefs are `/reviews`, `/?tab=LL`, a sign-out link and a support
article. The word "question" does not appear anywhere in the page text.

**On the Search merchant view** (all three profiles) there is no Q&A affordance either: no matching
control, and no occurrence of "Questions and answers", "Ask a question", or "See all questions". Those
pages are ordinary search-results pages (`All / Images / Videos / Forums / News`) carrying a merchant
panel — not a management console with a Q&A tab.

**So Q&A is not reachable from either surface the app embeds.** It remains **UNKNOWN** whether a Google
Maps merchant surface exposes it — Q&A is a Maps/Search consumer feature, and Google moved
single-location management into Search and Maps — but nothing the app can reach today shows it.

### Consequence: one product string overstates what ships

`PlatformDefinition` gives `googlebusiness` the description
*"Reviews and Q&A — rating, unanswered reviews, reply rate. No message channel."*

`PlatformDescriptionTests` exists specifically to hold these strings to what the channel does **today**,
and this one names a surface that is not reachable and produces no metric. It is shown to a paying
customer in the Add-account picker.

**Not changed here** — A1 is docs-only, and the right fix is a judgement call between dropping "and Q&A"
and first checking the Maps surface. Flagged for the owner.

## What is still not obtainable

- **Reply time.** Not exposed by Google in any form. Stated on screen rather than papered over — keep it
  that way.
- **A complete review corpus.** 50 of ~1,671 loaded at a time. The coverage line ("covers the first 50
  of 994") is the honest surface, and it silently degrades to "covers 50 loaded reviews" whenever the
  lifetime total fails to parse — which is exactly what the two-layout problem above caused.
