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
| **A5** | 112 | **Ship a fix without a new binary.** Manifest delivery riding the constant-URL GET `GitHubUpdateService` already makes. Data only — not a remote-code channel, no customer information outbound. Update [egress-inventory.md](egress-inventory.md) if the request shape changes at all. | A4 |
| **A6** | 113 | **Migrate Google Business onto the manifest.** The second consumer, which is what proves the schema generalises past one channel. Carries the two review-count layouts as ordered fallbacks and the star-rating **colours** as data — so the next Google palette change is a manifest bump, not a silent mislabelling. | A5 |
| **A7** | 114 | **Phase 3 — navigation mapping, WhatsApp + Google.** Named testable operations (*focus conversation X*, *open review Y*, *show archived*). URL-first where §3.3 allows it, DOM-driven only where it does not. Every operation gets an **independent readback** (never the navigator's own return value) and a trace logging what was wanted beside what was reached. Bounded retry with a stated budget. Hard rule: no navigation may cause a side effect the inventory flagged. | A6 |
| **A8** | 115 | **Phase 4 — unified inbox shell.** Evolve the existing per-account L1 drill-down; reuse the command centre's filters, age buckets and `Controls/Shared/AwaitingChatActions` rather than building parallel versions that can disagree. Reuse `ChannelScope` for any figure that covers some channels and not others. Build the rendering path for `PlatformCapabilities.IsAggregateOnly` — declared, documented, and with **no consumer anywhere** today. Reply hands off to the real client; no composer. | A7 |

**If Track B never happens, Track A still ships a real product**: a manifest-driven, self-diagnosing
scraper stack, mapped navigation, and a unified inbox that is genuinely rich for WhatsApp and Google.
It is thin on Meta, and it says so on screen rather than rendering an empty list.

---

## Track B — needs the owner

One sitting, roughly 30–40 minutes. Every item below is something no amount of agent work can reach.

| # | What is needed | Time | What it unblocks |
|---|---|---|---|
| **B1** | **Add a Meta Business Suite account and an Instagram account, and log into each.** Both are already offered in the Add-account picker. Credentials are never entered by the agent. | ~5 min | §4.3 experiments 1, 3 and 4; the two BLOCKED inventory files. Experiment 3 — reading Meta's own response rate and response time off Insights — is a responsiveness figure the product cannot produce by any other means. |
| **B2** | **The read-receipt experiment.** Second device, a test thread from a personal account, both runs. Protocol: [experiment-read-receipts.md](scraper-inventory/experiment-read-receipts.md). | ~20 min | The assumption the entire Meta design rests on. Run A licenses the Messenger store bridge; Run B decides whether *focus thread* is a read-only operation. |
| **B3** | **`send?phone=` against a number you control.** Does `web.whatsapp.com/send?phone=<digits>` open an existing conversation without creating a draft, marking anything read, or touching recents? | ~5 min | Replaces the most fragile navigation in the product — ~120 lines of defensive row-clicking — with a URL. Would materially simplify A7. |
| **B4** | **Four decisions**, once B1–B3 have produced evidence: the §3.6 architectural fork (Business Suite as *source* / *contributor* / *own channel*); the `messenger.com` start-URL redirect fix; whether Meta channels enter the unified inbox at all; and whether to re-scope `RequiresThreadOpenToRead`. | — | A9–A11 below. |
| **B5** | *Optional, disruptive.* **Logged-out / disconnected state captures.** One account signed out on each channel, briefly. | ~10 min | The one Phase 1 gap that cannot be filled passively. A logged-out client must not read as "zero unread" — that is how an app reports a quiet day during a session expiry. |

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
