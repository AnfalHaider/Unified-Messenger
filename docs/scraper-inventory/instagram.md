# Instagram (`instagram.com`) — scraper inventory

## ⛔ BLOCKED — no live session

**As of 2026-09-02 there is no Instagram account configured in the app**, so nothing in this file could
be observed. Nothing below is guessed.

### What unblocks it

The owner adds an **Instagram** account and logs in themselves. Nothing is in the way: the channel is
registered (`PlatformDefinition.All`, id `instagram`, default URL `https://www.instagram.com/`) and
**is offered in the Add-account picker** — `GetSelectablePlatforms()` returns `PlatformDefinition.All`
unfiltered as of v4.99.74. (The old `HiddenFromPicker` gate AGENTS.md still describes no longer exists
in the tree; verified 2026-09-02.)

---

## The one experiment that matters here, and why it is now the priority

§4.3.4 — **is there a passive read path in the consumer client?** Do unread counts, thread lists and
previews appear in the DOM, an in-memory store, or a local database without opening a thread?

When the brief was written this was the fallback question, worth asking only in case a customer had no
Business Portfolio. **The Messenger inventory promoted it to the main event.**

`messenger.com` was found to carry Meta's *LightSpeed* store — a 358-table local relational database
reachable from page JavaScript via `require('LSDatabaseSingleton')`, with a `threads` table exposing
`unreadMessageCount`, `snippet`, `snippetSenderContactId`, `lastActivityTimestampMs`,
`lastReadWatermarkTimestampMs` and `folderName`, all readable with **no conversation opened**. Full
detail in [messenger.md](messenger.md).

Instagram DMs are the same product family, served by the same module system. **LIKELY** — not confirmed —
that `instagram.com` carries an equivalent store. If it does:

- Instagram goes from honestly counts-only to full oversight, passively, with no API, no approval and no
  ban risk.
- The main argument for routing Instagram through Meta Business Suite ("one DOM instead of two")
  largely disappears.
- `MetaAggregateOnly` needs the same re-scoping Messenger needs: a prohibition on **thread-view
  navigation**, not on per-conversation reads.

### The probe that settles it, verbatim

Run in the logged-in Instagram client, opening nothing:

```js
// 1. is the module system the same?
typeof require === 'function' && typeof require('__debug') === 'object'

// 2. is there a LightSpeed store?
const m = require('LSDatabaseSingleton');
const p = m.getLSDatabaseSingletonPromiseOrValue();
const db = (p && p.then) ? await p : p;
Object.keys(db.tables).length            // messenger.com: 358

// 3. what does the threads table carry?
const it = db.tables.threads.entries();  // NOTE: a manual iterator - .next() only.
const r = it.next();                     // Array.from / for..of both silently yield nothing.
Object.keys(Array.isArray(r.value) ? r.value[1] : r.value)
```

Also check `indexedDB.databases()` for an `*_web_v1_<uid>`-shaped database, and the aggregate unread
counter in `[aria-label]`, which on Messenger reads `"Chats · N unread"` and needs no store at all.

**Report column names and counts only. Never row content** — this is a real business's customer data.

---

## Known before this phase, and still true

- `instagram.com/direct/t/<thread_id>` exists as a URL shape. **UNKNOWN** whether it opens an existing
  thread in an authenticated session, and **UNKNOWN** what it costs — that is a thread-view navigation,
  so under the current prohibition it is a hand-off target only, never a scan path.
- Meta redesigns Instagram frequently. Whatever anchors this inventory eventually records will need the
  ordered-fallback treatment more than any other channel.

---

## To inventory when the session lands

Full §4.2 schema. At minimum: DM list · DM thread · requests folder · unread badge · profile · notifications ·
empty / loading / logged-out states · and the side effect of each, observed **from a second device**, not
reasoned about.
