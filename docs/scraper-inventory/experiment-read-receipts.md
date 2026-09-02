# Experiment: does opening a Meta thread fire a read receipt?

**Status: NOT RUN.** Needs a second device. This is §4.3.2 of the scraper-foundation brief.

This is the single assumption the entire Meta design rests on. `PlatformCapabilities.RequiresThreadOpenToRead`
asserts it, `MetaAggregateOnly` applies it to Messenger and Instagram, and **nobody in this repo has ever
verified it.** Whichever way it lands, an assumption becomes a fact.

It cannot be observed from inside the browser. That is the whole point: a view rendering is not a view
being side-effect-free.

---

## Why it now matters more, not less

The Messenger inventory found that per-conversation detail — unread count, preview, direction,
timestamps — is readable from a local database with **no thread opened at all**
([messenger.md](messenger.md)). So the prohibition no longer decides whether Messenger can be measured.

It now decides two narrower but still live questions:

1. **Is the app currently misbehaving?** `messenger.com` redirects its own root into the most recent
   conversation (CONFIRMED, `redirectCount === 1`). If the receipt fires, then the shipped app tells a
   customer "seen" on **every launch, refresh and session warm**, with no user intent. That is a defect
   to fix, and the right fix differs depending on the answer.
2. **What may Phase 3 navigation do?** If the receipt fires, "focus thread" is not a read-only operation
   and must be gated behind explicit user intent — the user clicking reply — and never run by a
   background scan.

---

## Protocol

**Roles:** you drive; a second device (a phone, logged into a *different* personal account) is the
sender and the observer.

### Setup — use a thread you own, not a customer's

Per the agreed scope, do not test on a real customer conversation. A "Seen" cannot be withdrawn.

1. From the **second device's** personal account, send one short message to the business account on
   Messenger. This creates a thread that belongs to you.
2. On the second device, keep that conversation **open and visible**. The sender's view is where the
   evidence appears — the small avatar/"Seen" indicator under the sent message.
3. Confirm the message currently shows as **Delivered, not Seen**. Write down the exact indicator state.
   This is the baseline; without it the result proves nothing.

### Run A — read the local store only (the path the product wants to use)

4. In Unified Messenger, leave the Messenger account **on the chat list**. Do not click the new thread.
5. Tell me when you are here and I will run the store read against it over CDP.
6. **Watch the second device for 60 seconds.** Record: did the indicator change?

**Expected: no change.** A confirmed no-change here upgrades "reading the LS store fires no receipt"
from LIKELY to CONFIRMED, and that is what licenses the whole Messenger adapter.

### Run B — open the thread (the path the prohibition is about)

7. Now click the thread in the app so the conversation view opens.
8. **Watch the second device.** Record: did the indicator flip to Seen, and how fast?

### Record, for each run

| Field | Run A (store read) | Run B (thread open) |
|---|---|---|
| Indicator before | | |
| Indicator after | | |
| Seconds to change | | |
| Second-device account type (personal / business) | | |
| Date + time | | |

### Repeat separately for each surface

Messenger, Instagram, and Meta Business Suite may behave differently — **Business Suite is an agent
inbox and is the most likely of the three to fire.** Do not generalise one result to the others.

---

## What each outcome means

| Result | Consequence |
|---|---|
| **A: no change · B: Seen fires** | The expected shape. Prohibition re-scoped to "no thread-view navigation"; the store bridge is licensed; the Messenger start-URL redirect is a real defect that must be fixed before shipping. |
| **A: no change · B: no change** | The prohibition is unnecessary altogether. Meta channels can be measured like WhatsApp. Do not remove `RequiresThreadOpenToRead` on one observation — repeat on a second thread first. |
| **A: Seen fires** | The store read is not passive after all. The entire Messenger direction dies, honestly and early, and effort redirects into WhatsApp depth and Google completeness. Worth knowing more than any other result here. |

---

## And write the answer down the day you get it

Including the boring one. This file is the place.
