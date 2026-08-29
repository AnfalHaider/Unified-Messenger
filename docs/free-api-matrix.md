# Free-API research — verdict matrix

**As of:** 2026-08-29 · **Baseline:** v4.99.72 · **Phase 1 of 3, complete.**

> **§7 was answered on 2026-08-29 — the decisions are recorded in [§10](#10--owner-decisions--2026-08-29)
> and they, not §7's recommendations, are what Phases 2 and 3 are built against.** Read §10 before §7.
> Scope is now: **read-only oversight**, via the **Google Business Profile API** and the **Meta
> Conversations API** for **Instagram and Messenger**. The Google account is a personal `@gmail.com`, which
> closes the cheap OAuth path. §9 is the open-source library survey the owner asked for on top.

Written after the owner relaxed one hard constraint on 2026-08-29: *"We can use free APIs that are free
to use, and don't risk account ban."* Every other constraint in [`AGENTS.md`](../AGENTS.md) stands.

Every claim is labelled **CONFIRMED** (I read the vendor's own page and quote it), **LIKELY** (secondary
or partner documentation, untested), or **UNKNOWN** (with the artifact that would settle it named). No
route below was run against a live account — this is a documentary result, not a measured one, and §6
says so explicitly.

---

## 1 · The headline

Three findings, in order of how much they change the product:

1. **Google Business Profile API is the win, and it is bigger than expected.** It does not just supply the
   rating and lifetime total the app currently scrapes off the Search merchant view — it closes **D5** and
   **D6**, both of which this repo currently records as unobtainable, and it makes owner-decision §2 moot.
   The cost is an OAuth story with a real gate in it (§5.1).
2. **Instagram and Messenger are reachable without a webhook.** The blocker assumed in the session prompt —
   "webhook-driven, and a webhook needs a public inbound URL, which a local desktop app does not have" — is
   **not true for reading**. Meta's Conversations API is GET-pollable for both platforms (§5.5). That is
   exactly the shape the oversight engine consumes.
3. **WhatsApp — the product's core channel — gains nothing, and the obvious route would destroy the
   business.** Registering the owner's number on WhatsApp Cloud API requires deleting it from the WhatsApp
   Business app the staff actually reply in. This is the recommendation the prompt warned about, and the
   answer is a flat no (§5.3).

The relaxation is worth acting on. It is worth acting on for **Google first**, and it does nothing for the
channel that carries most of the product's value.

---

## 2 · The privacy test, applied

The test is not "does this touch the network". It is: **does this send the vendor something they do not
already have, or send anything to a third party?**

| Route | Sends the vendor anything new? |
|---|---|
| Google Business Profile API | **No.** Reading the owner's own reviews back off Google's servers. The reviews are Google's data about the owner's business; authenticating and listing them is the owner's own request. |
| Meta Conversations API (IG / Messenger) | **No.** The DMs are already on Meta's servers. Reading them back adds nothing Meta does not hold. |
| Telegram Bot API | **No** for messages already in Telegram. **But** a bot is a *third party the owner creates* — the token is theirs, the traffic is theirs, so this passes only because owner and bot operator are the same person. Note the distinction; it would fail for a hosted bot. |
| Discord bot | Same as Telegram. |
| WhatsApp Cloud API **via a BSP/partner** | **Yes — fails.** A Business Solution Provider is a third party that sees every message. Independent of cost, this is out. |
| Any route that would upload derived metrics | **Prohibited, unchanged.** On-time %, awaiting counts, FRT samples and AI prompts must not leave the box, including back to the vendor the raw data came from. Nothing in §5 proposes this. |

One structural rule for Phase 3, stated now so it cannot be lost: an API-backed channel must be **read-only
into the oversight store**. No derived figure is ever a request body.

---

## 3 · Verdict matrix — channels

| Route | Channel | What it exposes | Free at 2× our volume? | Auth / approval | Ban risk | New data to vendor? | Verdict |
|---|---|---|---|---|---|---|---|
| **Business Profile API v4 `accounts.locations.reviews`** | Google Business | Reviews, `starRating`, `comment`, `createTime`, `updateTime`, **`reviewReply.updateTime`**, plus `averageRating` + `totalReviewCount` per page | **Yes.** 300 QPM on approval; 3 locations × a few polls/day is ~4 orders of magnitude under. No pricing page exists. | OAuth 2.0, scope `business.manage` (sensitive) + a manual Google access request that starts at **0 QPM** | **None** — official, read-only scopes | No | **VIABLE IF** — the owner accepts either a Workspace-account OAuth setup or a 3–5 day sensitive-scope verification (§5.1) |
| **My Business Q&A API** | Google Business | Questions + answers | — | — | — | — | **WONTFIX — dead.** Discontinued **2025-11-03**. Not a constraint problem; the API no longer exists (§5.2) |
| **Google Business Messages** | Google Business | — | — | — | — | — | **WONTFIX — permanently dead**, as `AGENTS.md` already records. Confirmed, closed, do not revisit |
| **WhatsApp Cloud API** | WhatsApp / WhatsApp Business | Full messaging, webhooks | Moot | Requires the number be **deleted from WhatsApp first** | Not ban — **worse**: it removes the number from the app the staff use | No (direct) / **Yes** (via BSP) | **WONTFIX-BY-CONSTRAINT** — business-breaking, and the only non-breaking variant needs a paid third party. Two constraints, not one (§5.3) |
| **Telegram Bot API — plain bot** | Telegram | Only messages sent **to the bot** | Yes | BotFather token; `getUpdates` long-poll, no public URL | None — official | No | **VIABLE**, but it does not cover the owner's existing customer chats, so it answers nothing (§5.4) |
| **Telegram Bot API — Business Mode** | Telegram | The owner's own private customer chats, via `business_connection` / `business_message` | Yes for the API — **but requires Telegram Premium on the owner's account** | BotFather Business Mode + owner connects the bot in Telegram settings | None — official, this is the sanctioned mechanism | No | **VIABLE IF** the owner accepts a Telegram Premium subscription (recurring cost). Also see the rung-1 question in §5.4 |
| **Discord bot — server channels** | Discord | Messages in servers the bot is invited to; Gateway is an **outbound** WebSocket, no public URL | Yes | Bot token, per-server invite | None — official | No | **VIABLE** — and probably answers nothing this business has (§5.6) |
| **Discord — the owner's own DMs** | Discord | Would need a user token (self-bot) | — | — | **Account termination.** Discord's own support article forbids it | — | **WONTFIX-BY-CONSTRAINT** — the ban-risk clause the owner explicitly reinforced |
| **Meta Conversations API (GET polling)** | Instagram | `/PAGE-ID/conversations?platform=instagram`, thread `updated_time`, messages with `from` / `created_time` | Yes | `instagram_basic`, `instagram_manage_messages`, `pages_manage_metadata`; **App Review + Business Verification** for Advanced Access | **None** — official Graph API, no unofficial library involved | No | **VIABLE IF** — see the single highest-leverage unknown in §5.5 |
| **Meta Conversations API (GET polling)** | Messenger | `/PAGE-ID/conversations?platform=messenger`, same fields | Yes | `pages_manage_metadata`, `pages_read_engagement`, `pages_messaging` + App Review | None | No | **VIABLE IF** — same gate as Instagram, same submission |
| **Meta Business Suite** | Aggregator | — | — | — | — | — | **Not a route.** It has no API of its own; it is a UI over the same Graph API as the row above. Redundant if Conversations lands. Its scrape-stability vs `instagram.com` is **UNKNOWN** and not worth measuring unless Conversations fails |
| **Custom URL / generic** | generic | — | — | — | — | — | **N/A** — no vendor, no API. Stays an embed, correctly |

---

## 4 · Verdict matrix — non-channel capabilities

| Capability | Route | Free? | Verdict |
|---|---|---|---|
| **Tier-1 ONNX classifier** | `Microsoft.ML.OnnxRuntime` (MIT) + a small Apache-2.0 model (`all-MiniLM-L6-v2` ≈ 90 MB FP32 / ≈ 23 MB INT8) | Yes — no API, fully on-device, no constraint touched | **VIABLE, and I would still not build it.** Nothing here was blocked by cost; it was blocked by nobody choosing a model. Before choosing one, name the decision it makes that the existing heuristic + Ollama do not (§5.7) |
| **Code-signing the installer** | Azure Artifact Signing (ex-Trusted Signing), **$9.99/month** | **No — recurring cost** | **WONTFIX-BY-CONSTRAINT** (no recurring cost) unless the owner accepts ≈ $120/yr. It is the only thing that removes the SmartScreen warning. Owner decision, §7 |
| **Code-signing the installer** | SignPath Foundation, free for open source | Free, but requires the repo be **public under a recognised OSS licence**, and signs as *"SignPath Foundation"*, not the business | **WONTFIX-BY-CONSTRAINT** — this repo is private and the publisher name would be wrong. A second constraint, not the relaxed one |
| **Auto-update** | GitHub Releases, already shipping | Yes, unmetered for public release assets | **VIABLE — already adopted, no action.** Confirmed by the fact it ships (`GitHubUpdateService`, 5 GET call sites, `egress-inventory.md` row 3) |
| **Crash / error reporting** | — | — | **WONTFIX-BY-CONSTRAINT, permanently.** Any telemetry violates the zero-egress rule outright. Recorded here so it is not raised again |
| **Google Q&A** | See §3 | — | **Dead API.** No longer covered by the Business Profile APIs |

---

## 5 · Route detail and evidence

### 5.1 · Google Business Profile API — the highest-value route

**What it gives that the app does not have.**

| Field | Today | With the API |
|---|---|---|
| Rating, lifetime total | Scraped off the **Search merchant view** via an `aria-label` and two anchored regexes, because the reviews manager page does not carry them; throttled to 6h because it costs a visible navigation that yanks the owner's page away | `averageRating` and `totalReviewCount` are **in the list response body**, per page. No navigation, no regex, no 6-hour throttle, no two-layout problem. CONFIRMED |
| **D5** — reviews read 50 at a time out of ~1,671, `MaxPages = 1` because DOM traversal over-counted 2–3× | Open, deliberately. Coverage is disclosed on screen | `pageSize` max 50 is the **page size, not the ceiling** — `nextPageToken` is a server cursor, which cannot double-count the way scroll-and-scrape does. And `totalReviewCount` gives the traversal a **checksum**: the app can assert it read what Google says exists instead of guessing. CONFIRMED |
| **D6** — "Google publishes no reply dates anywhere the scrape can reach", recorded as **unobtainable** | The tile says so rather than estimating | `reviewReply.updateTime` — *"Output only. The timestamp for when the reply was last modified."* CONFIRMED. **This closes D6** and makes owner-decision §2 ("measure review reply time from installation?") unnecessary — real dates exist for historic replies, not only future ones |
| Three locations | Three separate scrapes | `accounts.locations:batchGetReviews` — one request across locations. CONFIRMED |

> **One honest caveat on D6.** `reviewReply.updateTime` is *last modified*, not *first replied*. Editing a
> reply moves it. For an unedited reply the two are the same, and for reply-time purposes that is almost
> always the case — but the tile must say "reply last updated", not "replied", or the app repeats exactly
> the class of defect the v4.99.46 audit spent its time on.

**Cost.** Free. There is no pricing page for these APIs, and quota on approval is 300 QPM. Three locations
polled a few times a day is roughly four orders of magnitude under that — at 2× volume, or 200×. CONFIRMED.

**The gate — this is the whole "IF".** Two separate approvals, and they are often conflated:

1. **API access.** Default quota is **0 QPM**: *"If your quota is 0 QPM (Queries Per Minute), your project
   has not yet been approved."* Requires a GBP verified and active 60+ days, a website representing the
   business, and a request form submitted from an email that is an owner/manager on the profile. All three
   are satisfiable here. Timeline is not stated by Google; a developer-forum thread reports "10+ business
   days" pending. CONFIRMED for the requirements, LIKELY for the timeline.
2. **OAuth.** `business.manage` is a **sensitive** scope. Google's exception list includes *personal use* —
   but taking that exception means staying in **Testing** publishing status, and *"a publishing status of
   'Testing' is issued a refresh token expiring in 7 days"*. CONFIRMED, from Google's own OAuth page.
   **Consequence: the owner would re-authorise every week, forever.** That is not shippable.

   Two escapes:
   - **(a) Workspace account.** If the Google account that owns the three Business Profiles is a Google
     Workspace account on a custom domain, the OAuth client can use user type **Internal** — no
     verification, no 7-day expiry, one consent. By far the cleanest outcome. Whether it applies is
     **UNKNOWN**; artifact: whether that account is Workspace or a personal `@gmail.com`. §7 asks.
   - **(b) Sensitive-scope verification.** Publicly accessible homepage, a privacy policy hosted **on the
     same domain** and linked from the consent screen, domain ownership proved in Search Console, branding
     published, and a demo video showing the scope in use. *"typically takes 3-5 business days"*. No fee
     documented. CONFIRMED. The business already needs a website for approval (1), so this is achievable —
     but the privacy policy is a real artifact somebody has to write and host.

**Ban risk: none.** Official API, read-only scopes, no unofficial library.

**Token storage — flagged, not decided.** The obvious candidate is
`System.Security.Cryptography.ProtectedData` (DPAPI, `CurrentUser` scope) alongside the existing stores: it
keeps the secret unreadable by another Windows user on the same box and adds no dependency. It is **not**
protection against anything already running as the owner. That is a Phase-3 decision with the owner's
threat model attached, not a Phase-1 conclusion.

### 5.2 · Google Q&A — the API was shut down

*"The My Business Q&A API was discontinued on November 3, 2025."* Support ended 2025-09-15. No replacement
resource is listed. CONFIRMED against Google's own sunset-dates page.

**Trap recorded so nobody re-derives it:** the Q&A REST reference page still resolves and still documents
`v1.locations.questions.list` and `questions.answers.upsert` as though live. It is a stale page. The sunset
table is authoritative. Reviews are **not** on that table and remain supported.

### 5.3 · WhatsApp Cloud API — the recommendation that would destroy the product

Meta's own phone-number documentation, quoted:

- *"Numbers already in use with WhatsApp cannot be registered unless they are deleted first."*
- *"Registered numbers can still be used for everyday purposes, such as calling and text messages, but
  cannot be used with WhatsApp Messenger."*

CONFIRMED. Partner documentation adds that existing message history is lost and the number cannot return to
the WhatsApp Business app unless deregistered from Cloud API — LIKELY (respond.io / Meta partner docs, not
Meta's own page).

**What that means for this business.** Staff at three branches reply to customers in the WhatsApp Business
app. Migrating the number to Cloud API removes the app they work in and deletes the history. The app would
gain a clean API and the business would lose its inbox. There is no version of that trade worth making, and
no amount of free tier changes it.

The one non-breaking variant — onboarding through a BSP that supports business-app-number onboarding, which
preserves concurrent use — introduces a paid third party that sees every message. That fails **no recurring
cost** and the §2 privacy test independently.

Pricing is therefore moot and I did not model it.

**WhatsApp stays WebView2 scraping.** The store bridge, the IndexedDB fallback and the sidebar preview
harvest remain the only route, and everything `AGENTS.md` records about them stands.

### 5.4 · Telegram — clean, official, and probably answering a question nobody asked

`getUpdates` long polling is an alternative to webhooks, and the two are mutually exclusive: *"There are two
mutually exclusive ways of receiving updates for your bot — the getUpdates method on one hand and webhooks
on the other."* Updates are held server-side for 24 hours. LIKELY (Telegram's documentation, reached through
secondary quotation — `core.telegram.org` reset the connection on three direct fetch attempts; artifact to
confirm: one live `getUpdates` call, which needs only a BotFather token and five minutes).

**A plain bot only sees messages sent to the bot.** It cannot see the owner's existing customer chats, so it
does not answer the oversight question at all.

**Business Mode does.** `business_connection` / `business_message` updates give a bot the owner's private
chats with customers, and it is the sanctioned mechanism — BotFather → Bot Settings → Business Mode, then
the owner connects the bot under Telegram Business → Chatbots. Zero ban risk by construction. The catch:
Telegram Business is a **Telegram Premium** feature on the connecting account. Sources disagree on whether
connected bots specifically require Premium; treat the requirement as **LIKELY** and the cost as real.

**Rung-1 question.** Telegram is `HiddenFromPicker` today and there is no evidence in this repo that the
business has a Telegram presence at all. A clean, free, well-documented API for a channel with no customers
on it is worth nothing. §7 asks before any of this is designed.

### 5.5 · Instagram + Messenger — the assumed blocker is not real

The session prompt's stated blocker was webhooks. **For reading, it does not apply.** Meta's Conversations
API documentation, quoted:

- *"To get a list of conversations, send a `GET` request to the `/PAGE-ID/conversations` endpoint"*
- *"To get a list of messages in a conversations, send a `GET` request to the `/CONVERSATION-ID` endpoint"*
- *"...include the `platform` parameter set to `instagram` or `messenger`"*
- `updated_time` — *"the most recent time a message was sent"*

CONFIRMED. Fields available include `updated_time`, `created_time`, `messages`, and `from`/`to`. That is
thread identity, last-message time and direction — precisely what `OversightRollupBuilder` consumes to
answer "who is waiting and how long", with no DOM, no scraper and no ban surface.

Permissions: Instagram `instagram_basic`, `instagram_manage_messages`, `pages_manage_metadata`; Messenger
`pages_manage_metadata`, `pages_read_engagement`, `pages_messaging`. CONFIRMED.

**The gate, and the one experiment worth running.** Advanced Access needs Meta App Review plus Business
Verification — free, but reported at 2–4 weeks with revisions restarting the clock (LIKELY, secondary).
**But** development mode allows up to 25 test users without App Review — and this app has exactly **one**
user, who is also the app's developer and the account owner.

> **UNKNOWN — highest-leverage item in this document.** Can the owner add their own Instagram professional
> account as a tester on their own Meta app and call `/me/conversations` under Standard Access, skipping App
> Review entirely? If yes, Instagram and Messenger become the cheapest new channels in the product. If no,
> they cost a multi-week review with a real rejection rate.
> **Artifact that settles it:** create a Meta app in development mode, add the owner's IG professional
> account as a tester, call `GET /PAGE-ID/conversations?platform=instagram`. Roughly one hour, no code, no
> commitment. Run this before believing any Phase-3 estimate for these channels.

Two further **UNKNOWN**s, both facts about the owner rather than the platform: whether the Instagram account
is a Professional account, and whether it is linked to a Facebook Page. Both are prerequisites, and both are
settings the owner can read off their own phone.

### 5.6 · Discord — a clean split

A **bot** in a server is free, official, and connects over an outbound Gateway WebSocket, so no public URL is
needed. Viable. Reading the **owner's own DMs** would require driving a user account, which Discord's own
support article forbids and enforces with account termination — the exact ban risk the owner named. WONTFIX.

Same rung-1 question as Telegram: Discord is embed-only today and there is no sign this business has a
Discord server. §7 asks.

### 5.7 · Tier-1 ONNX — free, unblocked, and still probably not worth it

Nothing here was ever gated by cost or by the no-API rule. `Microsoft.ML.OnnxRuntime` is MIT and already a
free NuGet; suitable small models exist under Apache-2.0 (`all-MiniLM-L6-v2`, ≈ 90 MB FP32, ≈ 23 MB INT8 —
LIKELY, secondary sources). The gate was always "nobody has chosen a model", and it still is.

The question to answer before choosing one: **what decision does the classifier make that the existing
heuristic plus Ollama do not already make correctly?** Awaiting-reply detection is already deterministic and
correct. §0.2b of `remaining-work.md` records what happened the last time a model was put in front of
something the app already computed right. Adding ~23 MB to the installer to re-decide a question the app
answers deterministically would be the same mistake in a different coat.

Recommendation: **leave gated**, and do not treat the relaxation as having unblocked it, because it never
blocked it.

---

## 6 · What this does *not* demonstrate

In the spirit of `egress-inventory.md`, the limits of this result, stated rather than left to be discovered:

- **Nothing here was run.** Every row is documentary. No OAuth flow was completed, no token obtained, no
  endpoint called. "The API exists" is not "the API is reachable by this app", and this document establishes
  only the first.
- **No live account was used**, so no rate limit, no pagination behaviour and no field nullability was
  observed. The Google review pagination claim in particular — that a server cursor cannot over-count the
  way the DOM traversal did — is sound reasoning about cursors, not a measurement of Google's cursor.
- **Approval timelines are all LIKELY or unstated.** Google publishes none for API access; Meta's is
  secondary. Neither belongs in a Phase-3 estimate as a fixed number.
- **Two Telegram claims are secondary** because `core.telegram.org` refused three fetch attempts.
- **The owner's channel presence is unmeasured.** Whether Instagram, Telegram or Discord carry any customers
  is not in this repo, and I did not read the owner's live account store to find out.

---

## 7 · Blocking questions — Phase 2 cannot start without these

**Q1 · The interaction model.** `AGENTS.md` says *"App never auto-sends. Automation is read-only scraping
only."* An official API makes composing and sending trivial, which is exactly why it must be confirmed
rather than assumed. Three positions: read-only oversight · click-through to the real client (what WhatsApp
does today) · the app composing or sending. The third is prohibited by a constraint the owner has **not**
relaxed, and I will not design it without an explicit instruction.

> Note one asymmetry: the Google reviews route is the one place where "the app replies" is different in
> kind — `reviews.updateReply` posts a **public** reply to a customer review, not a private message. If
> sending is ever permitted anywhere, this should be the last place, not the first.

**Q2 · Which routes advance to Phase 2.** My recommendation, in order:
1. **Google Business Profile API** — take it. Largest gain, closes two items recorded as unobtainable.
2. **Meta Conversations API** — run the one-hour experiment in §5.5 *before* committing. If test-user access
   works, take it; if it needs full App Review, it becomes a much bigger decision.
3. **Telegram / Discord** — do not design until Q4 says there are customers on them.
4. **WhatsApp Cloud API** — closed. Do not reopen.

**Q3 · The Google account type.** Is the Google account that owns the three Business Profiles a **Google
Workspace** account on a custom domain, or a personal **@gmail.com**? This single fact decides whether the
Google route costs one consent click or a privacy policy, a Search Console verification and a demo video.

**Q4 · Which channels the business actually uses.** Does the business have customers messaging on Instagram?
On Telegram? On Discord? A free official API for a channel with no customers on it is worth nothing, and
building one would be the most expensive kind of waste this project can produce.

**Q5 · Code signing (independent of everything above).** The only route that removes the SmartScreen warning
costs ≈ $120/yr and therefore breaks the standing "no recurring cost" rule. That is the owner's call, not a
research finding. Free alternatives require open-sourcing the repo and would sign under someone else's name.

---

## 8 · Evidence log

Every source opened for this document.

| # | Source | Used for |
|---|---|---|
| 1 | `developers.google.com/my-business/reference/rest/v4/accounts.locations.reviews/list` | `averageRating`, `totalReviewCount`, `nextPageToken`, `pageSize` max 50 |
| 2 | `developers.google.com/my-business/reference/rest/v4/accounts.locations.reviews` | Review + ReviewReply fields, `reviewReply.updateTime` |
| 3 | `developers.google.com/my-business/content/prereqs` | Access request, 0 QPM until approved, 300 QPM on approval |
| 4 | `developers.google.com/my-business/content/limits` | Quota figures, increase policy |
| 5 | `developers.google.com/my-business/content/review-data` | Endpoint paths, `batchGetReviews`, `updateReply` |
| 6 | `developers.google.com/my-business/content/sunset-dates` | Q&A API discontinued 2025-11-03; reviews not sunset |
| 7 | `developers.google.com/identity/protocols/oauth2` | 7-day refresh token under "Testing" publishing status |
| 8 | `developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification` | Homepage, privacy policy, Search Console, demo video, 3–5 business days |
| 9 | `support.google.com/cloud/answer/13463073` (via search) | Verification exceptions incl. personal use, internal use |
| 10 | `developers.facebook.com/docs/whatsapp/cloud-api/phone-numbers` | Number cannot be used with WhatsApp Messenger; must be deleted first |
| 11 | `developers.facebook.com/docs/whatsapp/cloud-api/get-started` | Checked — carries no migration detail |
| 12 | respond.io / Meta partner migration docs (via search) | History loss, BSP concurrent-use path — LIKELY only |
| 13 | `developers.facebook.com/docs/messenger-platform/conversations/` | GET-pollable conversations, `platform` parameter, fields, permissions |
| 14 | Meta App Review / Business Verification write-ups (via search) | 2–4 week timeline, 25 test users without review — LIKELY only |
| 15 | `core.telegram.org/bots/api` (via search; direct fetch reset ×3) | getUpdates/webhook mutual exclusivity, 24h retention |
| 16 | Telegram Business / connected-bots documentation (via search) | `business_connection`, `business_message`, Premium requirement — LIKELY |
| 17 | `support.discord.com/hc/en-us/articles/115002192352` (via search) | Self-bots forbidden, account termination |
| 18 | `azure.microsoft.com/pricing/details/trusted-signing` (via search) | $9.99/month, $99.99/month tiers |
| 19 | `signpath.org/terms.html`, `signpath.io/solutions/open-source-community` (via search) | Free for OSS only; signed as SignPath Foundation |
| 20 | ONNX Runtime / MiniLM sizing write-ups (via search) | ≈ 90 MB FP32, ≈ 23 MB INT8 — LIKELY |
| 21 | `nuget.org/packages?q=Google.Apis.MyBusiness` | The nine published packages, versions and dates — **no v4/reviews package** |
| 22 | `developers.google.com/my-business/samples` | Which languages Google publishes clients for |
| 23 | `nuget.org/packages/Google.Apis.Auth` | Apache-2.0, 1.76.0 (2026-08-20), 580.7M downloads |
| 24 | `github.com/TelegramBots/Telegram.Bot` | MIT, Bot API 10.3, 2,332 commits |
| 25 | Facebook C# SDK status (via search) | Official SDK deprecated and unsupported |

---

## 9 · Addendum — open-source libraries for the chosen routes

Requested by the owner alongside the route selection. The `AGENTS.md` ban is on **unofficial protocol
libraries** (Baileys, whatsmeow) because they carry ban risk. A client wrapper around a **vendor's own
public API** is a different category and carries none — it is the same HTTP the vendor documents. Each
candidate below is assessed on licence, maintenance, and whether it earns its place at all.

| Route | Library | Licence | State | Verdict |
|---|---|---|---|---|
| Google reviews (v4) | **none exists** | — | — | **Raw `HttpClient`.** See the finding below |
| Google OAuth | `Google.Apis.Auth` | Apache-2.0 | 1.76.0, published 2026-08-20, 580.7M downloads | **Take it.** Handles the installed-app authorization-code flow and token refresh; writing that by hand is the wrong kind of lazy |
| Meta Graph (IG + Messenger) | official Facebook C# SDK | — | **Deprecated and unsupported** | **Raw `HttpClient`.** Community forks exist (`fabricatorsltd/facebook-sdk-dotnet`, `devTaras/fb-dotnet-sdk`) and none is worth a dependency for four GET calls |
| Telegram | `TelegramBots/Telegram.Bot` | MIT | Bot API 10.3, 2,332 commits, actively developed | **Would take it if Telegram were in scope.** Business updates landed in Bot API 7.2, well below 10.3, so coverage is LIKELY — unverified because Telegram is deferred |

### The finding: there is no .NET client for the reviews API

CONFIRMED. NuGet publishes **nine** `Google.Apis.MyBusiness*` / `BusinessProfile*` packages. Every one is a
**v1** API. **None of them is reviews** — reviews only ever existed on v4, and v4 was never regenerated into
a client library. Google's own samples page lists .NET among its client languages, which is true of the v1
APIs and misleading about the one endpoint this project needs.

Practically this is good news, not bad. The route is two authenticated GETs against a documented REST
endpoint — `Google.Apis.Auth` for the token, `HttpClient` for the call, `System.Text.Json` (already
referenced) for the response. No heavyweight generated client, no new transitive tree.

### Second trap, recorded so nobody loses an afternoon

**`Google.Apis.MyBusinessQA.v1` is still on NuGet, last updated 2026-05-12** — nine months *after* the API
it wraps was shut down (§5.2). A maintained package for a dead service is more convincing than a stale doc
page, and it would take a working build and a runtime 404 to discover otherwise. The sunset table is the
only authority. Do not treat a live NuGet package as evidence that an API exists.

### What this means for `egress-inventory.md`

Every adopted route adds rows to §1 of that file — new `HttpClient` call sites to `oauth2.googleapis.com`,
`mybusiness.googleapis.com` and `graph.facebook.com`. The file's whole value is that it is re-derivable
rather than asserted, so **updating it is part of the definition of done for each increment**, not a
follow-up. Note also that these will be the first non-loopback requests the app makes that carry an
`Authorization` header, and the first that are not plain `GetAsync` with a constant URL.

---

## 10 · Owner decisions — 2026-08-29

Recorded at the close of Phase 1. These fix the scope of Phases 2 and 3.

| # | Decision | Consequence |
|---|---|---|
| **1** | **Read-only oversight.** Not click-through, not composing, not sending | `reviews.updateReply` is **out of scope** even though the token can reach it. The Meta send API is out of scope. The Reviews page's existing on-device reply *drafting* is unaffected — it drafts for the owner to paste and has never sent |
| **2** | Routes: **Google Business Profile API**, **Meta Conversations API** | Plus a request to survey open-source libraries for these — §9 |
| **3** | The Google account is a **personal `@gmail.com`** | The Workspace escape (§5.1a) is **closed**. The route now requires sensitive-scope verification: public homepage, privacy policy hosted on the same domain and linked from the consent screen, Search Console domain ownership, published branding, and a demo video. 3–5 business days, no fee. The alternative is re-authorising every 7 days forever, which is not shippable |
| **4** | Customers message on **Instagram** and **Facebook Messenger**. Not Telegram, not Discord | Both are the same Meta submission, so Messenger is nearly free once Instagram is in. Discord closes entirely |

**One contradiction, flagged not resolved.** Telegram Business Mode was selected as a route in (2) but
Telegram was **not** listed as a channel customers use in (4). It is researched above and it is genuinely
viable, but building an API integration for a channel with no customers on it is the most expensive kind of
waste this project can produce. **It is excluded from the Phase 3 roadmap unless the owner says there are
customers there**, at which point it is a small, well-understood piece of work.

**One honesty note on decision 3, since read-only was chosen.** Google offers no read-only scope for
reviews — `business.manage` is the only one, and it can write. The app will therefore hold a credential
capable of posting public replies while deliberately never using it. That is worth saying out loud rather
than discovering later, and Phase 3 should pin "the app issues no write to Google" with a test, the way
`UserFacingErrorTests` and `AccountVocabularyTests` pin their rules, rather than relying on nobody adding
the call.

---

## 11 · Re-research — 2026-08-29, second pass

Two things changed after the owner ruled out the Meta API in favour of a standalone Instagram route. One of
them **corrects a claim in §5.1 that this document got wrong.**

### 11.1 · Correction — `business.manage` is probably **not** a sensitive scope

§5.1 states that `business.manage` is sensitive, and builds the entire "IF" on it: sensitive-scope
verification, or a refresh token that expires every 7 days. **That was asserted, not checked, and the
evidence now points the other way.**

Google's own OAuth scopes page lists the row as:

> `https://www.googleapis.com/auth/business.manage` | Manage your Business Profile on Google

with **no sensitive marker**, and the same page says: *"Sensitive scopes require review by Google and have a
`sensitive` indicator on the Google Cloud Console's OAuth consent screen configuration page."* Google's
verification help adds: *"if your app utilizes only non-sensitive scopes, it is not mandatory for your app
to complete the app verification process."*

**If that holds, the entire P2 prerequisite disappears** — no public homepage requirement, no privacy policy
hosted on the business domain, no Search Console verification, no demo video, no 3–5 day review. Publish the
consent screen to Production and the refresh token is long-lived. The gmail.com answer would then cost
almost nothing, rather than the multi-day task §5.1 described.

**Status: LIKELY, and the artifact that settles it is two minutes of the owner's time.** Google states the
Cloud Console is authoritative — add the scope to the consent screen and read the category badge it shows.
Until someone looks, the roadmap plans for both and leads with the cheap branch.

### 11.2 · Standalone Instagram exists, and it is a better route than the one §5.5 proposed

The owner ruled out "the Meta API" and asked for standalone Instagram. There is a real, official setup for
exactly this: **Instagram API with Instagram Login**.

| | Meta Conversations API via a Page (§5.5, superseded) | **Instagram API with Instagram Login** |
|---|---|---|
| Facebook Page required | Yes | **No** — *"This API setup does not require a Facebook Page to be linked to the Instagram professional account."* CONFIRMED |
| Host | `graph.facebook.com` | `graph.instagram.com` |
| Endpoints | `/PAGE-ID/conversations`, `/CONVERSATION-ID` | `/me/conversations`, `/<CONVERSATION_ID>` — GET-pollable, same shape |
| Permission | `instagram_basic` + `instagram_manage_messages` + `pages_manage_metadata` | **`instagram_business_manage_messages`** (with `instagram_business_basic`) |
| Covers Messenger too | Yes | **No** |

**What "standalone" does and does not mean — stated plainly so it is not over-read.** It removes the
Facebook Page, the Page linkage, the Page permissions and the Meta Business Suite from the picture. It does
**not** make the route non-Meta: `graph.instagram.com` is Meta infrastructure and the app is still registered
on `developers.facebook.com`. **There is no non-Meta route to Instagram DMs.** The only alternatives are
unofficial libraries, which are banned for ban risk and which the owner reinforced on 2026-08-29. If
"standalone" was meant as "nothing to do with Meta at all", the honest answer is that Instagram cannot be
done at all, and the channel should be dropped rather than faked.

**Messenger falls out of scope as a consequence.** Facebook Messenger is a Facebook Page product; its
conversations are Page-owned and there is no Instagram-Login-style standalone route to them. Keeping
Messenger means keeping the Page-based setup the owner just declined. The owner listed Messenger as a
channel customers use, so **this is a real loss and it is theirs to accept or reverse**, not mine to resolve.

### 11.3 · Three operational facts the first pass missed

All CONFIRMED or LIKELY from Meta's own documentation, and all three change the design rather than just the
estimate:

1. **Message history is capped at 20 per conversation.** *"you can only get details about the 20 most recent
   messages in the conversation. If you query a message that is older than the last 20, you will see an error
   that the message has been deleted."* Harmless for *who is waiting* (that needs the last message) and
   harmless for forward-tracked FRT (`ResponseTimeTracker` already measures forward from a watch start).
   **Fatal for historical backfill** — there is no Instagram equivalent of the WhatsApp history import, and
   the UI must not imply one. Inactive Request-folder threads older than 30 days are also absent.
2. **The access token expires in 60 days and must be refreshed while alive.** A long-lived token can be
   refreshed once it is 24 hours old and before it expires; *"tokens not refreshed within 60 days will expire
   and can no longer be refreshed."* For a desktop app this is a real failure mode: **leave the app closed
   for 60 days and the Instagram connection dies permanently and needs manual re-authorisation.** Google's
   refresh token has no such rule. This needs a refresh on startup and an honest disconnected state, not a
   silent zero.
3. **Rate limit is ~200 calls per user per hour** (Business Use Case). Polling the conversation list every
   minute is 60 calls/hour before a single message read. **Fetching messages for every thread on every poll
   would breach it.** The design must use the conversation list's `updated_time` to fetch messages only for
   threads that actually changed — which is also just less work.
