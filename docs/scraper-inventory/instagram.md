# Instagram (`instagram.com`) — scraper inventory

**Observed:** 2026-09-04 · two live logged-in business accounts, in the app's own WebView2 via CDP.
**Build:** ~9,150 JS modules · LightSpeed schema **358 tables** (identical count to `messenger.com`).

Unblocked when the owner signed in. This file previously read ⛔ BLOCKED.

> ⚠️ **This file said "Instagram is honestly counts-only" for one afternoon. That was wrong**, and the
> correction is the most useful thing in it. The first pass measured the LightSpeed store, found it
> empty on the feed, and stopped — concluding that only the tab-title badge was readable. The store is
> indeed empty; the conclusion did not follow, because **Instagram does not use LightSpeed on the web
> feed for this. It uses Relay**, and the Relay store is already populated with the DM inbox before
> anything navigates anywhere. Per-thread *who* and *how long* are readable passively. Only the message
> preview text is not.
>
> The owner caught it by pointing at a screenshot of their own inbox and asking why the app could not
> read what was plainly on screen. Measuring one store and generalising to "the page holds nothing" is
> the mistake not to repeat on Messenger and Business Suite.

---

## Where the data actually is: the Relay store, on the feed, unprompted

**CONFIRMED.** `require('PolarisRelayEnvironment')` → `.getStore().getSource()` on
`https://www.instagram.com/` — the app's start URL, with no navigation — holds ~576 records including:

| Type | Count | What it is |
|---|---|---|
| `XFBIGDirectViewerThread` | **15** | One DM thread each. The payload below. |
| `SlideThread` | 15 | Thin wrapper; `as_ig_direct_thread` points at the above. |
| `SlideMailboxThreadsByFolderEdge` | 60 | Edges. See the folder correction below — not 60 distinct threads. |
| `SlideMailboxThreadsByFolderConnection` | 3 | ⚠️ **Three Relay aliases of ONE folder, not three folders.** See below. |
| `XFBSlideReadReceipt` | 24–27 | Per-participant read watermarks. |
| `XDTNotificationBadgeCount` | 1 | **Activity breakdown — comments / likes / relationships.** See its own section. |

### Folder correction — measured 2026-09-04, second pass

An earlier draft of this file read the three `…Connection` records as *"the three folders, matching the
client's Primary / General / Requests tabs"*. **That was inferred from the record count and it is wrong.**
Dereferencing the connection keys shows all three carry the same argument:

```
threads_by_folder(first:15,folder:"INBOX")
__IGDThreadListOffMsys_SlideMailbox__threads_by_folder_connection(folder:"INBOX")
__IGDChatTabsPOGsOffMsys__threads_by_folder_connection(folder:"INBOX")
```

Three Relay aliases over **one** folder — `INBOX`, i.e. Primary. So the 60 edges are the same 15 threads
counted four times over, and **General and Requests are not fetched on the feed at all**. The client's
"Requests (2)" tab is not visible passively. Any figure the product prints must say Primary only.

Both connections also report `has_next_page: true` with an `end_cursor`, so the backlog beyond 15 exists
and is paginable — but following that cursor means issuing our own query, which is a different act from
reading one the client already made. Not proposed here.

Instagram prefetches the mailbox on the feed to draw the Messages badge. That prefetch is the whole
opportunity: it is the client's own request, already made, for its own reasons.

### `XFBIGDirectViewerThread` — the fields

| Field | Type | Use |
|---|---|---|
| `thread_title` | string | **The customer's display name.** Present 15/15 on both accounts. |
| `last_activity_timestamp_ms` | string (ms epoch) | **Exact age of the last activity.** Present 15/15. Finer than the client's own rounded "58m". |
| `$r:client__is_unread` | Relay resolver ref | **The unread flag.** Deref → `__resolverValue`. See below. |
| `users` → `XDTUserDict` | ref | `username` (the `@handle`), `profile_pic_url`, `is_verified`. |
| `thread_key`, `id` | string | Stable per-thread identity — a real key, unlike WhatsApp's positional rows. |
| `thread_subtype` | string | `IG_BUSINESS_ACCOUNT_ONE_TO_ONE` on all 30 threads observed — genuine one-to-one customer conversations, not groups. |
| `slide_read_receipts` | refs | The **other** participant's `watermark_timestamp_ms`. Not ours — see the trap below. |
| `marked_as_unread` | boolean | ⚠️ **Not the unread state.** See the trap below. |
| `thread_image_url` | null | Null on all observed; the avatar is on `users[].profile_pic_url`. |

### Two traps, both measured

**`marked_as_unread` is the manual "Mark as unread" flag, not the unread state.** It read `false` on all
15 threads of an account whose badge said 6. A scraper reading it would report every account permanently
caught up. The real signal is the Relay resolver `$r:client__is_unread` → `__resolverValue`.

**`slide_read_receipts` does not contain the viewer's own watermark.** The first attempt derived unread
arithmetically — `last_activity > my_watermark` — and got `null` on 15/15, because the receipts are the
*other* party's. Deriving unread from receipts is not available; read the resolver.

### The verification

Resolver-derived unread count vs. the client's own badge, both accounts, same instant:

| Account | Threads in store | Resolver says unread | Tab-title badge | Agree |
|---|---|---|---|---|
| A | 15 | **1** | `(1)` | ✅ |
| B | 15 | **6** | `(6)` | ✅ |

