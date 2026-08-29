# API-backed channels — implementation roadmap

**As of:** 2026-08-29 · **Baseline:** `f35f27c`, v4.99.72 · **Suite:** 1905 pass / 0 fail / 23 s (run at
this baseline, app killed first) · **Branch:** `feat/free-api-research`, 3 commits, **not pushed**

**Phase 3 of 3.** Reads on from [`free-api-matrix.md`](free-api-matrix.md) (what is possible) and
[`design/api-channels-spec.md`](design/api-channels-spec.md) (what the app becomes). Scope fixed by the
owner on 2026-08-29: **read-only oversight**, **Google Business Profile API** + **Meta Conversations API**
for **Instagram and Messenger**. Telegram and Discord are out.

### The assumption this roadmap is built on

**Reading a Meta conversation over the Graph API does not mark it seen** — assumed at the owner's
instruction. It is **LIKELY, not CONFIRMED** (`spec §1`). Every item that depends on it is marked
**⚠ ASSUMES-PASSIVE**, and §6 states exactly what changes if the probe comes back the other way. Nothing in
Track A (Google) depends on it.

### Severity, as used here

Not defect severity — **the severity of the gap if this item is skipped, or shipped wrong**:

| | |
|---|---|
| **S1** | The app shows the owner a false number, or damages customer trust |
| **S2** | A figure becomes misleading, or a documented constraint is silently broken |
| **S3** | A real capability is missing or degraded; nothing is untrue |
| **S4** | Consistency, documentation, developer-facing correctness |

---

## 0 · At a glance

| | |
|---|---|
| **Increments** | 10 — 106 through 115, v4.99.73 → v4.99.82 |
| **Unblocked today** | 1 (increment 106) |
| **Blocked on an external clock** | 9 — see §1 |
| **Longest pole** | Google API access approval, LIKELY 10+ business days, **submit on day 1** |
| **Cheapest decisive action** | The Meta read-receipt probe, ~1 hour, no code |
| **New runtime dependencies** | exactly one — `Google.Apis.Auth` (Apache-2.0) |

**Ordering principle.** The one unblocked increment is also the one that must land *before* any new channel
connects, so the sequence is not a compromise: do the honesty work while the approvals are in flight.

---

## 1 · Prerequisites — owner tasks, not increments

These have external clocks. None is code. **Start P1 and P3 on the same day.**

### P1 · Google Business Profile API access request — blocks 108–110

**What it needs:** a Business Profile verified and active 60+ days (✅ satisfied), a website representing the
business (✅ required anyway), and the GBP API contact form submitted with the Cloud project number from an
email that is an owner or manager on the profile. **Default quota is 0 QPM until approved** — the project is
unusable, not slow, until then. Timeline unstated by Google; a developer-forum thread reports 10+ business
days (LIKELY).

**Done when:** the Cloud console shows 300 QPM instead of 0.

### P2 · Google sensitive-scope verification — blocks 107 shipping to the owner's machine

Forced by the owner's account being a personal `@gmail.com` (`free-api-matrix.md` §10 decision 3), which
closes the Workspace "Internal" escape. Without it the app stays in Testing publishing status and **the
refresh token expires every 7 days** — re-authorising weekly forever, which is not shippable.

**What it needs:** a publicly accessible homepage · a privacy policy hosted **on the same domain** and linked
from the OAuth consent screen · domain ownership proved in Google Search Console · branding published in the
Cloud console · a demo video showing the scope in use. *"typically takes 3-5 business days"*. No fee.

**The privacy policy is the real work here** and nobody has written it. It must describe what the app does
with Google user data — which, given the zero-egress rule, is an unusually easy document to write honestly:
the data is read, stored locally, and never transmitted anywhere.

**Done when:** the consent screen shows no "unverified app" interstitial and a refresh token survives 8 days.

### P3 · Meta read-receipt probe — blocks all of Track B, ~1 hour ⚠

**The single highest-leverage action in this plan.** Create a Meta app in development mode, add the owner's
Instagram professional account as a tester, then:

1. `GET /PAGE-ID/conversations?platform=instagram` — does it return without App Review? (This also settles the
   §5.5 UNKNOWN: whether the tester path avoids a 2–4 week review entirely.)
