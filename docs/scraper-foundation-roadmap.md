# Scraper foundation — roadmap

**Written:** 2026-09-02 · **Baseline:** v4.99.74 · after `2ad78e4` (Phase 1, three channels).

Covers the four phases of the scraper-foundation stream: inventory → manifest + resilience →
navigation mapping → unified inbox. Companion to [scraper-inventory/](scraper-inventory/), which holds
the evidence. The live product backlog stays [remaining-work.md §0](remaining-work.md); this file is
one stream inside it.

## Ordering rule

**Track A runs first and needs nothing from the owner. Track B is batched at the end.**

That is deliberate. Owner time is the scarcest input here — a second device, a login, a customer-safe
test thread — so it is spent once, in one sitting, on the things that genuinely cannot be done without
it, rather than dribbled across the stream as a series of blocks.

The cost of this ordering is stated plainly in §Track B: three Track-A items are *shaped* by answers
only Track B can give, and are sequenced after it for that reason.

### The phase gate, and why Track A does not break it

The brief says: do not start a phase before the one before it is complete and its findings are on disk.
Phase 1 is **not** complete — Meta Business Suite and Instagram have no session.

Track A does not breach that, because Phase 2's own instruction is to prove the manifest by migrating
the **existing WhatsApp scraper** first, and the WhatsApp inventory is done and on disk. The manifest
work is gated on the WhatsApp findings, which exist. Meta's manifest entries wait for Meta's inventory.
No channel gets a manifest before its own inventory lands.

---

## Track A — no owner involvement

