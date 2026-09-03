# Scraper inventory

Per-channel maps of every page, view and state of each channel's web client: what it is, how it is
reached, which DOM anchors identify it, **what visiting it does to the customer's experience**, and what
oversight data it can honestly yield.

This is Phase 1 of the scraper-foundation work. It gates the selector manifest (Phase 2), navigation
mapping (Phase 3) and the unified inbox (Phase 4).

**Sequencing for all four phases: [../scraper-foundation-roadmap.md](../scraper-foundation-roadmap.md).**

## Files

| File | Channel | State |
|---|---|---|
| [messenger.md](messenger.md) | Facebook Messenger (`messenger.com`) | ✅ Inventoried — **and it overturns a core assumption** |
| [whatsapp.md](whatsapp.md) | WhatsApp / WhatsApp Business (`web.whatsapp.com`) | ✅ Inventoried |
| [google-business.md](google-business.md) | Google Business (reviews only — **Q&A is not reachable**) | ✅ Inventoried |
| [meta-business-suite.md](meta-business-suite.md) | Meta Business Suite (`business.facebook.com`) | ⛔ BLOCKED — no live session |
| [instagram.md](instagram.md) | Instagram (`instagram.com`) | ⛔ BLOCKED — no live session |
| [experiment-read-receipts.md](experiment-read-receipts.md) | §4.3.2 protocol — needs a second device | ☐ NOT RUN |

Telegram, Discord and Custom URL are **out of scope**: nobody here can log into them, so any DOM map
would be invention. They ship as embed-only tabs stating "No oversight metrics" — a shipped product
decision, unrelated to this work. If a login appears, each becomes its own increment and the schema
below applies unchanged.

## Method

Every row was produced by reading a **live, logged-in client** through the app's own WebView2 — not
desktop Chrome, not memory, not a third-party blog, not an open-source clone's source.

```
# launched from OUTSIDE the MSIX container (an agent shell redirects %LOCALAPPDATA% writes)
set WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333
start "" "%LOCALAPPDATA%\Programs\UnifiedMessenger\UnifiedMessenger.exe"
# then drive CDP Runtime.evaluate over the socket at http://127.0.0.1:9333/json/list
```

Kill the app first — the single-instance mutex silently swallows the launch and leaves a port-less
instance. Relaunch without the variable when done.

Two passes so far, both on 2026-09-02: the **first** established the three channels; **A1** (increment
108) closed the gaps it left. A1 corrected two claims the first pass got wrong — see *Corrections* below.

### Corrections made by a later pass

Kept visible rather than edited away, because a reader who acted on the first version needs to know.

| First pass said | A1 found | Where |
|---|---|---|
| Messenger LS columns were reported as populated on N rows | Those columns are **`[hi, lo]` arrays**, and an array is always truthy — the population counts measured *column existence*, not value presence | [messenger.md](messenger.md) |
| `unreadMessageCount` presented as the confirmed awaiting signal | Readable and correctly typed, but **never observed non-zero** — LIKELY, not CONFIRMED | [messenger.md](messenger.md) |
| `moduleCount: 0` looked like it might be a miscount | It is **stale state from a previous failed discovery**, never reset — misleading rather than wrong | [whatsapp.md](whatsapp.md) |

### Read scope actually exercised

**List views only. No conversation was opened on any channel.** Every probe read structure that was
already rendered, or a local database that was already populated. The one navigation performed was one
Google Business tab from the Search merchant view back to `business.google.com/reviews` — the same
navigation the app's own reviews scrape makes.

### Privacy

The data on this machine is a real business's customer conversations. Probes returned **shapes, not
content**: attribute names, element counts, column names, type fingerprints, and text with every word
collapsed to `W` and every number to `N`. No message body, customer name or phone number appears in any
file in this directory, in any commit, or in `app.log`.

## Evidence labels

Every claim carries one:

- **CONFIRMED** — observed directly in a live client on the date given.
- **LIKELY** — documented or strongly implied, not tested here.
- **UNKNOWN** — not established. The row names the artifact that would settle it.

Two traps this repo has already paid for, restated because they apply directly to this phase:

1. **"The selector exists" is not "the selector is stable."** A hashed class matching today says nothing
   about next month. Every anchor below is ranked.
2. **"A view renders" is not "a view is side-effect-free."** The read-receipt problem is invisible from
   inside the browser. Anything in the ⚠ column marked CONFIRMED was confirmed from outside it, or is
   marked UNKNOWN and names the second-device test that would settle it.

## Anchor stability ranking

Used in every DOM anchors column:

| Rank | Meaning |
|---|---|
| **STABLE** | `data-testid`, a stable `id`, or an ARIA `role` the client's own accessibility depends on. |
| **SEMI** | `aria-label` text (survives redesign, breaks on localisation or copy change), or a URL shape. |
| **POSITIONAL** | Structural position, or an index-bearing testid such as `list-item-3`. Breaks on reorder. |
| **FRAGILE** | Hashed/minified class names. Record it, but never depend on it without a fallback. |

## Client builds observed

| Channel | Build identifier | Date |
|---|---|---|
| Messenger | `rsrc.php/v4iUw44/`, `rsrc.php/v4iesF4/`; LS schema 358 tables | 2026-09-02 |
| WhatsApp Web | module total 18009; store-bridge strategy `known-name` | 2026-09-02 |
| Google Business | reviews manager, star glyph `U+E838` | 2026-09-02 |
| WebView2 runtime | Edge 152.0.4191.53 / Chrome 152 | 2026-09-02 |

Record the build on every future re-inventory. An inventory with no version is undatable and therefore
untrustworthy within six months.
