# API-backed channels — implementation roadmap

**As of:** 2026-08-29 (2nd revision) · **Head:** `89bd2ab`, **v4.99.73** · **Suite:** 1916 pass / 0 fail /
24 s · **App builds:** 0 warnings · **Branch:** `feat/free-api-research`, 5 commits, **not pushed**

**Phase 3 of 3.** Reads on from [`free-api-matrix.md`](free-api-matrix.md) (what is possible, incl. the
§11 re-research) and [`design/api-channels-spec.md`](design/api-channels-spec.md) (what the app becomes).

### What changed in this revision

1. **Increment 106 is shipped** (v4.99.73). It was the only item that needed nothing from the owner.
2. **Messenger is out.** The owner ruled out the Meta Page-based API; Messenger has no standalone route
   (`free-api-matrix.md` §11.2). This is a channel the owner said customers use — see §7 D6.
3. **Instagram is now the standalone Instagram-Login route**, `graph.instagram.com`, no Facebook Page.
4. **P2 has probably evaporated** — `business.manage` looks like a non-sensitive scope, which would delete
   the privacy-policy/Search-Console/demo-video prerequisite entirely. §1 P2 carries the correction.
5. Three operational constraints found on re-research now shape Track B: a 20-message history cap, a
   60-day token that dies if not refreshed, and a ~200 calls/hour limit.

### The assumption this roadmap is built on

**Reading an Instagram conversation over the API does not mark it seen** — assumed at the owner's
instruction. **LIKELY, not CONFIRMED** (`spec §1`). Dependent items are marked **⚠ ASSUMES-PASSIVE**;
§6 states what changes if the probe says otherwise. Nothing in Track A depends on it.

### Severity, as used here

Not defect severity — **the severity of the gap if this item is skipped, or shipped wrong**:
**S1** the app shows a false number or damages customer trust · **S2** a figure becomes misleading or a
documented constraint is silently broken · **S3** a real capability is missing; nothing is untrue ·
**S4** consistency and documentation.

---

## 0 · At a glance

| | |
|---|---|
| **Shipped** | 1 — increment 106, v4.99.73 |
| **Remaining** | 9 — increments 107–115, v4.99.74 → v4.99.82 |
| **Implementable without the owner today** | **0** — see §7 |
| **Blocked on an external clock** | all 9 |
| **Longest pole** | Google API access approval, LIKELY 10+ business days |
| **Cheapest decisive actions** | 2 minutes (read one badge in Cloud Console) and ~1 hour (the Instagram probe) |
| **New runtime dependencies** | exactly one — `Google.Apis.Auth` (Apache-2.0) |

---

## 1 · Prerequisites — owner tasks, not increments

**Start P1 and P3 on the same day.** P0 is two minutes and may delete P2 outright.

### P0 · Read one badge in the Cloud Console — 2 minutes — **do this first**

Add `https://www.googleapis.com/auth/business.manage` to the OAuth consent screen of the Cloud project and
read the category it is labelled with. Google states the Console is where scope category is *"indicated
automatically"*.

- **Non-sensitive** (expected) → **P2 is deleted.** Publish to Production, no verification, long-lived
  refresh token, done.
- **Sensitive** → P2 applies as written below.

This costs nothing and decides several days of work either way.

### P1 · Google Business Profile API access request — blocks 108–110

**Needs:** a Business Profile verified and active 60+ days (✅) · a website representing the business (✅) ·
the GBP API contact form submitted with the Cloud project number, from an email that is an **owner or
manager** on the profile. **Default quota is 0 QPM until approved** — the project is unusable, not slow.
Timeline unstated by Google; a developer-forum thread reports 10+ business days (LIKELY).

**Only the owner can do this** — the form requires their profile-linked email. No alternative route exists;
the API cannot be reached without it.

**Done when:** the Cloud console shows 300 QPM instead of 0.

### P2 · Google sensitive-scope verification — **probably unnecessary; gated on P0**