2. On a thread with an unread inbound message, `GET /CONVERSATION-ID?fields=messages`.
3. **From a second device, check whether the sender now sees "Seen".**

**Done when:** both answers are written down. If (3) shows "Seen", stop and go to §6.

**Also confirm, same session:** that the Instagram account is a Professional account and is linked to a
Facebook Page. Both are prerequisites and both are readable off the owner's phone.

### P4 · Meta App Review + Business Verification — blocks Track B *only if P3 step 1 fails*

Free. 2–4 weeks, revisions restart the clock (LIKELY). Needs official business documents for Business
Verification. **Do not start this until P3 says it is necessary** — it is a multi-week commitment to answer a
question a one-hour test may make moot.

---

## 2 · Track 0 — unblocked, do now

### Increment 106 · v4.99.73 · Scope every WhatsApp-only figure · **S2** · effort **S**

**Problem.** The Analytics page and the business report present WhatsApp-only figures as covering all
accounts, and say nothing about it.

**Why it matters to the owner.** This is already wrong today, not merely wrong later: the owner has three
Google Business accounts that the Analytics page silently excludes, so *"Share of message volume across your
accounts"* is a sentence about eight accounts rendered from five. The day an Instagram account connects it
gets worse and stays invisible. This is the exact defect class the v4.99.46–47 audit fixed three times.

**Where.**