| # | Increment | What lands | Gated on |
|---|---|---|---|
| ~~**A1**~~ | ~~108~~ | ✅ **Done 2026-09-02.** Google Q&A — **not reachable from either surface**, and the shipped channel description names it. WhatsApp ack — **earnable, but the state is in the glyph's colour**, not its name (same trap as the Google stars); `last-msg-status` presence is itself a direction signal. Messenger `folderName` — **does not separate the folders**, one value on every row; and every LS numeric column is a `[hi, lo]` pair, so the naive unread check marks everything unread. `moduleCount` — **stale state from a previous failed attempt**, never reset. Plus: the WhatsApp chat list is **transient after a cold launch**, so a scan then sees zero chats. | — |
| ~~**A2**~~ | ~~109~~ | ✅ **Done, v4.99.75.** `Assets/Config/selectors/whatsapp.json`, `SelectorManifest`, `SelectorManifestLoader`, `window.__umPick` / `__umSelectorsReady` / `__umSelectorReport`. Load order override → embedded default → JS built-in; a bad override is ignored, never fatal. One call site migrated as proof. 19 tests. **Deviation:** the brief's "supported client-version range" is a free-text `observedAgainst` instead — none of these clients expose a version the loader could compare, so a structured range would be a field nothing could ever validate. | — |
| ~~**A3**~~ | ~~110~~ | ✅ **Done, v4.99.76.** 57 call sites, 50 anchors, across `whatsapp-adapter.js`, `adapter-core.js`, `ConversationFocusHelper.cs` and `OversightSnapshotReader.cs`. **The schema held** — one field had to be added (`match: "union"`, for the unread-badge sum) and one helper shape (`umCandidates`, for sites that iterate their own list). `whatsapp-store-bridge.js` needed no migration: it has **zero** DOM selectors, it reads the in-memory store. Verified live against three real accounts. **Found and fixed en route:** a brace error that killed the entire adapter with the suite green (see A3.1). | — |
| **A3.1** | 110 | ✅ `InjectedScriptSyntaxTests` — `node --check` over every injected script, because the above escaped 1940 passing tests. Every other JS test here asserts on script *text*, which a syntactically broken file passes. Guard verified by deliberately breaking a file. | — |
| ~~**A4**~~ | ~~111~~ | ✅ **Done, v4.99.77.** `SelectorHealth` + a second Settings → Data line; *working / degraded / broken*, plain language, owner's screen and `app.log` only. **Three design corrections found by measuring:** readiness cannot gate escalation (the readiness anchors *are* the chat list, so a real break reports "not ready" too — threshold raised to 10 consecutive misses, time being the only honest discriminator); 28 of 50 anchors marked `optional` because conversation-scoped and dormant anchors never match on a healthy account and would have fired a false alarm on every install; degraded says "Still reading correctly" so a fallback is never read as lost data. `AccountReadHealth`/`ReadFailed` deliberately untouched — a degraded selector that still resolves is not a read failure. Also fixed A3's leftover: `attachSidebarObserver` retries until the list exists. | — |
| ~~**A5**~~ | ~~112~~ | ✅ **Done, v4.99.78.** `SelectorManifestUpdater` + a `selectors-<platform>.json` release asset, discovered in the pass that already finds the installer and applied **before** the version comparison — a fix that only shipped alongside a new binary would defeat the purpose. Guards: GitHub host allowlist (stricter than the installer path, and a test asserts the difference); validated by the loader's own parser plus shape caps before it touches disk; 256 KB cap enforced on the stream, not `Content-Length`; atomic write. One GET, no body, no query, nothing app-derived on the wire — [egress-inventory.md](egress-inventory.md) row 4. Rides `EnableAutoUpdate`, so no new outbound request for an owner who turned updates off. | — |
| ~~**A6**~~ | ~~113~~ | ✅ **Done, v4.99.79.** The schema generalised, and needed one field: `kind: "regex"`. Google's pages get no adapter-core, so the manifest reaches them as `window.__umSelectors` ahead of the kickoff scripts and is read by `__umGRSel`/`__umGRRx` — same contract, no shared runtime. Both review-count layouts are ordered candidates under `reviewTotalPaired`, with `reviewTotalUnpaired` kept separate because its capture shape differs. Verified live on all three profiles (4.6/994, 4.7/434, 4.6/249). **The star-rating colours were deliberately NOT moved into the manifest** — this row asked for it, and reading the code showed the star reader compares each glyph to the *first star's own colour* rather than a known gold, so it is self-calibrating; hard-coding two values would have been a regression dressed as configuration. A test records why. Also: `notes` prose is no longer modelled, so it stops being serialized into every page. | — |
| ~~**A7**~~ | ~~114~~ | ✅ **Done, v4.99.80.** `NavigationOperations` declares all four navigations with their readback anchors, retry budgets and side-effect flags. **The correctness win: the focus readback now decides, it does not narrate.** It was already being read — but *after* the return, purely for the log, while the method returned the navigator's own `true`. The click paths report success whether or not the chat opened, so that bool could be a lie; the composer check now gates it, with an un-evaluable readback treated as "no evidence" rather than failure. `show-archived` is new and built to the pattern: its readback was **measured, not guessed**, which changed the answer — the row count does not move when the panel opens and the Back button is generic chrome, so the anchor is the panel's own `[data-testid="archived-chatlist"]`. Google's two URL navigations are registered as in-page implementations with a test pinning the any-google.com-host guard. | — |
| ~~**A8**~~ | ~~115~~ | ✅ **Done, v4.99.81 — and it is smaller than this row assumed, on purpose.** Reading the code first showed the cross-account unified queue **already exists**: `CommandCenterPanel.BuildNeedsReplyList` gathers waiting customers across every account with facet, age and location filters, `AwaitingChatActions` on each row, and click-through that focuses the real client. Building a second one would have been exactly the duplication this row warned against. What was genuinely missing was **honesty about the channels it cannot show** — a Messenger account's waiting customers are absent from that queue and nothing said so. `ChannelCoverage` classifies every channel into four levels and renders one line naming the gaps, silent about channels that carry no conversations at all. That line is also the first consumer `PlatformCapabilities.IsAggregateOnly` has ever had. **Not built:** a separate inbox page — there is nothing for it to show that the existing queue does not, until a Meta adapter lands in A9. | — |

### A-recheck · Increment 116 (v4.99.82) — six fixes, one of them the reason for the rest

Everything above was rechecked against the **running app**, not just the suite. The first finding is why
the others were looked for:

**The A8 coverage notice never rendered.** It read the `instances` parameter, which the caller had already
filtered by `ContributesConversationMetrics` — stripping out every channel the notice exists to name. It
was unit-tested, green, published, installed, and drew nothing. The unit tests passed because they fed
`DescribeGaps` synthetic lists directly: they proved the function and said nothing about the wiring. Only
opening the app found it. A source guard now fails the build if that wiring returns.

That is the lesson worth keeping from this whole stream: **the suite proves functions, the app proves
features.** Three of the eight increments claimed "verified live" and were; A8 claimed it and had not been.

