# Messenger (`messenger.com`) — scraper inventory

**Observed:** 2026-09-02 · live logged-in business account, in the app's own WebView2 via CDP.
**Build:** `rsrc.php/v4iUw44/`, `rsrc.php/v4iesF4/` · LightSpeed schema **358 tables** · 13,642 JS modules.

---

## The headline: `RequiresThreadOpenToRead = true` is too strong for this channel

`PlatformDefinition.MetaAggregateOnly` declares Messenger aggregate-only on the grounds that
per-conversation detail "cannot be read without a user-visible side effect". Measured live, the premise
does not hold as stated.

**CONFIRMED — Messenger Web keeps a complete local relational database, readable from page JavaScript,
with no conversation opened.** It is Meta's *LightSpeed* store (`LSDatabaseSingleton`), backed by the
IndexedDB database `messenger_web_v1_<uid>`, and it exposes a `threads` table carrying every field the
oversight engine needs:

| LS `threads` column | What it gives the product |
|---|---|
| `threadKey` | Stable conversation identity (matches the `/t/<id>/` URL segment) |
| `unreadMessageCount` | **The awaiting-reply signal** |
| `snippet` | **Last-message preview text** |
| `snippetSenderContactId` | **Direction** — whether the last message was ours or theirs |
| `lastActivityTimestampMs` | **Last-activity timestamp** |
| `lastReadWatermarkTimestampMs` | When *we* last read it — a first-class FRT input WhatsApp does not expose |
| `folderName` | Inbox / requests / spam / archived separation |
| `threadType`, `memberCount` | 1:1 vs group |
| `muteExpireTimeMs`, `isHidden`, `threadTags` | Mute, hide and label state |
| `draftMessage` | An unsent draft (read-only; **never write here** — D1) |

and a `messages` table with `threadKey`, `timestampMs`, `messageId`, `senderId`, `text`, `sendStatus`,
`isUnsent`, `primarySortKey`.

That is everything the WhatsApp store bridge yields, **plus** a read watermark. It is the same
architectural pattern the WhatsApp bridge already proved: read the client's own already-decrypted local
store instead of its DOM.

### What is and is not proven

| Claim | Label | Basis |
|---|---|---|
| The data above is readable with **no conversation opened** | **CONFIRMED** | Read live via `require('LSDatabaseSingleton')`; 40 `threads` rows enumerated without touching a thread. |
| Reading the LS store fires **no** read receipt | **LIKELY** | It is a local IndexedDB-backed table read with no network write by construction. Meta's own *write* path is a separate stored procedure (`LSOptimisticMarkThreadReadV2StoredProcedure`) triggered by the thread **view**, not by a table read. Not yet observed from a second device. |
| **Opening** a thread view fires a read receipt | **UNKNOWN** | Never verified by anyone in this repo. `LSReportThreadViewOpenStoredProcedure` and `LSOptimisticMarkThreadReadV2StoredProcedure` exist in the module map, which is strong circumstantial support — but a module existing is not a receipt firing. §4.3.2 experiment; needs a second device. |

**The distinction that matters:** the prohibition was written to stop an adapter opening threads. That
remains correct and should stay. What is now measurably wrong is the *inference drawn from it* — that
per-conversation detail is therefore unreachable. Detail does not require the thread view. The
prohibition should be re-scoped from "no per-conversation reads" to "no thread-view navigation".

### Sync completeness caveat

Of 40 `threads` rows read, `snippet`, `unreadMessageCount` and `memberCount` were populated on **19**.
The remainder are partial rows (other folders, or not yet synced). This mirrors the documented WhatsApp
behaviour where `chat.msgs` fills in lazily — expect coverage to rise the longer the session is warm.
Any adapter must treat a null `snippet` as "not synced yet", **never** as "no messages".

---

## Loading `messenger.com` opens a conversation by itself

**CONFIRMED.** The account's configured start URL is `https://www.messenger.com/`. The live tab was at
`https://www.messenger.com/e2ee/t/<id>/` with `performance.getEntriesByType('navigation')[0].redirectCount === 1`.

Messenger redirects the root into the most recently active thread. So **every app start, every WebView
refresh and every session warm lands inside a real customer conversation** — today, in shipped code, on
a channel whose capabilities declare that opening a thread is disqualifying.

If experiment §4.3.2 confirms the receipt fires, this is a live defect: the app tells customers it
looked, on every launch, with no user intent. The fix is cheap — point the Messenger start URL at a
non-thread surface, or suppress the redirect — but it should not be applied before the experiment,
because the right fix differs depending on the answer.

---

## View inventory

