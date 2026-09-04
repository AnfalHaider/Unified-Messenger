# Instagram (`instagram.com`) — scraper inventory

**Observed:** 2026-09-04 · two live logged-in business accounts, in the app's own WebView2 via CDP.
**Build:** 9,621 JS modules · LightSpeed schema **358 tables** (identical count to `messenger.com`).

Unblocked when the owner signed in. This file previously read ⛔ BLOCKED.

---

## The headline: the store is there, and it is empty

**CONFIRMED — Instagram carries the same LightSpeed store as Messenger.** `require('LSDatabaseSingleton')`
resolves, and it exposes the same 358 tables including `threads`, `messages`, `contacts` and
`participants`. The §4.3.4 prediction in the previous version of this file was right.

**CONFIRMED — and on the feed page it holds nothing.** Measured on a signed-in account with six unread
DMs:

| Table | Rows |
|---|---|
| `threads` | **0** |
| `messages` | **0** |
| `contacts` | **0** |
| `participants` | **0** |
| `_user_info` | 1 |

That single difference is the whole story of this channel, and it is a difference of *product*, not of
technology. `messenger.com` **is** an inbox, so its store is populated the moment the page loads.
`instagram.com` is a feed; Direct is a separate route, and the store fills only when that route is
opened. So the passive read that makes Messenger rich yields nothing at all here.

### Corroboration

The architecture is Meta's own, and documented. Project LightSpeed rebuilt Messenger around **MSYS**, a C
library wrapping SQLite with stored procedures, modelling the app "relationally across hundreds of
tables" with core tables **threads, messages, attachments and contacts** — which is precisely the schema
observed live in both clients. It is a deliberate cross-app messaging platform shared between Messenger
and Instagram, not an implementation accident, which is the strongest available reason to expect it to
stay put.

---

## What CAN be read passively: the unread count, from the tab title

**CONFIRMED.** `document.title` is `"(6) Instagram"` on an account with six unread threads. A count, on
the feed, with no navigation, no thread opened and no receipt.

That is the entire honest yield for Instagram today, and it is exactly the shape
`PlatformCapabilities.IsAggregateOnly` was written for: *"callers should render a count and explicitly
say detail is unavailable, rather than showing an empty list."* Instagram is the **first platform that
would actually reach that branch** — built in A8, tested, and until now reachable by no shipped channel.

Corroborating module names present in the bundle, none of them yet read:
`useIGDSystemFolderUnreadThreadCountQuery` · `XFBIGDirectViewerThreadIsUnreadResolver` ·
`useIGDChatTabsGetUnreadThreads.react` · `MWPIsThreadUnread`.

---

## The open question, and why it was not answered

Per-thread detail needs `instagram.com/direct/inbox/` opened so the store populates. **Not attempted**,
deliberately: `messenger.com` was measured redirecting its own root *into a conversation*, and until the
read-receipt experiment ([experiment-read-receipts.md](experiment-read-receipts.md)) says what opening a
Meta thread costs, navigating a Direct route on the owner's real business account risks firing a "Seen"
that cannot be withdrawn.

**The precise question for that session:** does `/direct/inbox/` show the thread list *without* opening a
conversation — and does landing there populate `threads` with unread counts, snippets and timestamps?
If yes, Instagram becomes as rich as Messenger for the price of one navigation to a list view. If it
auto-opens a thread the way `messenger.com` does, Instagram stays counts-only on the passive route.

---

## View inventory

| View | URL-addressable? | DOM anchors | Side effects | Oversight data | Notes |
|---|---|---|---|---|---|
| **Feed (start URL)** | Yes — `instagram.com/` | — | None observed | **Unread thread count, from `document.title`** | The only view the app loads today, and the only passive yield. |
| **Direct inbox** | Yes — `/direct/inbox/` | UNKNOWN | **UNKNOWN — the blocking question above** | Would populate the LightSpeed `threads` table | Not visited. |
| **Direct thread** | Yes — `/direct/t/<id>/` | UNKNOWN | **Marks read; LIKELY fires a receipt** | Full transcript | Hand-off target only, never a scan path. |
| **Logged out** | — | `input[name="username"]`, `input[name="password"]` | — | — | Present and reliable — see the handshake section below. |

---

## Consequences for the product

1. **Instagram is honestly counts-only**, and that is a finding, not a shortfall. It gives
   `IsAggregateOnly` its first real consumer.
2. **`CanReadUnread` may be flipped for Instagram once an adapter reads the title badge** — and nothing
   else. `CanReadPreview`, `CanReadTimestamps` and `SupportsFrt` stay false until the Direct-inbox
   question is settled.
3. **The sign-in gate matters more here than anywhere else.** A logged-out Instagram tab has no title
   badge, so a scraper that does not check sign-in state would read "0 unread" and report a quiet
   account. See `connection-handshake.js` — its Instagram profile did not exist until this pass.
