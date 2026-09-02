# Meta Business Suite (`business.facebook.com`) — scraper inventory

## ⛔ BLOCKED — no live session

**As of 2026-09-02 there is no Meta Business Suite account configured in the app**, so nothing in this
file could be observed. Configured accounts are: 3 × WhatsApp, 3 × Google Business, 1 × Messenger.

Per the inventory rule, **nothing is guessed here.** No selector table, no view list, no anchor ranking.
A guessed table is worse than no table: it looks like evidence.

### What unblocks it

The owner adds a **Meta Business Suite** account in the app and completes the Facebook login themselves.
Nothing is in the way: the channel is registered (`PlatformDefinition.All`, id `metabusinesssuite`,
default URL `https://business.facebook.com/`) and **is offered in the Add-account picker** —
`PlatformModuleSettingsHelper.GetSelectablePlatforms()` returns `PlatformDefinition.All` unfiltered as
of v4.99.74. (The old `HiddenFromPicker` gate that AGENTS.md still describes no longer exists in the
tree; verified 2026-09-02.)

The existing Messenger account is logged into the same Facebook identity, so the login itself is
expected to be quick — but it must be done by the owner. Credentials are never entered by the agent.

---

## The three experiments waiting on this session

All three are from §4.3 of the session brief and all three can be answered in one sitting.

### 1. Does the inbox LIST expose thread data without opening anything?

Unread state, preview text, timestamp, originating platform, customer name, Done/follow-up status —
read from the list, touching no thread, confirmed from a second device that no "Seen" fired.

**This question is now less decisive than when it was written.** The Messenger inventory
([messenger.md](messenger.md)) found a complete local relational database on `messenger.com` itself,
readable with no thread opened and carrying unread count, preview snippet, direction, timestamps and a
read watermark. If Instagram carries the same LightSpeed store — plausible, since both are Meta web
clients built on the same module system — then the "one DOM instead of two" argument for Business Suite
largely evaporates, because each consumer client can be read directly and passively.

Business Suite still matters for two things the consumer clients cannot supply: workflow state
(Done / labels / assignment / follow-up), and the Insights figures below.

### 2. Does opening a thread fire a read receipt?

Business Suite is an **agent inbox**, so it is the most likely of the three surfaces to fire. Must be
tested separately from Messenger and Instagram — they may differ.

### 3. Can Meta's own response rate and response time be read off Insights?

The highest-value question that survives the Messenger finding intact. Per Facebook Page, per Instagram
account, or aggregate only? What date ranges? Is the "Very Responsive" badge state readable?

No thread needs opening, the figure is computed by Meta, and the product cannot produce a Meta
responsiveness figure by any other means today.

---

## Two hard limits — CONFIRMED before this phase, and both still stand

1. **Business Suite's WhatsApp inbox is unreachable for this owner and most customers.** It requires a
   WhatsApp Business Account on the Cloud API with a dedicated number that has never been active on
   regular WhatsApp. That is the number migration the superseded API research already killed: it would
   remove the number from the WhatsApp Business app the staff reply in. **Business Suite covers Messenger
   and Instagram. Not WhatsApp.** Anyone reading "all-in-one inbox for WhatsApp, Instagram and Facebook"
   in Meta's marketing is planning around a product this owner cannot have.

2. **"Mark as unread" does not un-fire a read receipt.** It restores the inbox's own state. The
   customer's "Seen" indicator fired the moment the thread opened and cannot be withdrawn. **Peek-and-
   restore must not be built.** It would restore the app's signal while having already told the customer
   we looked — the worst of both, and invisible from inside the browser. This is the most tempting wrong
   idea in this whole area.

---

## The architectural fork this channel forces

Business Suite is a **reader, not a channel**. A message that arrived on Instagram is an Instagram
message regardless of which UI reads it. But the app's model assumes one adapter reads its own
platform's client: `PlatformAdapterInternals.ResolveEnabledAdapter` switches on platform id,
capabilities hang off `PlatformDefinition`, and oversight entities are keyed by instance.

| Option | Shape | Verdict |
|---|---|---|
| **A — source** | Introduce a *source/reader* concept distinct from *channel*. One Business Suite account contributes `instagram` and `messenger` threads. | Correct modelling, larger change. |
| **B — contributor** | Keep the existing shape; its adapter emits `ThreadData` tagged with the originating platform. | Smaller change, muddier model. |
| **C — own channel** | Business Suite is its own channel. | **Wrong.** Double-counts anyone who also connects Instagram, and "Meta Business Suite" is not a thing customers message you on. |

Whichever wins, **de-duplication is mandatory** — a customer who connects both Business Suite and
Instagram must not appear twice in "who is waiting". That is the exact defect class this codebase has
fixed three times, and here it would be introduced by construction, not by accident.

**The Messenger finding shifts the balance toward doing nothing yet.** If both consumer clients can be
read passively and directly, Business Suite may be worth adding only for workflow state and Insights —
a much narrower job than "a reader that supplies two channels", and one that may not need A or B at all.
Decide after the session exists, not before.

---

## Also to inventory when the session lands

- Which Business Suite UI is being served — there is a `/latest/` shell and older paths. Record it.
- Whether the inbox list updates live or needs a refresh, and what a refresh costs.
- Whether the platform filter and a specific thread are URL-addressable (`/latest/inbox/…`).
- Insights: exact URLs, metric selectors, date-range controls, per-Page vs per-IG-account granularity.
- Labels, Done and follow-up state — **readable is the whole prize; writable is out of scope under D1.**
- Any history horizon on the inbox list, and the behaviour of the Requests and Spam folders.
- Whether Business Suite also exposes a LightSpeed store, as `messenger.com` does.