| View | URL-addressable? | How reached | DOM anchors | Side effects of visiting | Oversight data yielded | State required | Notes / traps |
|---|---|---|---|---|---|---|---|
| **Chat list** (left rail) | No — a persistent pane, not a route | Always present | `[role="grid"][aria-label="Chats"]` then `[role="row"]` — **STABLE** (role + label). Rows carry **no** `aria-label`, **no** `data-testid`; only `data-visualcompletion`. Row classes **FRAGILE-hashed**. | None observed. Rendering the list opens nothing. | Per-row thread link, name, preview, relative time, bold-weight unread hint | Logged in | 14 rows rendered at the app's WebView width. Virtualised — the rest are not in the DOM. Prefer the LS store over this DOM entirely. |
| **Chat-list row** | Row links to `/e2ee/t/<ID>/` | — | `a[href^="/e2ee/t/"]` — **STABLE** shape. Name and preview in `span`s; a screen-reader summary span renders `"<name>: <preview> <date>."`. Timestamp in `abbr`/`time` — **STABLE**. Unread inferred from `font-weight >= 600` — **FRAGILE** (computed style, no semantic marker). | None | name, preview, timestamp, unread-ish | List rendered | The DOM's unread signal is **weight only** — there is no unread attribute on the row. That alone is why the LS store is the right path, not a nicety. |
| **Aggregate unread counters** | No | Always present | `[aria-label]` matching `"Chats · N unread"` and `"Requests · N unread"` — **SEMI** (localised copy) | None | Inbox and Requests unread totals, with no thread touched | Logged in | The cheapest honest Meta metric available today. Survives even if the LS route is rejected. |
| **Conversation** | Yes — `/e2ee/t/<ID>/`, and `/t/<ID>/?focus_target=1` | Click row, or navigate URL | Message rows also use `[role="row"]`, so **always scope list queries to `[aria-label="Chats"]`** | **Marks the thread read. UNKNOWN whether the receipt reaches the customer; LIKELY yes.** Not opened during this inventory. | Full transcript — but see the prohibition | Logged in | Under D1 this view is a **hand-off target only**: focus it when the user clicks reply, never on a background scan. |
| **Requests folder** | UNKNOWN | Inbox switcher | `[aria-label="Requests · N unread"]` — **SEMI** | UNKNOWN | Pending-request count | Logged in | Not opened. `folderName` in the LS `threads` table likely separates these without navigating — settle it from the store, not the UI. |
| **Spam / Archived** | UNKNOWN | Inbox switcher | `[aria-label="Inbox switcher"]` — **SEMI** | UNKNOWN | — | Logged in | Same: prefer `folderName`. |
| **Empty / loading / logged-out** | — | — | UNKNOWN | — | — | — | Not observed. A logged-out state must be distinguishable from "zero unread", or the app reports a quiet day during a session expiry. Artifact that would settle it: one DOM dump of the logged-out client. |

---

## Local storage surfaces

IndexedDB databases present (names only):

`messenger_web_v1_<uid>` (the LS store — **the read path**) · `messenger_web_fts_v1_<uid>` (full-text
search index) · `messenger_web_ebdb_v1_<uid>` · `messenger_web_eb_minos_db_v11_<uid>` ·
`messenger_web_metadata_v1_<uid>` · `messenger_web_signal_v1/v3_<uid>` · `mw_notifications_db` ·
`messenger_pwa_db` · `cache_service_<uid>_v3` · plus queue and quota bookkeeping.

Module system: `require` and `__d` present; `require('__debug')` resolves — the **same** discovery
pattern `whatsapp-store-bridge.js` already uses. A Messenger bridge is a port of an existing, proven
component, not a new one.

**Trap (inherited from the WhatsApp bridge, applies verbatim):** pull modules through `require(name)`.
`require('__debug').modulesMap` values are descriptors whose `exports` stay empty until materialised.

**Trap (new):** `db.tables.threads.entries()` returns a **manual iterator** — an object with `.next()`
only. It is not iterable, not async-iterable and not thenable, so `Array.from` and `for…of` both yield
nothing and look exactly like an empty database.

---

## Consequences for the product

1. **Messenger is not honestly counts-only.** `CanReadUnread`, `CanReadPreview`, `CanReadTimestamps`,
   `CanReadContactIdentity` and `SupportsFrt` are all reachable through a store bridge. None may be
   flipped before the adapter that backs each read actually ships — that rule stands unchanged.
2. **`RequiresThreadOpenToRead` should be re-scoped, not cleared.** Rewrite it as a prohibition on
   thread-view navigation, which is what it was always protecting.
3. **The start-URL redirect needs a decision** once §4.3.2 lands.
4. **This weakens the case for Meta Business Suite as a read path — it does not remove it.** Business
   Suite may still be the better source for Instagram, and its Insights response-rate figures (§4.3.3)
   have no equivalent here. Inventory it before deciding.