⚠ **Correction to the previous revision of this file.** It stated flatly that `business.manage` is a
sensitive scope and that the owner's `@gmail.com` therefore forced a multi-day verification. That was
asserted rather than checked. Google's scopes page carries **no** sensitive marker on that row, and its
verification help says *"if your app utilizes only non-sensitive scopes, it is not mandatory for your app to
complete the app verification process."*

**If P0 says non-sensitive, skip this entirely.** If it says sensitive: publicly accessible homepage · a
privacy policy hosted **on the same domain** and linked from the consent screen · domain ownership in Search
Console · branding published · a demo video. *"typically takes 3-5 business days"*, no fee. The privacy
policy is the only real work, and given the zero-egress rule it is an unusually easy one to write honestly.

**Alternatives researched, both rejected:** a Google Workspace account would allow user type *Internal* (no
verification, no expiry) but costs a monthly subscription, breaking the no-recurring-cost rule for a problem
P0 may show does not exist. Staying in *Testing* status avoids verification but issues **7-day refresh
tokens** — re-authorising weekly forever, which is not shippable.

### P3 · Instagram probe — blocks all of Track B — ~1 hour ⚠

**The single highest-leverage action in this plan.** Create an app on `developers.facebook.com` using the
**Instagram API with Instagram Login** setup — no Facebook Page required — add the owner's Instagram
professional account as a tester, then answer three questions and write the answers down:

1. Does `GET https://graph.instagram.com/me/conversations` return under Standard Access, with the owner as a
   tester? (This decides whether P4's multi-week App Review is needed at all.)
2. On a thread with an unread inbound message, `GET /<CONVERSATION_ID>?fields=messages`.
3. **From a second device, does the sender now see "Seen"?** ← the ASSUMES-PASSIVE gate.

**Also confirm in the same session:** that the Instagram account is a **Professional** account (business or
creator). That is a prerequisite and it is a setting on the owner's phone.

**Done when:** all three answers are written down. If (3) shows "Seen", stop and go to §6.

### P4 · Meta App Review — **only if P3 question 1 fails**

Free. 2–4 weeks, revisions restart the clock (LIKELY). Requires `instagram_business_basic` and
`instagram_business_manage_messages` in one submission, and reviewers expect to watch a test user message
the account and the app receive it. **Do not start until P3 says it is necessary** — it is a multi-week
commitment to answer a question a one-hour test may make moot.

---

## 2 · Track 0 — complete

### ✅ Increment 106 · v4.99.73 · Scope every WhatsApp-only figure · **S2** · effort **S** — **SHIPPED**

Analytics and the business report drew from the WhatsApp pipeline and presented the result as covering every
account — already false for an owner with three Google Business accounts connected.

**Shipped:** a `ChannelScope` helper feeding both the Analytics page and the business report, so the screen
and the export cannot disagree · the report carries the scope line under its heading and gained a **Channel**
column · the donut subtitle no longer claims to span all accounts · `ChannelScopeTests` (9 cases) ·
`PlatformDescriptionTests.MeasuredChannelsDoNotClaimToBeUnmeasured` closes the missing half of that guard,
landed early because increment 114 depends on it. Suite 1905 → 1916.

**One thing worth carrying forward.** A test written for this increment failed, and the *test* was wrong:
`PlatformDefinition.CapabilitiesFor` runs an unknown platform id through `NormalizePlatformId`, which falls
back to `"whatsapp"`, so a corrupt id genuinely **is** scanned by the WhatsApp pipeline. Counting it as
excluded would have made the scope line describe an app that does not exist. It is now pinned as *covered*,
with the reasoning, so nobody re-derives it.

---

## 3 · Track A — Google Business Profile API

Gated on **P1**, and on **P0** before 107 reaches the owner's machine.

### Increment 107 · v4.99.74 · OAuth, token storage, and the no-write guard · **S1** · effort **M**

**Problem.** The app has no way to hold a Google credential, and the only scope Google offers for reviews can
write.

**Why it matters.** `business.manage` can post public replies to customer reviews. The owner chose
read-only. The app will hold a credential capable of doing something the owner has forbidden, and "nobody
will add that call" is not a control.

**The correction.**

1. Add `Google.Apis.Auth` (Apache-2.0, 1.76.0) to
   [`UnifiedMessenger.csproj:99`](../UnifiedMessenger/UnifiedMessenger.csproj#L99). **The only new runtime
   dependency in this roadmap.** There is no `Google.Apis.MyBusiness.v4` package — reviews were never
   regenerated into a client (`free-api-matrix.md` §9) — so the API call itself is `HttpClient` +
   `System.Text.Json`, both already referenced.
2. Installed-app authorization-code flow with a **loopback redirect** (`http://127.0.0.1:<port>`). No public
   URL, consistent with the rest of the app.
3. **Token storage: DPAPI** — `ProtectedData`, `DataProtectionScope.CurrentUser`, under
   `ApplicationPaths.UserDataRoot` beside the other stores. No new dependency; unreadable by another Windows
   user on the same box. **It is not protection against anything already running as the owner**, and the
   Settings copy must not imply otherwise. Route the load through `CorruptFileRecovery` and
   `ShellController.LoadStoreAsync` — `StoreLoadDurabilityTests` has a source guard that fails the build on
   a bare `await …LoadAsync()` in that block.
4. Settings → Data gains **Connect Google Business** / **Disconnect**, following the `UseStoreBridgeToggle`
   shape at [`SettingsPage.Data.partial.cs:19`](../UnifiedMessenger/Pages/SettingsPage.Data.partial.cs#L19)
   — including its `async void` lesson: the save must not be able to close the app.
5. **The guard.** A source-level test asserting the app issues no write to Google: no `PutAsync`/`PostAsync`/
   `DeleteAsync` against a `mybusiness.googleapis.com` URL, and no occurrence of `updateReply` or
   `deleteReply` under `UnifiedMessenger/`. The repo's own idiom —
   `UserFacingErrorTests.NoDialogShowsARawExceptionMessage` and `AccountVocabularyTests` both work this way.

**Verification.** Connect on the owner's machine; `app.log` records the acquisition with no secret in it.
Disconnect and confirm the stored file is gone. Leave it 8 days and confirm the refresh still works — that
is the test of P0/P2 and it cannot be shortened.

**Test that pins it.** The no-write guard · token-store round-trip through `CorruptFileRecovery` including a
locked file · a `ProtectedData` failure degrading to disconnected rather than throwing.

**What could break.** Two, both S1 if missed. **(a)** A token in `app.log` — the file support asks the owner
to send. Log the *outcome* and the *expiry*, never the value; `AppLogger`, never `Debug.WriteLine`.
**(b)** [`egress-inventory.md`](egress-inventory.md) currently documents that the app's own code contains
**no** `PostAsync`/`PutAsync`/`PatchAsync`/`SendAsync` at all, and that every non-loopback request is a
`GetAsync` to a constant URL. The OAuth token exchange is a `POST` and the first request carrying an
`Authorization` header. **Updating that file is inside this increment** — its whole value is being
re-derivable rather than asserted.

### Increment 108 · v4.99.75 · The reviews reader · **S3** · effort **M**

**Problem.** `GoogleReviewSnapshotService` scrapes reviews and stops after one page
([`:987`](../UnifiedMessenger/Services/Oversight/GoogleReviewSnapshotService.cs#L987), `MaxPages = 1`),
because walking pages over-counted by 2–3× (**D5**).

**Why it matters.** The owner sees a reply rate computed over the 50 most recent of ~1,671 reviews.

**The correction.** Page `accounts.locations.reviews.list` with `nextPageToken` until the token is absent,
then assert the count against `totalReviewCount` from the same response. A server cursor cannot double-count
the way a DOM scroll did — **but assert anyway**, and on mismatch return `ReachedLastPage = false` and keep
the partial-coverage vocabulary rather than reporting a total that disagrees with Google's own. Use
`accounts.locations:batchGetReviews` for the three locations in one request. Parse into the existing
`ReviewHealth` shape so nothing downstream changes yet.

**Rate limits.** 300 QPM against three locations a few times a day is not a real constraint. Bounded retry
on `429`/`5xx`, fall back to the previous snapshot, never spin.

**Offline.** Join failures into `OfflineState` so the screen and `app.log` cannot disagree —
`OfflineAdviceOnScreenTests` already guards every site that tells the owner to reconnect.

**Verification.** Against the live account after P1: the traversal reaches the last page and the count equals
`totalReviewCount` for all three locations. Compare the rating with the current scrape's value.

**Test that pins it.** Reader tests over recorded JSON: multi-page traversal terminates · a missing
`nextPageToken` sets `ReachedLastPage = true` · a count mismatch sets it **false** · a `429` returns the
prior snapshot, not an empty one. Follow `GoogleReviewScrapeTests`.

**What could break.** The scrape must remain the fallback for an owner who has not connected. Both readers
ship; do not delete the scraper here.

### Increment 109 · v4.99.76 · Review Desk onto the API · **S2** · effort **M**

**Problem.** The desk's coverage vocabulary, sample caveats and second rating scrape all work around limits
the API removes.

**Why it matters.** Coverage goes from *"covers the first 50 of 1,671"* to *"covers all 1,671 reviews"*, and
the reply queue stops being a sample — so *oldest waiting* and *how many at ≤3 stars* become facts about the
profile rather than about eight loaded rows.

**The correction.** Route `ReviewQueue` and `ReviewCoverage` off the API reader when a token exists ·
`QueueIsSample` becomes false, because every review carries its `comment` inline · **retire
`ScrapeRatingAsync` on the API path** along with the 6-hour `RatingRefreshInterval`
([`:456`](../UnifiedMessenger/Services/Oversight/GoogleReviewSnapshotService.cs#L456)), the two anchored
regex layouts, and the visible navigation that yanks the owner's page away · the business-wide rating
**stays a weighted mean, still labelled as one**, because Google publishes one per location and never one
for the business.

**Verification.** On screen with all three profiles: coverage reads "covers all", the unanswered count
matches the queue length, and no navigation occurs during a manual Re-sync.

**Test that pins it.** `ReviewCoverage` keeps its full existing suite. Add: with a token and a complete
traversal `Describe` returns "covers all"; **with a token and a failed traversal it returns the partial
form**. The second case is the one that matters.

**What could break.** Deleting the partial-coverage path. It still fires on first sync, a rate limit, an
expired token and offline. A desk that can only say "covers all" will one day say it about 50 reviews.

### Increment 110 · v4.99.77 · Real review dates — closes D6 · **S3** · effort **S**

**Problem.** Reply dates are recorded as unobtainable (**D6**), and trend tiles *"need a second day of
readings before they say anything"*.

**The correction.** `createTime` per review feeds `ReviewTrend` and `ReviewHistoryStore` directly, so
**history exists on first sync** and velocity stops being derived from the lifetime total's movement ·
`reviewReply.updateTime` gives a real reply-time median, **labelled "reply last updated", not "replied"** —
it is *last modified*, and editing a reply moves it; getting this wrong is a fresh instance of D10 · close
**D6** and close **owner-decision §2** (*"measure review reply time from installation?"*) as **no longer
necessary**, because real historic dates exist.

**Verification.** A freshly connected profile renders trend tiles on day one with no second reading. The
reply-time median is non-empty and its label says "last updated".

**Test that pins it.** A label test that fails on the string "replied" in that tile · trend tests where a
single day of API data produces a non-empty trend.

**What could break.** `ReviewHistoryStore` now has two sources of truth. Prefer `createTime`, keep stored
readings as fallback, and do not merge two populations under one noun — D9's whole lesson.

---

## 4 · Track B — Instagram (standalone, Instagram Login)

**All ⚠ ASSUMES-PASSIVE.** Gated on **P3**. If P3 question 3 fails, go to §6.
**Messenger is not in this track** — see §7 D6.

### Increment 111 · v4.99.78 · One capability set per adapter, not per platform · **S2** · effort **S**

**Problem.** `PlatformCapabilities` files `RequiresThreadOpenToRead` as a *platform fact* that "does not
change when we write a better adapter". It is not one: it is true of Instagram's **web client** and false of
its **API**, on the same platform.

**Why it matters.** Flipping Instagram's flags otherwise means editing `MetaAggregateOnly`
([`PlatformDefinition.cs:63`](../UnifiedMessenger/Models/PlatformDefinition.cs#L63)), whose own comment says
it was *"set before any Meta adapter exists precisely so it constrains whoever writes one"*. Editing it in
place would erase the prohibition for the scrape path, where it remains entirely true.

**The correction.** Make the capability set a function of the **resolved adapter**, not the platform id —
`PlatformAdapterInternals.ResolveEnabledAdapter` already switches on platform to pick an adapter, and
capabilities should follow the same resolution. `MetaAggregateOnly` stays exactly as written and remains
what an Instagram account **without** a connected credential reports. Amend the taxonomy comment to name
this third case.

**Verification.** With no credential connected, every capability answer is byte-identical to today.

**Test that pins it.** Unconnected Instagram resolves `MetaAggregateOnly`; connected resolves the API set;
**the scrape path can never report `RequiresThreadOpenToRead = false`**.

**What could break.** Anything caching capabilities per platform id for the process lifetime — they now
change at runtime when a credential is connected or disconnected.

### Increment 112 · v4.99.79 · The Instagram reader and adapter · **S3** · effort **L**

**Problem.** Instagram contributes nothing to oversight.

**The correction.** Follow `AGENTS.md` § *Platform adapter pattern*, minus the JS — there is no DOM here.
`GET https://graph.instagram.com/me/conversations` for threads, `GET /<CONVERSATION_ID>?fields=messages` for
messages, mapped to `ThreadData` through `ChatEntryParser` so both producers stay in step. Add a case in
`PlatformAdapterInternals.ResolveEnabledAdapter`. **Do not touch `UsesWhatsAppIndexedDbPipeline`.**

Flip capability flags **one at a time, each behind a read that works**: `CanReadUnread` first (it alone gates
command-centre inclusion), then `CanReadPreview`, `CanReadTimestamps`, `CanReadContactIdentity`, and
`SupportsFrt` **last**. *"Never set one optimistically: a true flag is a promise that a consumer can read
that field today."*

**Three platform constraints that shape this, all found on re-research:**

1. **20-message history cap.** *"you can only get details about the 20 most recent messages in the
   conversation."* Fine for *who is waiting* (needs the last message) and fine for forward-tracked FRT
   (`ResponseTimeTracker` already measures forward from a watch start). **Fatal for historical backfill** —
   there is no Instagram equivalent of the WhatsApp history import, and no surface may imply one. Threads in
   the Request folder inactive for 30+ days are absent entirely.
2. **The token expires in 60 days and dies if not refreshed.** Refreshable once 24 hours old and before
   expiry; *"tokens not refreshed within 60 days will expire and can no longer be refreshed."* **Leave the
   app closed for 60 days and the connection is permanently dead.** Refresh on startup, and surface an
   honest "Instagram disconnected — reconnect in Settings" state rather than a silent zero. Google's refresh
   token has no equivalent rule; do not generalise one to the other.
3. **~200 calls per user per hour.** Polling the conversation list every minute is 60 calls/hour before a
   single message read, so **fetching messages for every thread on every poll would breach it**. Use the
   list's `updated_time` to fetch messages only for threads that changed — which is also less work.

**Verification.** With `CanReadUnread` alone, the account appears in the command centre with a correct
awaiting count and is absent from on-time % — both automatic, via
[`CommandCenterPanel.xaml.cs:379`](../UnifiedMessenger/Controls/CommandCenterPanel.xaml.cs#L379) and
[`OversightRollupBuilder.cs:116`](../UnifiedMessenger/Services/Oversight/OversightRollupBuilder.cs#L116).
Check the count against Instagram on the owner's phone.

**Test that pins it.** Reader tests over recorded JSON · a rollup test that a `SupportsFrt = false` account
contributes to `AwaitingCount` and to neither side of the on-time ratio · a token-expiry test that produces
the disconnected state, never a zero.

**What could break.** Token expiry mid-scan leaving accounts in `Error` with no retry — F-OFFLINE-07's
shape, still open. Degrade to the last snapshot and say the data is stale.

### Increment 113 · v4.99.80 · Channel affordances on the dashboard · **S3** · effort **S**

**Problem.** Once rows come from two channels, the owner cannot tell which app to open to reply.

**The correction.** Per `spec §3`: a channel badge on each awaiting row using the platform glyph and
`AccentColor` (both already declared) · a channel filter chip row built with the existing `BuildFilterChip`
helper ([`:975`](../UnifiedMessenger/Controls/CommandCenterPanel.xaml.cs#L975)), **hidden when only one
channel contributes** · the location card subtitle naming the channels it rolls up.

**Test that pins it.** The chip row is absent with one contributing channel and present with two — one
filter with one option is furniture, the rule the sidebar's scope switch already follows.

**What could break.** `DesignScaleTests` reads `.cs` as well as `.xaml`, so the badge's sizes must be on the
scale — and any `FontIcon` must use a `"\uXXXX"` escape, never an inline character. All eight blank glyphs
found in v4.99.65 were inline.

### Increment 114 · v4.99.81 · Unhide Instagram; descriptions derived from capabilities · **S2** · effort **XS**

**Problem.** Instagram is in `HiddenFromPicker`
([`PlatformModuleSettingsHelper.cs:53`](../UnifiedMessenger/Services/PlatformModuleSettingsHelper.cs#L53))
and its `Description` says *"No oversight metrics"* — correct today, false the moment 112 lands.

**The correction.** Remove `instagram` from `HiddenFromPicker` · rewrite the description to what the channel
does, including that history starts at connection (constraint 1 above) · **derive the assertion from
`Capabilities`** so the two cannot drift.

> **Half of this already shipped in increment 106.**
> `PlatformDescriptionTests.MeasuredChannelsDoNotClaimToBeUnmeasured` now fails if a measured channel keeps
> claiming it is unmeasured — the direction nothing enforced. The guard is in place before the flags it
> protects start moving, which is the right order.

**What could break.** `metabusinesssuite` and `messenger` stay hidden/embed-only; neither has a standalone
route.

---

## 5 · Close-out

### Increment 115 · v4.99.82 · Documentation sync · **S4** · effort **S**

| File | Change |
|---|---|
| `docs/egress-inventory.md` | New §1 rows: `oauth2.googleapis.com` (POST), `mybusiness.googleapis.com` (GET), `graph.instagram.com` (GET). Correct the "no `PostAsync`" claim. Re-run each row's stated command rather than editing prose |
| `AGENTS.md` | Gotchas: no .NET client exists for the reviews API · `Google.Apis.MyBusinessQA.v1` is a live NuGet for a **dead** API · `RequiresThreadOpenToRead` is per-adapter, not per-platform · `business.manage` can write and a test forbids it · Instagram history starts at connection and its token dies at 60 days unused |
| `docs/remaining-work.md` | **D5 closed** (108) · **D6 closed** (110) · owner-decision §2 closed as unnecessary · Phase 5 updated: Instagram no longer gated on live-account DOM tuning; Messenger/Telegram/Discord remain embed-only |
| `CHANGELOG.md`, `README.md`, version sync | The five files in lockstep, every increment |

---

## 6 · ⚠ The fallback branch — if P3 shows the API marks threads seen

**Stop Track B at increment 111.** `spec §4` writes the alternative out in full; in roadmap terms it
collapses to one increment:

> **Increment 112-F · Instagram as an aggregate-only count · S3 · effort M.**
> `IsMessageChannel` and `CanReadUnread` true, everything else false. One number per account — how many
> customers are waiting — rendered as a **card**, never as rows in the Needs-reply list. The card says
> *"Instagram shows counts only — open Instagram to see who."* `SupportsFrt` stays false, so it is already
> excluded from on-time %.
>
> **This requires building the `IsAggregateOnly` rendering path, which has never existed** — the flag is
> declared and documented in `PlatformCapabilities` and `grep` finds no consumer outside its own file.
>
> 113 shrinks to the location-card subtitle only; there are no rows to badge and no queue to filter.

**Do not build the aggregate-only path speculatively before P3 answers.** It is a real screen with real copy,
and building it against a one-hour question nobody has asked is exactly what this project avoids.

---

## 7 · Deferred — what needs the owner, and what was researched instead

Requested explicitly: everything that needs the owner is listed here rather than half-built.

| # | Deferred item | Why it needs the owner | Alternative researched |
|---|---|---|---|
| **D1** | Google API access request (P1) | The form must come from an email that is an owner/manager on the Business Profile | **None exists.** The API is unreachable at 0 QPM without it |
| **D2** | Reading the scope badge in Cloud Console (P0) | Their Google account, their project | None — but it is 2 minutes and may delete P2 entirely |
| **D3** | Google sensitive-scope verification (P2) | Their domain, their website, their Search Console | Workspace account → *Internal* user type: **rejected**, recurring cost. Testing status: **rejected**, 7-day tokens |
| **D4** | The Instagram probe (P3) | Their Instagram account and a second device | **None.** No documentation answers the read-receipt question; it must be observed |
| **D5** | Instagram as a Professional account, if it is not already | A setting on their phone | None — it is a hard prerequisite of the API |
| **D6** | **Messenger** | They listed it as a channel customers use, then ruled out the Page-based API it requires | **Searched, none found.** Messenger conversations are Facebook-Page-owned; there is no Instagram-Login-style standalone route. Reinstating Messenger means reinstating the Page setup. **Their call** |
| **D7** | Code signing | Costs ≈ $120/yr, breaking the no-recurring-cost rule | SignPath Foundation is free but requires the repo be **public** under an OSS licence and signs as *"SignPath Foundation"*, not the business. **Rejected** |
| **D8** | Telegram / Discord | Selected as routes but not listed as channels customers use | Researched and viable (`free-api-matrix.md` §5.4, §5.6). Not roadmapped — a free API for a channel with no customers is worth nothing |

### Defaulted rather than deferred — no owner input needed unless they disagree

| Decision | Default taken | Reversible? |
|---|---|---|
| Token storage | **DPAPI `CurrentUser`.** No new dependency, standard for a local Windows app | Yes — one file |
| Version numbering | **Continue v4.99.x.** v5.0.0 at 109 or 113 is defensible | Yes — find-and-replace across five files |
| Analytics scope strategy | **Option A** (state the scope) — shipped in 106. Option B (a second data path so Analytics covers every channel) is an **L** and is not roadmapped | Yes |
| Instagram poll cadence | Driven by the ~200/hour limit and `updated_time`, not by a fixed timer | Yes |

### Why nothing else could be implemented now

The honesty ledger (`spec §8`) has twelve rows. **H1–H5 shipped in increment 106**; H8 was already correct.
The remaining six (H6, H7, H9, H10, H11, H12) each need a connected channel to be anything other than
speculative UI. The two refactors that look unblocked — the capability-per-adapter change (111) and the
`IsAggregateOnly` render path — have no consumer until an adapter exists, and building either now would be
scaffolding for a shape the probe may still change.

---

## 8 · What this roadmap deliberately does not do

- **Nothing that sends.** `reviews.updateReply` and the Instagram Send API are reachable with the credentials
  increment 107 acquires and are never called. 107 ships the test that keeps it that way.
- **Nothing to WhatsApp.** No API route exists that does not require deleting the number from the app the
  staff reply in (`free-api-matrix.md` §5.3).
- **No Facebook Page, anywhere.** That is what "standalone Instagram" means, and it is why Messenger is out.
- **No pretence that standalone means non-Meta.** `graph.instagram.com` is Meta, and the app is registered on
  `developers.facebook.com`. There is no non-Meta route to Instagram DMs that is not a banned unofficial
  library. If that was the intent, Instagram should be dropped rather than faked.
- **No Instagram history import.** The API returns the 20 most recent messages per thread. Oversight starts at
  connection and the UI must say so.
- **No Google Q&A** (API discontinued 2025-11-03), **no Tier-1 ONNX**, **no crash reporting**, and **no
  widening of `UsesWhatsAppIndexedDbPipeline`**.
- **No item that says "investigate."** P0–P4 are prerequisites with named artifacts and pass/fail outcomes.
