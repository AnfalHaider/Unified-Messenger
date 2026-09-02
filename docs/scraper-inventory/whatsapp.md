# WhatsApp / WhatsApp Business (`web.whatsapp.com`) — scraper inventory

**Observed:** 2026-09-02 · live logged-in account (one of three), app WebView2 via CDP.
**Build:** 18,009 JS modules · store bridge reports `strategy: "known-name"`, `chat: true`, `contact: true`.

This is the one channel with a shipped, working scraper. The inventory below is therefore aimed at two
things the existing code does not give us: **which anchors are stable enough to move into a manifest**,
and **which documented facts have gone stale**.

---

## Store-bridge health, measured live

`window.__umStoreBridgeProbe()` returned:

```
{"ready":true,"strategy":"known-name","moduleCount":0,"moduleTotal":18009,"chat":true,"contact":true}
```

**CONFIRMED** — the good path (`whatsapp-store-bridge.js`) is live and resolving both collections. The
app is *not* silently running on the IndexedDB fallback on this machine today.

One oddity worth a second look when the manifest lands: `moduleCount` is `0` while `moduleTotal` is
18,009 and both collections resolved. Either the field means something other than "modules matched", or
it is miscounted. It is the field a health surface would naturally show, so it should not be ambiguous.
**UNKNOWN** — settle it by reading the probe's own implementation.

Injected surface confirmed present: `__umFocusConversation`, `__umStartPreviewHarvest`,
`__umStoreBridgeProbe`, `__umTruncate`. `__umHarvestedPreviews` is `undefined` until a harvest runs —
expected, since harvest is manual-resync only.

---

## Stale documentation found

**AGENTS.md says: "Focus by sidebar `data-id` JID not title text."** Measured live:

```
document.querySelectorAll('[data-id]').length === 0
```

**CONFIRMED — there is not a single `data-id` attribute anywhere in the current WhatsApp Web document.**
The full attribute set present under `#pane-side` is: `alt`, `aria-colindex`, `aria-disabled`,
`aria-hidden`, `aria-label`, `aria-rowcount`, `aria-selected`, `class`, `data-icon`, `data-tab`,
`data-testid`, `dir`, `draggable`, `id`, `role`, `src`, `style`, `tabindex`, `title`, `type`, plus SVG
geometry attributes.

**The shipped code is not broken by this** — `whatsapp-adapter.js:1102` already carries the comment
"Verified live — rows carry no data-id", and `__umFocusConversation` matches on `span[title]` text with
the `data-id` reads kept only as a fallback. It is the AGENTS.md gotcha line that is stale and
misleading, and it should be corrected: a reader following it would design a selector that can never
match.

---

## Anchor inventory — WhatsApp is the *best*-anchored client of the three

Unlike Messenger, WhatsApp Web ships a rich `data-testid` vocabulary. These are **STABLE** anchors and
should form the backbone of the manifest:

| `data-testid` | What it identifies |
|---|---|
| `chat-list` | The conversation list container |
| `cell-frame-container` | One conversation row |
| `cell-frame-title` | Row title (contact name, or the raw number for unsaved contacts) |
| `cell-frame-primary-detail` | Row last-message preview |
| `cell-frame-secondary` | Row timestamp / meta line |
| `last-msg-status` | **Outbound ack tick** — the delivery/read glyph |
| `chat-msg-symbol` | Message-type symbol on the row |
| `chatlist-panel-archived-button` | Archived entry point |
| `chat-list-search-container` | Search box container |
| `filter-button`, `chat_list_filter_button_pill_label_item_<N>` | The list filter pills |
| `chatlist-header`, `chat-butterbar`, `wa-web-main-screen` | Shell / banner surfaces |
| `list-item-<N>` | One row, **POSITIONAL** — index-bearing, breaks on reorder. Never key on it. |

Stable ids: `#pane-side` (list pane), `#app` (shell), `#main` (**present only while a chat is open** —
this is the readback anchor `ConversationFocusHelper` already relies on).

Row classes are **FRAGILE-hashed** throughout. Never depend on one.

Unread signal: `[aria-label]` reading `"N unread message(s)"` — **SEMI** (localised, but semantic and
far better than Messenger's font-weight-only signal).

### `last-msg-status` is worth a look

`PlatformCapabilities.CanReadAck` is declared false with the reason "those are still only DOM tick
glyphs". That is accurate, but `data-testid="last-msg-status"` is a **stable** anchor for exactly that
glyph. Ack may be cheaper to earn than the comment implies. **UNKNOWN** — settle it by reading what the
element actually carries per state (delivered / read / pending), which needs a row in each state.

---

## View inventory

| View | URL-addressable? | How reached | DOM anchors | Side effects of visiting | Oversight data yielded | State required | Notes / traps |
|---|---|---|---|---|---|---|---|
| **Chat list** | **No** — the URL never changes per conversation | Default view | `#pane-side` + `[data-testid="chat-list"]`, rows `[role="row"]` — **STABLE** | None | Everything, via the store bridge; the DOM is the fallback | Logged in, phone paired | 64 rows rendered. Virtualised beyond that — the documented ~60-row harvest ceiling. |
| **Conversation** | **No** | Click row, or `__umFocusConversation` | `#main`, and a `[contenteditable="true"][role="textbox"]` composer that **exists only while a chat is open** | Marks read locally; sends a read receipt if the account has them enabled | Full transcript | Chat open | The composer's existence is the independent readback that separates "my selector is stale" from "the click did nothing". Keep this pattern for every channel. |
| **Archived** | No | `[data-testid="chatlist-panel-archived-button"]` — **STABLE** | Not opened | UNKNOWN | Archived thread count | Logged in | Not exercised in this pass. |
| **Search results** | No | `[data-testid="chat-list-search-container"]` — **STABLE** | Typing a query | None to the customer | Used by the focus helper's fallback path | Logged in | The focus helper searches by **phone digits, never a bare `@lid`** — an `@lid` matched unrelated message text and opened the wrong chat. Preserve that rule in the manifest. |
| **List filters** | No | `[data-testid="filter-button"]` pills — **STABLE** | Filtering | Unread / groups subsets without opening anything | Logged in | Pill testids carry an index (`..._item_9/10/11`) — **POSITIONAL**, read the label, not the number. |
| **Status / Channels / Communities / Calls** | No | `[aria-label]` on the primary navbar — **SEMI** | Not opened | None for this product | Logged in | Inventory only — no oversight value. Do not map. |
| **Logged-out / phone-not-connected** | — | — | UNKNOWN | — | — | — | **The important gap.** `OversightEntityHealth` already separates `IsStale` from `ReadFailed`, and this is the state that must drive `ReadFailed`. Artifact that would settle it: a DOM dump of the disconnected client. |

---

## `wa.me` / `send?phone=` — experiment §4.3.5

**NOT RUN.** The agreed read scope for this pass was list views only, and `?text=` is documented to
prefill a draft — a side effect a passive tool must not cause, and one that is only safely tested
against a number the owner controls.

What needs answering, unchanged: does `web.whatsapp.com/send?phone=<digits>` (no `text=`) open an
**existing** conversation without creating a draft, marking anything read, or adding the number to
recents? If clean, it replaces the ~120-line row-clicker with a URL for the most-used navigation in the
product. If not, DOM-driven navigation is the only option and Phase 3 must say so plainly rather than
wish it away.

Method when run: a number the owner controls, watched from the second device, with the LS-equivalent
before/after state captured.
