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

### The `moduleCount` oddity — settled (A1), and it is worse than "always zero"

First reading gave `moduleCount: 0`; a second account gave `moduleCount: 32, moduleTotal: 16892` — both
with `strategy: "known-name"`. So it is not simply unset on the fast path. Traced through the source:

- `diag.moduleCount` is assigned in **exactly one place** — inside the fallback-strategy loop
  (`whatsapp-store-bridge.js:398`), after `discoverByKnownName()` has already failed.
- `discoverByKnownName()` sets `moduleTotal` but never `moduleCount`.
- `diag` is **never reset between discovery attempts**, and `discover()` returns early when
  `store.chat` is already set.

**Therefore `moduleCount` reports how many modules a *previous, failed* discovery attempt scanned.** With
`strategy: "known-name"` it means either "the fast path hit first time" (`0`) or "an earlier attempt
scanned 32 modules and failed, and a later known-name attempt then succeeded" (`32`). It is not a
property of the strategy that actually worked.

That makes it actively misleading on a health surface — a reader sees "0 modules" and infers failure when
it is the *best* outcome. **A4 must not render it raw.** Either reset `diag` per attempt, or label it
"modules scanned by the last fallback attempt", or drop it from the surface.

**Second defect, same function:** the probe's returned object literal contains `contact:
diag.contactCollection` **twice** (`:836` and `:837`). A duplicate key — the second silently wins. It is
harmless today because both write the same value, but a field the author intended to expose was lost to
it, and nothing flags a duplicate key in plain JS.

**Not a defect:** `__umStoreBridgeProbe` has no C# caller. That is correct and intended — it is the
DevTools entry point AGENTS.md documents. The Settings → Data health line is fed separately, by
`StoreBridgeHealth.Record` from the scan path.

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

### `last-msg-status` — settled (A1, 2026-09-02). Ack is earnable, and the naive read is 100% wrong.

`PlatformCapabilities.CanReadAck` is declared false with the reason "those are still only DOM tick
glyphs". Measured live, the anchor is stable and the capability is earnable — **but the state is carried
in the glyph's COLOUR, exactly like the Google star rating.**

Inside `[data-testid="last-msg-status"]` sits an inline `<svg>` with a `<title>` and a `<path>`:

| Reading | Delivered | Read |
|---|---|---|
| `svg > title` text | `wds-ic-read` | `wds-ic-read` |
| `getComputedStyle(path).fill` | `rgba(0, 0, 0, 0.6)` | `rgb(0, 123, 252)` |

**The title is identical for both states.** An implementation that reads the icon name — the obvious
one — reports "read" for every outbound message. That is the same failure that labelled five unanswered
one-star Google reviews "Positive" for the life of that feature, in a second place, on a different
client, found by looking rather than by assuming.

Two further traps in the same element:

- **It hosts non-ack icons.** One row carried `ic-keyboard-voice-filled` (a voice-note marker) under the
  same testid. Filter on `title === 'wds-ic-read'` before reading a fill.
- **`data-icon` is not on it.** `#pane-side [data-icon]` returned **zero** elements at the time of
  measurement, even though `data-icon` appears in the pane's overall attribute set (it is on chrome icons
  elsewhere, intermittently). Do not anchor the ack on `data-icon`.

**Not observed: the single-tick "sent / pending" state.** Only delivered and read were present.
**UNKNOWN** whether it is a third fill, a different title, or an absent element. Settle it with a message
in flight before flipping `CanReadAck`.

### `last-msg-status` presence is itself a direction signal

It appeared on **12 of 66** rows — only those whose last message is outbound. So its mere presence
answers "did we speak last?" without reading any text, which is the awaiting-reply question. Cheaper and
more robust than parsing the preview, and it works on the DOM path where the store bridge is unavailable.

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
| **Cold sync (post-launch)** | — | Any app start | Shell testids (`wa-web-main-screen`, `chatlist-header`, `chat-list-search-container`) are present **while `chat-list` and `cell-frame-container` are not** | None | **None — and that is the danger** | Logged in, just launched | **CONFIRMED (A1).** See below. |
| **Logged-out / phone-not-connected** | — | — | UNKNOWN | — | — | — | **The important gap.** `OversightEntityHealth` already separates `IsStale` from `ReadFailed`, and this is the state that must drive `ReadFailed`. Artifact that would settle it: a DOM dump of the disconnected client. Owner task B5. |

---

## The chat list is transient after a cold launch — CONFIRMED (A1)

Across one app start, `#pane-side [role="row"]` was measured at **64, then 0, then 66**, over roughly
three minutes — with `document.readyState === "complete"` throughout, and the shell's own testids
(`wa-web-main-screen`, `chatlist-header`, `chatlist-panel-archived-button`) present the whole time. One
probe listed `chat-list` and `cell-frame-container` among the rendered testids; the next, seconds later,
found zero of both.

So during a cold sync WhatsApp Web presents a **fully-loaded document with an empty chat list**, and it
is not distinguishable from a genuinely empty account by any of `readyState`, the shell anchors, or the
row count.

**A scan in that window sees zero conversations.** Zero must never be read as "no conversations" — this
is precisely the `IsStale` vs `ReadFailed` distinction `OversightEntityHealth` already draws and which
its own comment warns never to infer from a zero count. The manifest's health reporting (A4) needs a
positive readiness anchor before it trusts a count: the presence of `[data-testid="chat-list"]` **with at
least one `cell-frame-container`**, not `readyState` and not the shell.

This also explains why `InstanceSessionManager.WarmBackgroundSessionsAsync` matters more than it looks:
an account scanned too soon after warm reports nothing, truthfully and uselessly.

## Archived — confirmed reachable, not yet mapped

`[data-testid="chatlist-panel-archived-button"]` is present with `aria-label="Archived "` (trailing
space in the live client — do not trim-match it exactly). It renders `ic-archive` plus the word
"Archived". **STABLE.** The panel behind it was not opened this pass; the entry point is confirmed, the
contents are not.

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