The other five, all confirmed and fixed: a diagnostic read holding an account's refresh gate with no
timeout (could stop that account syncing for the process lifetime); the focus arrival check narrowed
below the selector it replaced (would report failure on a conversation that opened); an unscoped composer
candidate that also matches the sidebar search box (would report arrival with nothing open — only the
`#main` conjunction was saving it); the archived navigator clicking before reading (a slow open gets
undone by its own retry); and one loading account masking both the healthy summary and any real
degradation warning on every other account.

**If Track B never happens, Track A still ships a real product**: a manifest-driven, self-diagnosing
scraper stack, mapped navigation, and a unified inbox that is genuinely rich for WhatsApp and Google.
It is thin on Meta, and it says so on screen rather than rendering an empty list.

---

### Hardening · Increments 117–118 (v4.99.83–84) — the backlog rechecked against the running app

Asked to clear the pending backlog before A9. Several entries were stale; two listed items closed; two
defects nobody had listed were found by looking at the screen.

**Closed from the backlog**

- **§0.1a, the one open UI item — two status palettes.** 59 foreground references migrated to the audited
  `UmStatus*` tokens; the ratchet drops 69 → 10. The deferral reasoned that migrating blind risks contrast
  regressions; that is backwards for foregrounds, because `StatusContrastTests` already measures every
  `UmStatus*` colour against LightCard, DarkCard and DarkChrome at 4.5:1 — so the migration moved
  references **into** the measured set. Safe mechanically because no non-Background system brush was ever
  used as a background, verified before the replace. Confirmed on screen in both themes.
  The ten survivors are `*BackgroundBrush` washes and stay: they sit *behind* text, and there is no
  `UmStatusInfoWash` to move the Attention one onto.
- **U9, tab order — a real collision, fixed.** `TabIndex` is scoped to the window, not the control tree it
  is written in. The sidebar numbers rows 1, 2, 3 … and a real rail carries eleven, while Settings
  declared 10 and 20 and the personal panel 15 and 20 — so sidebar row ten and the Settings nav both
  claimed 10. Observed live: tabbing off Dashboard landed on Reports. Three bands now (rail 1–89, footer
  90–99, content 200+), with tests that fail on an overlap.
- **F-OFFLINE-07 was stale.** `NavigationRetryScheduler` ships and was watched working live.

**Found by looking, not listed**

- The hero cited an age from a population it excludes — "45 customers are waiting · oldest 49d", where the
  49-day wait is in the backlog and therefore not one of the 45. Now says "oldest in the backlog".
- Analytics showed one fact under two headline tiles: "Replies (15m)" is fixed at 15 minutes and "SLA Met"
  uses the configured target, **which defaults to 15**. Both read 33% with nothing to say why. The SLA
  tile now names its threshold.
- Six defects in this stream's own work, including **the A8 coverage notice never rendering at all** — it
  read a list its own targets had already been filtered out of. Fully unit-tested, green, published,
  installed, and drawing nothing. See Increment 116 above.

**Still open, and none of it is code I can write.** No screen reader has ever been run; the full tab order
is uncertified beyond the collision fixed above; ARM64 has never been installed; uninstall data-erasure is
unverified; `ui-smoke` is intermittent on CI and needs repo admin to read the job log. The first two are
now **B6**.

### Instagram signed in (2026-09-04) — what it changed, and what it did not

Two Instagram accounts were added and signed in, which unblocked
[instagram.md](scraper-inventory/instagram.md). Three findings, in descending order of consequence.

**1 · Instagram carries the same LightSpeed store as Messenger — and on the feed it is empty.**
`require('LSDatabaseSingleton')` resolves with the identical 358 tables. But measured on a signed-in
account with six unread DMs: `threads` **0**, `messages` **0**, `contacts` **0**. The difference is
product, not technology — `messenger.com` *is* an inbox so its store is populated on load, whereas
`instagram.com` is a feed and Direct is a separate route. The passive read that makes Messenger rich
yields nothing here.

Corroborated independently: Meta's own Project LightSpeed / MSYS writing describes the cross-app
messaging platform as modelling the app "relationally across hundreds of tables" with core **threads,
messages, attachments, contacts** — exactly the schema found live in both clients. Shared by design, not
by accident, which is the best available reason to expect it to hold.

