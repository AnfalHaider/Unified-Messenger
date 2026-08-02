# ADR-005: Read WhatsApp's in-memory model store, not only its persisted store

## Status

Accepted (v4.88.0)

## Context

Oversight metrics were built from WhatsApp Web's persisted IndexedDB `chat` store. That source has two
structural limits we paid for in bugs, not in theory:

1. **Message bodies are encrypted at rest** (`msgRowOpaqueData`). The chat store carries no readable
   preview, so previews had to be harvested from the rendered sidebar DOM — roughly 60 rows. Every
   customer further down the list showed a blank preview.
2. **It lags.** A reply sent from the owner's phone can take a while to sync into the persisted store, so
   a just-answered chat kept reading as "awaiting". The workaround was to reload the WebView on Re-sync.

We evaluated four established WhatsApp-integration projects as possible foundations — OpenWA,
Evolution API, WAHA, and whatsapp-web.js / wa-automate. All are Node services whose engines are either
unofficial protocol reimplementations (Baileys, NOWEB, GOWS) or headless-puppeteer browsers.

## Decision

Do **not** adopt any of them as a foundation:

- Protocol reimplementations are banned outright — they carry account-ban risk, which is existential for
  a business owner's real WhatsApp account.
- A headless puppeteer browser is redundant. Our WebView2 *is* the browser, and it holds the owner's real
  logged-in session, which is the lowest-ban-risk arrangement available.
- They are overwhelmingly send-oriented. This app never sends.

Instead, adopt the *technique* those projects share: reach WhatsApp Web's own webpack module registry
from inside the page and read its in-memory model collections. Those models are already decrypted and
always current, because they are what the page itself renders.

Implemented as `Assets/Scripts/whatsapp-store-bridge.js`. Constraints that shaped it:

- **Read-only.** No send/mutate surface is resolved or called; a test asserts the absence of those
  identifiers. Models are read, never written.
- **Discovery by capability, not by module name.** Module names churn between WhatsApp releases and
  several look-alike modules export a `Chat` property. We identify a collection by the shape of the models
  it holds ("has `getModelsArray()`, models carry an id and a numeric `unreadCount`"). Three discovery
  strategies are tried in order: `require('__debug').modulesMap`, a webpack-chunk push (moduleRaid), and
  the module cache.
- **Fail soft in both directions.** It never throws into the page — an exception escaping into WhatsApp
  Web would break the owner's actual messaging client, which is worse than losing a metric. And any
  failure falls back to the IndexedDB scan, so a WhatsApp change costs previews, not oversight.
- **Identical output envelope.** The scan emits exactly the JSON the IndexedDB scan emits, so
  `ChatEntryParser` reads either source unchanged and the two cannot drift on the needs-reply list.

## Consequences

- Previews now exist for **every** chat, not just rendered sidebar rows.
- Replies sent from the phone clear "awaiting" promptly; Re-sync no longer needs to reload each account
  purely to harvest previews.
- The read is cheap enough (synchronous, in-memory) to justify the adaptive poll cadence in ADR-006.
- **A silent fallback is an invisible one.** `StoreBridgeHealth` plus the Settings → Data health line and
  `window.__umStoreBridgeProbe()` exist specifically so this degradation is observable.
- The IndexedDB path and its sidebar-DOM preview harvest must be **kept**, not deleted — they are the
  fallback. Three near-duplicate DOM preview harvesters in `whatsapp-adapter.js` remain a known
  consolidation opportunity, deferred rather than done.

## Attribution

Technique adapted from `wppconnect/wa-js` and `whatsapp-web.js` (both Apache-2.0). No bundle is vendored
and no third-party protocol library is used.