Exact on both. The per-thread flags also order correctly: on account B the six unread were the six most
recent by `last_activity_timestamp_ms` (30, 48, 103, 107, 128 and 163 minutes old), matching the client's
own top-of-list ordering.

---

## The second surface: the activity badge, broken down

**CONFIRMED, both accounts.** `XDTNotificationBadgeCount` — fetched on the feed by
`xdt_notification_badge(device_id:…)` — carries a **structured breakdown**, not just a total:

| Field | Account A | Account B | What it is |
|---|---|---|---|
| `total_count` | 12 | 23 | Sum of the below. |
| `activity_badge_counts.comments` | **4** | **5** | **Comments on the business's own posts.** |
| `activity_badge_counts.likes` | 5 | 9 | Likes. Vanity, not oversight. |
| `activity_badge_counts.relationships` | 1 | 8 | Follows / follow requests. |
| `activity_badge_counts.usertags` | null | null | Not populated on either. |

**`comments` is the find.** For a business, an unanswered comment under a post is a customer waiting in
public — arguably more urgent than a DM, because everyone can see it go unanswered. Nothing in the
product has ever surfaced it, and it costs one field read on a page already loaded.

Two honest limits, both of which must be printed next to the number:

- It is a **count only**. Who commented, on which post, and what they said are not in this record — the
  feed does not fetch them. This is the one place `PlatformCapabilities.IsAggregateOnly` genuinely fits.
- It is **unseen-activity, not unanswered**. It clears when the owner opens the notifications panel,
  whether or not they replied. So it under-reports after a glance, and it must never be phrased as
  "4 comments need a reply" — "4 new comments" is what the data supports.

Also present and worth noting for later: `XDTUserDict` carries `full_name`, `is_verified` and
`friendship_status`; `XDTRelationshipInfoDict` carries `incoming_request` / `outgoing_request`. And the
mailbox arrives via `get_slide_mailbox_for_iris_subscription` — Meta's realtime sync — which suggests the
thread list stays current without the app polling for it. Worth confirming before relying on it.

---

## What is NOT there, and what it costs

**CONFIRMED — there is no message preview text anywhere in the feed's Relay store.** A sweep of every
record for any field matching `snippet|last_permanent|preview_text|summary` returns **empty**. The client
renders "Raja sent a video." on `/direct/inbox/`, from data that route fetches; the feed's prefetch
carries thread metadata only.

So Instagram's passive ceiling is **name · handle · age · unread**, per thread — WhatsApp parity minus the
preview. Two further limits, both to be stated on screen rather than papered over:

1. **Top 15 threads of Primary only.** The connection is fetched `first:15, folder:"INBOX"`, and
   `has_next_page` is `true` — so the app can see that more exist but not what they are. General and
   Requests are not fetched at all. (WhatsApp, by contrast, renders ~60 rows across everything.) The
   figure must therefore be labelled *Primary, top 15*, never presented as the account's whole backlog.
2. **Unread is a lower bound on "awaiting reply", not a synonym.** Unread means the owner has not opened
   it. A thread they opened and did not answer is still awaiting, and reads as read. WhatsApp computes
   awaiting from who sent last; Instagram's resolver cannot. So the honest phrasing is **"at least N
   waiting"**, and the drill-down must say what it cannot see.

---

## View inventory

| View | URL-addressable? | Side effects | Oversight data | Notes |
|---|---|---|---|---|
| **Feed (start URL)** | Yes — `instagram.com/` | None observed | **Per-thread name, handle, age, unread — Primary, top 15 · plus new-comment / like / follow counts** | The only view the app loads, and it carries both surfaces. |
| **Direct inbox** | Yes — `/direct/inbox/` | UNKNOWN — B2a | Would add **preview text** and threads beyond 15 | Not visited. Now a *preview-only* question, no longer the gate on the channel. |
| **Direct thread** | Yes — `/direct/t/<id>/` | **Marks read; LIKELY fires a receipt** | Full transcript | Hand-off target only, never a scan path. |
| **Logged out** | — | — | — | `input[name="username"]` / `[name="password"]`. Reliable; see A12. |

---

## Consequences for the product

1. **Instagram is a measured channel, not an aggregate one — and it has two surfaces, not one.** DMs feed
   the needs-a-reply queue with real rows (who, which account, how long): `CanReadUnread` and
   `CanReadTimestamps` go true, `CanReadPreview` stays false. The **activity badge** is the separate,
   genuinely aggregate surface — new comments, likes and follows as counts — and it is the first real
   consumer `PlatformCapabilities.IsAggregateOnly` has ever had. Neither surface should be folded into
   the other: one is a queue of named people, the other is a number that clears on a glance.
2. **`SelectorManifest` does not fit this channel, and should not be forced to.** There are no DOM
   selectors here — the read is `PolarisRelayEnvironment` → record types → field names. The ordered-
   candidate idea still applies (the environment module name and the resolver key are both fallback-
   worthy), but as a *store* manifest: same failure reporting, different anchor kind.
3. **The sign-in gate matters more here than anywhere else.** A logged-out tab has no Relay mailbox at
   all, so an ungated scraper reads zero threads and reports a quiet account — the exact false-calm
   `AccountReadHealth` exists to prevent. A12 first.
4. **B2a shrinks.** It was the gate on Instagram being anything at all; it is now only the question of
   whether preview text and a deeper backlog are worth one navigation to a list view.