**2 · What Instagram *can* give passively is a count, from the tab title.** `document.title` reads
`"(6) Instagram"`. No navigation, no thread opened, no receipt. That makes Instagram the **first platform
that would actually reach `PlatformCapabilities.IsAggregateOnly`** — the rendering path built in A8 and
until now reachable by nothing shipped.

**3 · Sign-in detection is broken, on every channel.** Two defects in `connection-handshake.js`:

- `evaluateConnection` tests `loggedIn` **before** `loggedOut`, and WhatsApp's profile carries
  `urlLoggedIn: ['web.whatsapp.com']` — so an account sitting on the **QR sign-in screen** reports
  `Connected · "Signed in"`. The QR check below it is never reached.
- Only two profiles exist, `whatsapp` and `generic`. Instagram, Messenger and Google Business all fall to
  `generic`, whose logged-in test is `main, [role="main"], nav, header` — markup present on most login
  pages too.

And nothing meaningful gates on the result: `OversightAlertMonitor` checks `Connected` but
`OversightSnapshotReader.RefreshAsync` — the actual scan, and the manual Re-sync path — does not, and the
command centre does not either. Its header currently reads "8 professional accounts **connected**" for a
set that includes accounts nothing has ever signed into.

**Consequence for a logged-out account: the scraper runs, finds nothing, and reports a quiet account.**
That is the failure mode `AccountReadHealth` exists to prevent, arriving through a door it does not watch.

### A12 · Sign in before you scrape (no owner needed)

| # | What lands | Gated on |
|---|---|---|
| ~~**A12**~~ | ✅ **Done, v4.99.85 (Increment 119).** Fix `evaluateConnection` to test logged-out first. Real profiles for `instagram`, `messenger`, `googlebusiness`. Gate `OversightSnapshotReader` on the resolved state, not just the alert monitor. Command centre renders three tiers — **measured in full · counts only · not signed in** — and shows no figures at all for an account nothing is signed into, rather than stale ones. Header stops claiming "connected". | — |
| ~~**A13**~~ | ✅ **Done, v4.99.88 (Increment 122), corrected in v4.99.89–90.** **Instagram thread-level adapter — bigger than this row first said.** Read `PolarisRelayEnvironment`, not the DOM and not LightSpeed: `XFBIGDirectViewerThread` gives `thread_title`, `last_activity_timestamp_ms`, `users[].username` and an unread flag via the `$r:client__is_unread` resolver, for the top 15 threads, **on the feed with no navigation**. Verified against both accounts' own badges (1/1, 6/6). So Instagram feeds the needs-a-reply queue with real rows; `CanReadUnread` + `CanReadTimestamps` go true, `CanReadPreview` stays false. Three limits to state on screen: **Primary folder, top 15** (`has_next_page` is true, and General/Requests are never fetched); unread is a **lower bound** on awaiting, since a thread opened but unanswered reads as read — so the queue says "at least N". Depends on A12: a logged-out tab has no Relay mailbox and would read as zero. | A12 |
| ~~**A13b**~~ | ✅ **Done, v4.99.91 (Increment 125).** **New-comment counts, the second Instagram surface.** `XDTNotificationBadgeCount.activity_badge_counts` breaks the badge into `comments` / `likes` / `relationships` — measured 4/5/1 and 5/9/8 on the two accounts, on the feed, no navigation. **Comments are a customer waiting in public**, which nothing in the product has ever surfaced. Aggregate by nature — who, where and what are not fetched — so this is `IsAggregateOnly`'s first genuine consumer. Must be phrased "4 new comments", never "4 comments need a reply": the count clears when the notifications panel is opened, replied to or not. Ships beside A13, not inside it — a queue of named people and a number that clears on a glance are different objects. | A12 |

> **A13 supersedes the "counts-only" plan this row carried on 2026-09-04**, which was written from a
> LightSpeed measurement and generalised to the whole page. Evidence and the two traps that make a naive
> read report every account permanently caught up: [scraper-inventory/instagram.md](scraper-inventory/instagram.md).

## Track B — needs the owner

One sitting, roughly 50–60 minutes. Every item below is something no amount of agent work can reach.

**B6 is the one to do first if you only do one.** It needs no second device and no new logins, it closes
the largest untested area in the product, and it is the only way tab order can be certified at all.