| File | What |
|---|---|
| [`Services/UnifiedMessengerDashboardService.cs:34`](../UnifiedMessenger/Services/UnifiedMessengerDashboardService.cs#L34), `:146`, `:198` | The three `IsPlatformModuleEnabled` gates that make the page WhatsApp-only |
| [`Controls/ActivityPatternsPanel.xaml.cs:81`](../UnifiedMessenger/Controls/ActivityPatternsPanel.xaml.cs#L81) | Same gate |
| [`Pages/AnalyticsPage.xaml:118,125,138,145`](../UnifiedMessenger/Pages/AnalyticsPage.xaml#L118) | The four KPI section headers |
| `Pages/AnalyticsPage.xaml:162-166` | Account Leaderboard — *"Ranked by on-time reply rate."* |
| `Pages/AnalyticsPage.xaml:176-180` | Messages by Account — *"Share of message volume across your accounts."* |
| [`Services/Analytics/BusinessReport.cs:263`](../UnifiedMessenger/Services/Analytics/BusinessReport.cs#L263) | `# Business report — {PeriodLabel}`, no scope |
| `Services/Analytics/BusinessReport.cs:314` | `## By account` table, no channel column |

**The correction.**

1. One scope line on the Analytics page, under the title, naming the channels the figures cover — derived
   from the instances actually in scope, never a hardcoded "WhatsApp". Sits beside the existing branch filter
   (v4.99.71); the two are orthogonal and must read as orthogonal.
2. **Messages by Account** subtitle names the scope. **Account Leaderboard** must never rank an account it
   cannot measure: either scope the whole board or list unmeasurable accounts in a separate, unranked
   "not measured" group. Ranking is the dangerous half — an omitted account reads as a zero-scoring one.
3. `BusinessReport.BuildMarkdown` gains a scope line under the `#` heading, **always**, including when the
   answer is only WhatsApp. A saved `.md` outlives the screen that explained it.
4. `## By account` gains a **Channel** column, so a `—` in the reply-time column reads as *not measured*
   rather than *broken*.
5. The CSV export needs **nothing** — `MessageAnalyticsService.cs:384` already writes `instance.Platform`
   per row. Verify, do not touch.

**Verification.** Analytics and Reports open on screen with three Google accounts connected; the scope line
names WhatsApp and the count matches the accounts actually charted. Export one `.md` and read the heading.

**Test that pins it.** New `ChannelScopeTests`: (a) the report markdown contains a scope line for every
input, including a single-channel one; (b) the `## By account` table has a Channel column whenever a row
exists; (c) a leaderboard built from a mixed set never contains an account whose capabilities report
`SupportsFrt = false`. Follow `AnalyticsSlaLabellingTests` — the existing test for exactly this class of
problem — rather than inventing a new shape.

**What could break.** The scope line is derived, so it must not crash on an empty registry — reuse the
existing `NoAccountsState` path rather than rendering a scope line over nothing. `BusinessReportTests` and
`BusinessReportSharePercentTests` assert on the markdown and will need updating; that is the test doing its
job, not a regression.

---

## 3 · Track A — Google Business Profile API

Gated on **P1**. Increment 107 is additionally gated on **P2** before it reaches the owner's machine.

### Increment 107 · v4.99.74 · OAuth, token storage, and the no-write guard · **S1** · effort **M**

**Problem.** The app has no way to hold a Google credential, and the only scope Google offers for reviews can
write.

**Why it matters.** `business.manage` can post public replies to customer reviews. The owner chose read-only.
The app will hold a credential capable of doing something the owner has forbidden, and "nobody will add that
call" is not a control.

**The correction.**

1. Add `Google.Apis.Auth` (Apache-2.0, 1.76.0) to
   [`UnifiedMessenger.csproj:99-108`](../UnifiedMessenger/UnifiedMessenger.csproj#L99). **The only new
   runtime dependency in this roadmap.** There is no `Google.Apis.MyBusiness.v4` package — reviews were never
   regenerated into a client (`free-api-matrix.md` §9) — so the API call itself is `HttpClient` +
   `System.Text.Json`, both already present.
2. Installed-app authorization-code flow with a **loopback redirect** (`http://127.0.0.1:<port>`). No public
   URL, consistent with everything else in this app.
3. **Token storage: DPAPI.** `System.Security.Cryptography.ProtectedData`, `DataProtectionScope.CurrentUser`,
   written under `ApplicationPaths.UserDataRoot` beside the other stores. No new dependency, unreadable by
   another Windows user on the same box. **It is not protection against anything already running as the
   owner**, and the Settings copy must not imply otherwise. Route the load through `CorruptFileRecovery`
   and `ShellController.LoadStoreAsync` like every other store — `StoreLoadDurabilityTests` has a source
   guard that fails the build on a bare `await …LoadAsync()` in that block.
4. Settings → Data gains **Connect Google Business** / **Disconnect**, following the
   `UseStoreBridgeToggle` shape at [`SettingsPage.Data.partial.cs:19`](../UnifiedMessenger/Pages/SettingsPage.Data.partial.cs#L19)
   — including its `async void` lesson: the save must not be able to close the app.
5. **The guard.** A source-level test asserting the app issues no write to Google: no `PutAsync`/`PostAsync`/
   `DeleteAsync` to a `mybusiness.googleapis.com` URL, and no occurrence of `updateReply` or `deleteReply` in
   `UnifiedMessenger/`. This is the repo's own idiom — `UserFacingErrorTests.NoDialogShowsARawExceptionMessage`
   and `AccountVocabularyTests` both work this way.

**Verification.** Connect on the owner's machine; `app.log` records the token acquisition with no secret in
it. Disconnect and confirm the stored file is gone. Leave it 8 days and confirm the refresh still works —
that is the test of P2, and it cannot be shortened.

**Test that pins it.** The no-write guard above; token-store round-trip through `CorruptFileRecovery`
including a locked-file case; `ProtectedData` failure degrades to disconnected rather than throwing.

**What could break.** Two, both S1 if missed. **(a)** A token in `app.log` — the file support asks the owner
to send. Log the *outcome* and the *expiry*, never the value; `AppLogger`, never `Debug.WriteLine`.
**(b)** `egress-inventory.md` currently documents that the app's own code contains **no** `PostAsync`,
`PutAsync`, `PatchAsync` or `SendAsync` at all, and that every non-loopback request is a `GetAsync` to a
constant URL. The OAuth token exchange is a `POST` to `oauth2.googleapis.com` and is the first request the
app makes carrying an `Authorization` header. **Updating that file is part of this increment, not a
follow-up** — its entire value is that it is re-derivable rather than asserted.

### Increment 108 · v4.99.75 · The reviews reader · **S3** · effort **M**

**Problem.** `GoogleReviewSnapshotService` reads reviews by scraping and stops after one page
([`:987`](../UnifiedMessenger/Services/Oversight/GoogleReviewSnapshotService.cs#L987), `MaxPages = 1`),
because walking pages over-counted by 2–3× (D5).

**Why it matters.** The owner sees a reply rate computed over the 50 most recent of ~1,671 reviews and
labelled as covering the first 50. Honest, and much less useful than the truth.

**The correction.** A reader that pages `accounts.locations.reviews.list` with `nextPageToken` until the
token is absent, then asserts the count against `totalReviewCount` from the same response. **A server cursor
cannot double-count the way a DOM scroll did** — but assert it anyway, and if the assertion fails, return
`ReachedLastPage = false` and keep the partial-coverage vocabulary rather than reporting a total that
disagrees with Google's own. Use `accounts.locations:batchGetReviews` for the three locations in one request.
Parse into the existing `ReviewHealth` shape so nothing downstream changes yet.

**Rate limits.** 300 QPM against three locations polled a few times a day is not a real constraint. Handle
`429` and `5xx` with bounded retry and give up quietly to the previous snapshot; never spin.

**Offline.** Join failures into `OfflineState` so the screen and `app.log` cannot disagree — `OfflineState.AnyOffline`
already exists and `OfflineAdviceOnScreenTests` guards every site that tells the owner to reconnect.

**Verification.** Run against the live account after P1; the traversal reaches the last page and the count
equals `totalReviewCount` for all three locations. Compare the rating against the current scrape's value.

**Test that pins it.** Reader tests over recorded JSON: multi-page traversal terminates; a missing
`nextPageToken` sets `ReachedLastPage = true`; a count/`totalReviewCount` mismatch sets it **false**; a
`429` returns the prior snapshot rather than an empty one. Recorded fixtures, following
`GoogleReviewScrapeTests`.

**What could break.** The scrape must remain the fallback for an owner who has not connected, so both readers
ship. Do not delete the scraper in this increment.

### Increment 109 · v4.99.76 · Review Desk onto the API · **S2** · effort **M**

**Problem.** The desk's coverage vocabulary, sample caveats and second rating scrape all exist to work around
limits the API removes.

**Why it matters.** Coverage goes from *"covers the first 50 of 1,671"* to *"covers all 1,671 reviews"*; the
reply queue stops being a sample, so *oldest waiting* and *how many at ≤3 stars* become facts about the
profile rather than about eight loaded rows.

**The correction.**

1. Route `ReviewQueue` and `ReviewCoverage` off the API reader when a token exists.
2. `QueueIsSample` becomes false — every review carries its `comment` inline, so no DOM expansion per
   preview. Remove the sample suffixes **only on the API path**.
3. Rating and lifetime total come from the same response body. **Retire `ScrapeRatingAsync` on the API
   path**, along with `RatingRefreshInterval`'s 6-hour throttle
   ([`:456`](../UnifiedMessenger/Services/Oversight/GoogleReviewSnapshotService.cs#L456)), the two anchored
   regex layouts, and the visible navigation to the Search merchant view that yanks the owner's page away.
4. The business-wide rating **stays a weighted mean, still labelled as one** — Google publishes a rating per
   location and never one for the business. The API does not change that.

**Verification.** On screen, with all three profiles connected: coverage reads "covers all", the unanswered
count matches the queue length, and no navigation occurs during a manual Re-sync.

**Test that pins it.** `ReviewCoverage` keeps its full existing suite — the partial vocabulary must still be
reachable. Add: with a token and a complete traversal, `Describe` returns "covers all"; **with a token and a
failed traversal it returns the partial form, not "covers all"**. That second case is the one that matters.

**What could break.** Deleting the partial-coverage path. It still fires on first sync, a rate limit, an
expired token and offline. A desk that can only say "covers all" will one day say it about 50 reviews.

### Increment 110 · v4.99.77 · Real review dates — closes D6 · **S3** · effort **S**

**Problem.** Two limits the repo records as unobtainable or day-two: reply dates (D6) and trend tiles that
*"need a second day of readings before they say anything"*.

**Why it matters.** The owner installs and sees review trends immediately instead of tomorrow, and gets a
median review reply time that today reads "not available".

**The correction.**

1. `createTime` per review feeds `ReviewTrend` and `ReviewHistoryStore` directly. Velocity stops being
   derived from the lifetime total's movement. **History exists on first sync.**
2. `reviewReply.updateTime` gives a real reply-time median. **Label it "reply last updated", not "replied"** —
   it is *last modified*, and editing a reply moves it. Getting this wrong is a fresh instance of D10.
3. Close **D6** in `remaining-work.md`, and close **owner-decision §2** (*"should review reply time measure
   from installation?"*) as **no longer necessary** — real historic dates exist, so the "since 12 March"
   footnote-forever problem does not arise.

**Verification.** On a freshly connected profile the trend tiles render on day one with no second reading.
The reply-time median is non-empty and its label says "last updated".

**Test that pins it.** A label test that fails on the string "replied" in the review reply-time tile — this
is cheap and it is exactly the kind of wording defect the audit kept finding. Trend tests with a single day
of API data producing a non-empty trend.

**What could break.** `ReviewHistoryStore`'s existing one-reading-per-day-per-account shape now has a second
source of truth. Prefer `createTime` and keep the stored readings as the fallback; do not merge two
populations under one noun, which is D9's whole lesson.

---

## 4 · Track B — Instagram and Messenger

**All four increments are ⚠ ASSUMES-PASSIVE.** Gated on **P3**. If P3 fails, go to §6 instead.

### Increment 111 · v4.99.78 · One capability set per adapter, not per platform · **S2** · effort **S**

**Problem.** `PlatformCapabilities` splits flags into *platform facts, which do not change when we write a
better adapter* and *adapter capabilities*. `RequiresThreadOpenToRead` is filed as a platform fact and is
not one — it is true of Meta's **web client** and false of Meta's **API**, on the same platform.

**Why it matters.** Without this, flipping Instagram's flags means editing a constant whose own comment says
it was *"set before any Meta adapter exists precisely so it constrains whoever writes one"*
([`PlatformDefinition.cs:63`](../UnifiedMessenger/Models/PlatformDefinition.cs#L63)). Editing that constant
in place would erase the prohibition for the scrape path too — the path where it is still completely true.

**The correction.** Make the capability set a function of the resolved adapter, not of the platform id.
`PlatformAdapterInternals.ResolveEnabledAdapter` already switches on platform to pick an adapter; capabilities
should follow that same resolution. `MetaAggregateOnly` stays exactly as written and remains what an
Instagram account **without** a connected API credential reports. Amend the taxonomy comment in
`PlatformCapabilities` to name this third case, so the next person does not re-derive it.

**Verification.** With no Google/Meta credential, every capability answer is byte-identical to today.

**Test that pins it.** Capability resolution returns `MetaAggregateOnly` for an unconnected Instagram account
and the API set for a connected one; **the scrape path can never report `RequiresThreadOpenToRead = false`**.

**What could break.** Anything caching capabilities per platform id for the process lifetime. Capabilities
now change when a credential is connected or disconnected, at runtime.

### Increment 112 · v4.99.79 · The Meta Conversations reader and adapter · **S3** · effort **L**

**Problem.** Instagram and Messenger contribute nothing.

**Why it matters.** The owner's customers message on both. Today those queues are invisible to oversight.

**The correction.** Follow `AGENTS.md` § *Platform adapter pattern*, minus the JS — there is no DOM here.
`GET /PAGE-ID/conversations?platform=instagram|messenger` for threads, `GET /CONVERSATION-ID?fields=messages`
for the messages, mapped to `ThreadData` through `ChatEntryParser` so both producers stay in step. Add a case
in `PlatformAdapterInternals.ResolveEnabledAdapter`. **Do not touch `UsesWhatsAppIndexedDbPipeline`** — its
own comment is the rule, and a new channel earns metrics by declaring capabilities, never by joining that
pipeline.

Flip capability flags **one at a time, each behind a read that works**: `CanReadUnread` first (it alone gates
command-centre inclusion via `ContributesConversationMetrics`), then `CanReadPreview`, `CanReadTimestamps`,
`CanReadContactIdentity`, and `SupportsFrt` **last** — only once a live read confirms the message list goes
deep enough to find the inbound message that opened a thread. *"Never set one optimistically: a true flag is
a promise that a consumer can read that field today."*

**Verification.** With `CanReadUnread` alone, an Instagram account appears in the command centre with a
correct awaiting count and is absent from on-time % — both automatic, via
[`CommandCenterPanel.xaml.cs:379`](../UnifiedMessenger/Controls/CommandCenterPanel.xaml.cs#L379) and
[`OversightRollupBuilder.cs:116-118`](../UnifiedMessenger/Services/Oversight/OversightRollupBuilder.cs#L116).
Confirm the count against Instagram on the owner's phone.

**Test that pins it.** Reader tests over recorded JSON; a rollup test that a `SupportsFrt = false` account
contributes to `AwaitingCount` and to neither side of the on-time ratio. That behaviour exists and is
correct — pin it before relying on it.

**What could break.** Token expiry mid-scan leaving accounts in `Error` with no retry — F-OFFLINE-07's shape,
still open. Degrade to the last snapshot and say the data is stale, never show zero.

### Increment 113 · v4.99.80 · Channel affordances on the dashboard · **S3** · effort **S**

**Problem.** Once rows come from three places, the owner cannot tell which app to open to reply.

**The correction.** Per `spec §3`: a channel badge on each awaiting row using the platform glyph and
`AccentColor` (both already declared per platform); a channel filter chip row built with the existing
`BuildFilterChip` helper (`CommandCenterPanel.xaml.cs:975-1004`), **hidden when only one channel
contributes**; and the location card's subtitle naming the channels it rolls up.

**Verification.** On screen with WhatsApp and Instagram accounts in one location.

**Test that pins it.** The chip row is absent with one contributing channel and present with two —
one filter with one option is furniture, the rule the sidebar's scope switch already follows.

**What could break.** `DesignScaleTests` reads `.cs` as well as `.xaml`, so the badge's font and icon sizes
must be on the scale. And any `FontIcon` here must use a `"\uXXXX"` escape, never an inline character — all
eight blank glyphs found in v4.99.65 were inline.

### Increment 114 · v4.99.81 · Unhide Instagram; descriptions derived from capabilities · **S2** · effort **XS**

**Problem.** Instagram is in `HiddenFromPicker`
([`PlatformModuleSettingsHelper.cs:53`](../UnifiedMessenger/Services/PlatformModuleSettingsHelper.cs#L53))
and its `Description` says *"No oversight metrics"* — correct today, false the moment 112 lands.

**Why it matters.** `PlatformDescriptionTests` enforces one direction only: it fails on roadmap words and
requires unmeasured channels to say "No oversight metrics". **Nothing fails when a measured channel keeps
claiming it is unmeasured.** That is a customer-visible false statement in the Add-account picker with no
guard on it.

**The correction.** Remove `instagram` from `HiddenFromPicker`; rewrite both descriptions to what the channel
does; and **derive the assertion from `Capabilities`** so the two cannot drift again — a channel with
`ContributesConversationMetrics` must not say "No oversight metrics", and one without must.

**Verification.** Open Add account; both entries describe what they do.

**Test that pins it.** The bidirectional `PlatformDescriptionTests` above. It should fail if run against the
tree *before* the description edit — write it first and watch it go red.

**What could break.** `metabusinesssuite` stays hidden: it has no API of its own and is redundant once
Conversations lands (`free-api-matrix.md` §3).

---

## 5 · Close-out

### Increment 115 · v4.99.82 · Documentation sync · **S4** · effort **S**

Not optional bookkeeping — three of these documents are load-bearing for the next agent session.

| File | Change |
|---|---|
| `docs/egress-inventory.md` | New §1 rows: `oauth2.googleapis.com` (POST), `mybusiness.googleapis.com` (GET), `graph.facebook.com` (GET). Correct the claim that the app contains no `PostAsync`. Re-run each row's stated command rather than editing prose |
| `AGENTS.md` | Gotchas: no `.NET` client exists for the reviews API · `Google.Apis.MyBusinessQA.v1` is a live NuGet for a **dead** API · `RequiresThreadOpenToRead` is per-adapter, not per-platform · Google reviews needs `business.manage`, which can write, and a test forbids it |
| `docs/remaining-work.md` | **D5 closed** (109) · **D6 closed** (110) · owner-decision §2 closed as unnecessary · Phase 5 updated: Instagram/Messenger no longer gated on live-account DOM tuning |
| `CHANGELOG.md` | One section per shipped version, owner-facing |
| `README.md` | Current release line only |
| Version sync | `.csproj`, `app.manifest`, `installer-shared.iss`, `README.md`, `CHANGELOG.md` — the five files, in lockstep, every increment |

---

## 6 · ⚠ The fallback branch — if P3 shows the API marks threads seen

**Stop Track B at increment 111.** Do not build 112–114 as specified.

`spec §4` writes the alternative out in full. In roadmap terms it collapses to **one increment**:

> **Increment 112-F · Instagram and Messenger as aggregate-only counts · S3 · effort M.**
> Capabilities: `IsMessageChannel` and `CanReadUnread` true, everything else false. The channels contribute
> one number per account — how many customers are waiting — rendered as a **card**, never as rows in the
> Needs-reply list. The card says *"Instagram shows counts only — open Instagram to see who."*
> `SupportsFrt` stays false, so they are already excluded from on-time %.
>
> **This requires building the `IsAggregateOnly` rendering path, which has never existed.** The flag is
> declared and documented in `PlatformCapabilities` — *"callers should render a count and explicitly say
> detail is unavailable, rather than showing an empty list"* — and `grep` finds no consumer outside its own
> file. It becomes load-bearing overnight.
>
> Increment 113 shrinks to the location-card subtitle only; there are no rows to badge and no queue to
> filter. Increment 114 is unchanged in shape but the descriptions must say *counts only*.

**Do not build the aggregate-only path speculatively before P3 answers.** It is a real screen with real
copy, and building it against a 10-minute question that has not been asked is the thing this project is
careful not to do.

---

## 7 · Cross-cutting decisions

| # | Decision | Status |
|---|---|---|
| C1 | **Token storage** — DPAPI `CurrentUser`, no new dependency, not protection against code running as the owner | **Recommended, needs one word from the owner.** Their threat model has never been stated |
| C2 | **Token refresh** — `Google.Apis.Auth` handles it; the 7-day expiry is a *publishing-status* problem solved by P2, not a code problem | Settled |
| C3 | **Rate limits** — 300 QPM vs three locations a few times a day. Bounded retry on 429/5xx, fall back to the previous snapshot, never spin | Settled |
| C4 | **Offline** — every new failure joins `OfflineState`, so the screen and `app.log` answer "are we online" identically. `OfflineAdviceOnScreenTests` already guards this | Settled |
| C5 | **Version numbering** — v4.99.x has been running since long before this work. Increment 109 (complete review coverage) or 113 (a second measured channel) is a defensible **v5.0.0** | **Owner's call.** This roadmap assumes v4.99.73–82 throughout; renumbering is a find-and-replace across five files |
| C6 | **Analytics option A or B** (`spec §5`) — A (scope honestly) is assumed. B (a second data path so Analytics covers every channel) is an L and is not on this roadmap | **Deliberate.** Raise it only if the owner wants cross-channel volume history, not because the page looks incomplete |

---

## 8 · What this roadmap deliberately does not do

- **Nothing that sends.** `reviews.updateReply` and the Meta Send API are reachable with the credentials
  increment 107 acquires and are never called. Increment 107 ships the test that keeps it that way.
- **Nothing to WhatsApp.** No API route exists that does not require deleting the number from the app the
  staff reply in (`free-api-matrix.md` §5.3). The store bridge, IndexedDB fallback, preview harvest and
  backfill are untouched.
- **No Telegram, no Discord.** Telegram was selected as a route but not listed as a channel customers use;
  it stays out until that changes, at which point it is a small, well-understood piece of work.
- **No Google Q&A.** Its API was discontinued 2025-11-03.
- **No Tier-1 ONNX.** Free and unblocked, and still without an answer to *what does it decide that the
  existing heuristic and Ollama do not already decide correctly* (`free-api-matrix.md` §4.7).
- **No crash reporting, ever.**
- **No widening of `UsesWhatsAppIndexedDbPipeline`.**
- **No item that says "investigate."** The two open questions are P1–P4 prerequisites with named artifacts
  and pass/fail outcomes, not research tasks smuggled into a build plan.
