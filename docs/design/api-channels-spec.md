# API-backed channels — what the app becomes

**As of:** 2026-08-29 · **Baseline:** v4.99.72 · **Phase 2 of 3.** No production code.

Design follow-on from [`free-api-matrix.md`](../free-api-matrix.md). Scope fixed by §10 of that document:
**read-only oversight**, via the **Google Business Profile API** and the **Meta Conversations API** for
**Instagram and Messenger**. Telegram and Discord are out.

This document answers one question per surface: *what changes on screen, and what must the screen now say
that it does not say today.* It is written against the tree, with `path:line` for every claim about current
behaviour.

---

## 1 · The gate that decides the whole Meta design

`Models/PlatformCapabilities.cs` declares a standing prohibition, and it is aimed squarely at these two
channels:

> `RequiresThreadOpenToRead` — *"Meta's web clients mark a thread read and fire a read receipt to the
> customer the moment the thread is opened. For an oversight app that is disqualifying: measuring the
> awaiting-reply signal would destroy it and tell the customer we looked."*

Both Instagram and Messenger carry it today via `MetaAggregateOnly`
([PlatformDefinition.cs:63](../../UnifiedMessenger/Models/PlatformDefinition.cs#L63)), which sets
`IsMessageChannel = true` and nothing else. That is why they are aggregate-only.

**That flag is a fact about the web client, not about the platform.** The Conversations API is a different
access path, not a better adapter over the same DOM. Meta's Send API documents `mark_seen` as an explicit
sender action the app chooses to POST — *"Send the `mark_seen` indicator when your bot receives a message so
that the user does not feel ignored"* — a recommendation that would be meaningless if reading already marked
messages seen. No Meta documentation says a `GET` marks a thread read.

**Status: LIKELY, not CONFIRMED — and everything below depends on it.** The inference is strong, but the
cost of being wrong is that the app silently tells customers *"we saw your message"* while the owner has not
replied. That is worse than not building the channel at all.

> **Falsifiable test, ~10 minutes, folded into the one-hour Meta experiment already named in
> `free-api-matrix.md` §5.5:** from the dev-mode app, `GET /CONVERSATION-ID?fields=messages` on a thread with
> an unread inbound message, then check on a second device whether the "Seen" marker appeared for the sender.
> If it did, stop — Meta stays aggregate-only and §4 of this document is what ships. If it did not,
> `RequiresThreadOpenToRead` becomes false *for the API-backed adapter* and §3 is what ships.

**Design consequence either way.** `PlatformCapabilities` currently splits flags into "platform facts, which
do not change when we write a better adapter" and "adapter capabilities". This finding puts a third case
between them: a flag that is true for one access path and false for another, on the same platform. The
taxonomy comment needs amending, and the honest fix is that Instagram-via-scrape and Instagram-via-API are
**two capability sets**, not one platform whose flags flipped. Phase 3 decides whether that is a second
`PlatformDefinition` entry or a capability set selected by the resolved adapter; it is not a Phase 2 call.

---

## 2 · What each channel declares

The whole visual result follows from this table, because every surface already asks capabilities rather than
hard-coding platform ids.

| | `IsMessageChannel` | `CanReadUnread` | `CanReadPreview` | `CanReadTimestamps` | `CanReadContactIdentity` | `SupportsFrt` | `UsesWhatsAppIndexedDbPipeline` |
|---|---|---|---|---|---|---|---|
| WhatsApp family (today) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Google Business (today **and after the API**) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Instagram / Messenger — **today** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Instagram / Messenger — **API, if the read is passive** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ * | ❌ |
| Instagram / Messenger — **API, if the read marks seen** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |

\* `SupportsFrt` requires timestamps **plus direction**. The Conversations API supplies both — `messages`
carries `from` and `created_time`. Flag it only once a live read confirms the message list is deep enough to
find the inbound that started a thread; per the file's own rule, *"never set one optimistically: a true flag
is a promise that a consumer can read that field today."*

**Google's row does not move, and that is correct.** `IsMessageChannel = false` is permanent — Google
Business Messages was shut down in July 2024. The API changes the *reader* behind
`GoogleReviewSnapshotService`, not the channel's nature. Everything Google gains is on the Review Desk (§7),
and none of it belongs in the conversation metrics.

**`UsesWhatsAppIndexedDbPipeline` stays false for every new channel.** That gate is
[PlatformModuleSettingsHelper.cs:24](../../UnifiedMessenger/Services/PlatformModuleSettingsHelper.cs#L24)
and its own comment is the rule: *"A new channel earns oversight metrics by declaring its own capabilities
and shipping its own adapter — never by being added here."* Section 5 is entirely about what that costs.

---

## 3 · Dashboard and command centre

### The merge question, argued

**Merge. Channel is an attribute of a row, never a container for one.**

The owner's first question is *who is waiting*. A customer waiting on Instagram is waiting exactly as much as
one waiting on WhatsApp, and the whole product thesis is that the owner should not have to do the merge in
their head across three branches and four tabs. Splitting the queue by channel rebuilds the tab bar the app
exists to replace.

The counter-argument — that channels have different reply norms, so mixing them mixes urgencies — is real but
is already handled somewhere better: the age buckets. `CommandCenterPanel.xaml.cs:1207-1222` grades waiting
time into `<15m / 15m–1h / 1–4h / >4h` for live chats and `today / 1–7d / 1–4w / >1 month` for the
carried-over ones. Those thresholds encode urgency directly. A channel label adds nothing the age bucket does
not already say better.

Three things must stay separable, and two of them already are:

1. **The on-time denominator.** Already correct.
   [OversightRollupBuilder.cs:116-118](../../UnifiedMessenger/Services/Oversight/OversightRollupBuilder.cs#L116)
   filters `measuredReplied`/`measuredOpen` by `capabilities(id).SupportsFrt` and sets `supportsTiming` from
   whether *any* member can time. A channel that cannot be measured is excluded rather than counted as a
   miss. **Zero new work** — a Meta account with `SupportsFrt = false` joins the awaiting count and stays out
   of the percentage automatically, and `OversightEntityHealth.SupportsResponseTiming` already forces the
   card to say so instead of printing a flattering 100%.
2. **A per-channel filter chip**, so the owner *can* separate on demand without the default doing it for
   them. `CommandCenterPanel.xaml.cs:975-1004` already builds a branch chip row (`All branches`, one per
   location) and a facet row from the same `BuildFilterChip` helper. A channel row is the same control with a
   different source list. Show it **only when more than one channel contributes** — a chip row offering
   "All channels / WhatsApp" on a single-channel install is noise.
3. **Coverage labelling.** §6.

### What flows in for free

`CommandCenterPanel.xaml.cs:379` gates command-centre inclusion on
`PlatformModuleSettingsHelper.ContributesConversationMetrics`, which is
`IsMessageChannel && CanReadUnread`. **The moment a Meta account declares `CanReadUnread`, it appears** —
cards, the worst-first sort, the Needs-reply flat list, age buckets, search, branch grouping, the mark-handled
and snooze actions from `Controls/Shared/AwaitingChatActions`, and the KPI micro-trend sparklines. None of
that is WhatsApp-shaped; it is `ThreadData`-shaped.

That is the payoff of the capability model, and it is why the dashboard is the cheapest surface to extend and
the analytics page (§5) is the most expensive.

### What must change

| Change | Why |
|---|---|
| **Channel badge on each awaiting row** in the Needs-reply list | Once rows can come from three places, the owner needs to know which app to open to reply. A small glyph + the platform accent (`PlatformDefinition.AccentColor`, already declared per platform), not a text column |
| **Channel filter chip row**, hidden at one channel | Above |
| **Card subtitle names the channels rolled up** in `ByLocation` grouping | A location card says `AccountCount` today. "3 accounts" is fine; "3 accounts · WhatsApp, Instagram" tells the owner what a zero means |
| **`IsAggregateOnly` rendering path must actually exist** | The flag is declared and documented — *"callers should render a count and explicitly say detail is unavailable, rather than showing an empty list"* — but nothing consumes it (`grep` finds one reference, in its own file). If the read-receipt test comes back bad (§4), this becomes load-bearing overnight and it has never been rendered |

### Sidebar

`WorkspaceSidebarMenuPlanner.cs:70,86` filters on `IsSidebarVisible`, which is *any registered platform* —
so Instagram and Messenger accounts already appear once added. The only work is removing `instagram` from
`HiddenFromPicker`
([PlatformModuleSettingsHelper.cs:53](../../UnifiedMessenger/Services/PlatformModuleSettingsHelper.cs#L53));
`messenger` is already offered.

**And the `Description` strings must be rewritten in the same commit that flips the flags, not after.**
`PlatformDescriptionTests` fails the build on roadmap words and requires unmeasured channels to say "No
oversight metrics" — so today's Instagram description is correct and would become a lie the moment the
adapter lands. The test enforces one direction; nothing enforces the other. Phase 3 should make the
description assertion derive from `Capabilities` so the two cannot drift.

---

## 4 · The fallback design, if the API read marks threads seen

Written out because it must not be improvised under time pressure, and because it is a perfectly respectable
product.

Instagram and Messenger stay `IsMessageChannel = true, CanReadUnread = true`, everything else false. They
contribute **one number per account: how many customers are waiting.** No names, no previews, no ages, no
reply times.

- They appear as **cards**, not rows. `ContributesConversationMetrics` is true, so a card is built.
- The card shows `AwaitingCount` and an explicit line: *"Instagram shows counts only — open Instagram to see
  who."* Never an empty list where WhatsApp shows names.
- They are **absent from the Needs-reply flat list**, because that list is per-conversation. A count cannot
  join a list of rows, and padding it with "3 unnamed Instagram conversations" is the "never show 0 where the
  truth is unknown" rule in a different costume.
- `SupportsFrt = false`, so they are excluded from on-time % — already automatic.
- The location card's aggregate awaiting count **does** include them, and the card must say the detail
  underneath covers fewer channels than the number above it.

This is the `IsAggregateOnly` path the model already anticipates. It is meaningfully useful — "eleven people
are waiting on Instagram" is information the owner does not have today — and it is honest.

---

## 5 · Analytics — the surface with a real problem

**Today the Analytics page is WhatsApp-only by construction, and it does not say so.**

`UnifiedMessengerDashboardService.cs:34,146,198` and `ActivityPatternsPanel.xaml.cs:81` all gate on
`IsPlatformModuleEnabled`, which is `UsesWhatsAppIndexedDbPipeline`, which is WhatsApp family and nothing
else. That is *currently harmless* because WhatsApp is the only channel with metrics, so "all accounts" and
"all WhatsApp accounts" name the same set.

**Adding Meta breaks that silently.** `AnalyticsPage.xaml:176-180` reads:

> **Messages by Account** — *"Share of message volume across your accounts."*

With an Instagram account connected and contributing to the dashboard, that sentence becomes false on a
screen the owner uses to make decisions, and nothing on the page reveals it. This is precisely the defect
class the v4.99.46–47 audit fixed three times (D9, D10) — a figure covering some channels and not others,
presented as covering all.

### The choice, and my recommendation

| | What it means | Cost | Consequence |
|---|---|---|---|
| **A. Scope the page honestly** | Every WhatsApp-pipeline figure gains a scope line: *"WhatsApp accounts only."* The chart title becomes "Messages by WhatsApp account" | XS | Immediately true. The owner sees a gap and knows it is a gap. Analytics stops growing with the product |
| **B. Second data path** | Meta conversations feed `MessageAnalyticsService` through a non-IndexedDB route | L | Analytics covers everything. This is a large piece of work and it is where the `UsesWhatsAppIndexedDbPipeline` gate would be under most pressure to be widened — which its own comment forbids |
| **C. Do nothing** | | — | The page lies by omission the day Instagram connects |

**Recommendation: A now, in the same increment that lands the first Meta account — B later, or never.**
A is cheap, it is honest, and it is reversible. B is a real project and should be justified by the owner
actually wanting cross-channel volume history, not by the page looking incomplete. C is not an option; the
scope label is not optional work that can slip to a follow-up increment, because the increment that connects
Instagram is the increment that makes the page wrong.

### Per-element

| Element | Source | After Meta |
|---|---|---|
| Messages Over Time | WhatsApp pipeline | Scope line |
| Average Response Time | WhatsApp pipeline | Scope line |
| Caught up by thread | WhatsApp pipeline | Scope line |
| Replies Within 15 Minutes | WhatsApp pipeline | Scope line |
| **Account Leaderboard** — *"Ranked by on-time reply rate"* | WhatsApp pipeline | **Worst case here.** A leaderboard that silently omits accounts implies the omitted ones scored nothing. Either scope it explicitly or list non-timing accounts in a separate "not measured" group — never rank them |
| **Messages by Account** | WhatsApp pipeline | Retitle to name the channel scope |
| Branch filter (v4.99.71) | Location metadata, not platform | **Unaffected** — it filters by branch, which is orthogonal to channel. Two independent filters, correctly |
| **CSV export** | `MessageAnalyticsService.cs:363-384` | **Already correct** — the row carries `instance.Platform` as a column, so the consumer can see the scope. No change needed |

---

## 6 · Reports and exports

`BusinessReport.BuildMarkdown` (`BusinessReport.cs:259-325`) is the weakest artifact here, because a
Markdown file outlives the screen that explains it.

| Line today | Problem | Fix |
|---|---|---|
| `# Business report — {PeriodLabel}` | No scope at all | Add one scope line under the heading naming the channels the figures cover, always — including when that is only WhatsApp |
| `- Customer messages this week: **N**` | Unscoped total | Covered by the scope line |
| `- Median first reply: **X** (N replies measured)` | Already states its sample size — good | Unchanged. This is the pattern the rest should copy |
| `## By account` table: `Account / Messages / Median reply / Waiting now` | An Instagram account appears with `—` in three columns and no explanation | Add a **Channel** column. `—` next to "Instagram" reads as *not measurable*; `—` with no channel reads as *broken* |

The at-a-glance block already models the right behaviour: `FrtSamplesThisWeek > 0` gates the reply-time
bullets out entirely rather than printing a zero. Extend that instinct — a channel with no timing data
contributes no timing row, and the scope line explains why.

**PNG export** (`WeeklyReportDialog`) renders the same content; the scope line rides along for free.

---

## 7 · Review Desk — coverage goes from *the first 50 of 1,671* to complete

Google is the smallest visual change and the largest correctness change, because the desk was **built** to
state its own limits and most of those limits disappear.

| Today | With the API |
|---|---|
| `ReviewCoverage.Describe` → *"covers the first 50 of 1,671"* | → *"covers all 1,671 reviews"*, from `reachedLastPage` being genuinely reached via `nextPageToken`, checksummed against `totalReviewCount` |
| `QueueIsSample(shown, unanswered)` is **true** — the scrape reads the reply-button count for the page but builds preview text for only the first handful, because each preview costs a DOM expansion | **False.** Every review in the list response carries its `comment` inline. The queue stops being a sample, and everything derived from it — oldest waiting, how many at ≤3 stars — loses the sample caveat |
| Reply rate is over the loaded window | Over the whole profile |
| Rating and lifetime total come from a **separate** 6-hourly Search-merchant-view scrape that can disagree with the manager page | `averageRating` and `totalReviewCount` are in the **same response body**. The two-source disagreement `ReviewCoverage`'s docs describe stops existing. The 6-hour throttle, the two regex layouts and the visible navigation all go away |
| Per-review dates are unavailable, so `ReviewTrend` derives velocity from the lifetime total's movement, and tier 2 **needs a second day of readings before trend tiles say anything** | `createTime` per review. **History is available on first sync** — the trend tiles work on day one instead of day two |
| **D6** — reply dates unobtainable; the tile says so | `reviewReply.updateTime` is a real timestamp. **Median review reply time becomes a real measurement** |
| Owner-decision §2 — *"should review reply time measure from installation?"* | **Closed as unnecessary.** Real historic dates exist; no "since 12 March" footnote forever |

### What must not be deleted

`ReviewCoverage`'s partial-coverage vocabulary. It stops firing in the happy path but it is still the correct
answer during first sync, on a rate limit, on an expired token, and offline. A desk that can only say
"covers all" is a desk that will one day say "covers all" about 50 reviews.

### One new caveat to add

`reviewReply.updateTime` is *last modified*, not *first replied* — editing a reply moves it. For an unedited
reply they are the same, which is nearly always. The tile must read **"reply last updated"**, not "replied".
Getting this wrong would be a new instance of exactly what D10 was.

### Unchanged

The business-wide rating stays a **weighted mean**, labelled as one. Google publishes a rating per location
and never one for the business; the API does not change that, and the label must survive.

---

## 8 · The honesty ledger

Every figure whose meaning changes, in one place, so Phase 3 can turn each row into a test.

| # | Figure | Today | Must say |
|---|---|---|---|
| H1 | "Messages by Account" / *share across your accounts* | True by accident | Name the channel scope |
| H2 | Account Leaderboard | Ranks all measurable accounts | Never rank an unmeasurable account; list it separately or scope the whole board |
| H3 | Every Analytics KPI | WhatsApp-only, unlabelled | One scope line on the page |
| H4 | Business report at-a-glance | Unscoped | Scope line under the heading |
| H5 | `## By account` table | No channel column | Add one, so `—` reads as *not measured* not *broken* |
| H6 | Location card `AccountCount` | Accounts only | Name the channels when mixed |
| H7 | Awaiting rows | No channel | Channel badge |
| H8 | On-time % | Already excludes non-FRT channels and says so | **No change — this one is already right** |
| H9 | Review coverage | *"covers the first 50 of 1,671"* | *"covers all N"*, and keep the partial vocabulary for degraded reads |
| H10 | Review reply time | *"not available"* | Real median, labelled **"reply last updated"** |
| H11 | Instagram/Messenger card, aggregate-only fallback | n/a | *"counts only — open Instagram to see who"*; never an empty list |
| H12 | `PlatformDefinition.Description` for Instagram | *"No oversight metrics"* — correct today | Must change in the same commit as the flags. Derive the assertion from `Capabilities` so it cannot drift |

---

## 9 · What does not change

Worth stating, because the temptation in a channel expansion is to touch everything.

- **WhatsApp.** The store bridge, IndexedDB fallback, preview harvest, backfill and `@lid` phone resolution
  are untouched. No API exists for them (`free-api-matrix.md` §5.3) and none is wanted.
- **The `UsesWhatsAppIndexedDbPipeline` gate.** Not widened. New channels bring their own reader.
- **`BackfillSyncManager` / `OversightSnapshotReader` / `PlatformAdapters`.** All correctly gated to
  WhatsApp; all left alone.
- **The branch filter.** Orthogonal to channel.
- **Quiet hours, mark-handled, snooze, the alert monitor, the notification hub.** All operate on
  `ThreadData` and instance ids, not on platforms.
- **The Google channel's nature.** Reviews and Q&A only, permanently — and Q&A's API is dead
  (`free-api-matrix.md` §5.2), so Google remains reviews-only in practice as well as in principle.
- **Anything that sends.** Read-only was the owner's decision. `reviews.updateReply` and the Meta Send API
  are reachable with the credentials this design acquires and are deliberately never called.

---

## 10 · Open for Phase 3

1. **The read-receipt test** (§1). Nothing about Meta is designable until it runs. It is ~10 minutes inside an
   experiment already scheduled.
2. **One capability set per platform, or per adapter?** (§1). Instagram-via-scrape and Instagram-via-API are
   genuinely different capability sets on one platform id, and `PlatformCapabilities` has no shape for that.
3. **Where the token lives.** Flagged in `free-api-matrix.md` §5.1, still open. DPAPI `CurrentUser` is the
   obvious candidate; the owner's threat model has not been stated.
4. **What pins "the app never writes to Google."** The credential can post public replies. A source-level
   guard in the shape of `UserFacingErrorTests` / `AccountVocabularyTests` is the repo's own idiom for this.
5. **Analytics option A or B** (§5). A is assumed throughout this document.
6. **Whether `IsAggregateOnly` gets a rendering path now or only if §4 fires.** Building it unconditionally
   is insurance against the read-receipt test coming back bad; building it speculatively is the thing this
   project is careful not to do.