| # | What is needed | Time | What it unblocks |
|---|---|---|---|
| **B1** | **Add a Meta Business Suite account and an Instagram account, and log into each.** Both are already offered in the Add-account picker. Credentials are never entered by the agent. | ~5 min | §4.3 experiments 1, 3 and 4; the two BLOCKED inventory files. Experiment 3 — reading Meta's own response rate and response time off Insights — is a responsiveness figure the product cannot produce by any other means. |
| **B2a** | *Downgraded — no longer a gate.* **Does `instagram.com/direct/inbox/` open a thread?** One navigation to the DM *list*, watched from a second device. | ~10 min | **Was** the gate on Instagram being anything more than a number; A13 removed that — who is waiting and for how long are readable on the feed, unprompted. What is still behind this door is **preview text** and **threads beyond the top 15**. Worth doing only if the owner wants the queue to show *what the customer said*, not just who is waiting. Still not attempted unilaterally: a "Seen" on a real customer cannot be withdrawn. |
| **B2** | **The read-receipt experiment.** Second device, a test thread from a personal account, both runs. Protocol: [experiment-read-receipts.md](scraper-inventory/experiment-read-receipts.md). | ~20 min | The assumption the entire Meta design rests on. Run A licenses the Messenger store bridge; Run B decides whether *focus thread* is a read-only operation. |
| **B3** | **`send?phone=` against a number you control.** Does `web.whatsapp.com/send?phone=<digits>` open an existing conversation without creating a draft, marking anything read, or touching recents? | ~5 min | Replaces the most fragile navigation in the product — ~120 lines of defensive row-clicking — with a URL. Would materially simplify A7. |
| **B4** | **Four decisions**, once B1–B3 have produced evidence: the §3.6 architectural fork (Business Suite as *source* / *contributor* / *own channel*); the `messenger.com` start-URL redirect fix; whether Meta channels enter the unified inbox at all; and whether to re-scope `RequiresThreadOpenToRead`. | — | A9–A11 below. |
| **B5** | *Optional, disruptive.* **Logged-out / disconnected state captures.** One account signed out on each channel, briefly. | ~10 min | The one Phase 1 gap that cannot be filled passively. A logged-out client must not read as "zero unread" — that is how an app reports a quiet day during a session expiry. |
| **B6** | **The accessibility listening session.** Narrator on, four short passes. Script: [accessibility-listening-session.md](accessibility-listening-session.md). | ~20 min | **Two gaps in one pass.** Everything in the accessibility work is right by construction and by test and nobody has ever *listened* to it — the single largest untested area. It also settles tab order, which cannot be certified from outside: focus rings are one or two pixels and do not survive screenshot scaling, whereas Narrator *announces* each focused control. Independent of B1–B5; can be done first. |

---

## Track A′ — after Track B

Sequenced here because their **shape**, not just their content, depends on B's answers.

| # | Increment | What lands | Gated on |
|---|---|---|---|
| **A9** | 116 | **Messenger store bridge + adapter.** A port of `whatsapp-store-bridge.js` onto `LSDatabaseSingleton`. Flip `CanReadUnread` / `CanReadPreview` / `CanReadTimestamps` / `CanReadContactIdentity` / `SupportsFrt` **one at a time, each as the read that backs it lands** — never ahead. | B2 Run A |
| **A10** | 117 | **Business Suite + Instagram inventory, then their manifest entries.** Includes whichever de-duplication the B4 fork decision requires — a customer who connects both Business Suite and Instagram must not appear twice in "who is waiting". | B1, B4 |
| **A11** | 118 | **Meta columns in the unified inbox**, at whatever fidelity B2 licensed. | A9, A10, B4 |

---

## Standing constraints on every increment

Not restated per row because they never vary:

- **Zero oversight data leaves the machine.** Breakage reporting goes to the owner's screen and
  `app.log`, never to a vendor endpoint. This matters *more* now the app ships to third parties whose
  customer data this is.
- **The app never sends.** No injected JS that fills a composer, clicks send, or synthesises input on a
  message box. Reading is passive and undetectable; driving a composer is account automation, and a wave
  of customer bans ends the product. If there appears to be a safe version, write the argument down and
  put it to the owner — do not implement it and ask afterwards.
- **This app's own design system.** `Tokens.xaml`, the `Um*` scale, enforced by `DesignScaleTests`. No
  mimicking of any platform's visual identity.
- **No capability flag flipped ahead of the read that backs it.**
- **Full suite green before every push**, app killed first.
