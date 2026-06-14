# STAGE 3 — Agent-DOM-Scraper Changelog

**Date:** 2026-06-14  
**Scope:** `UnifiedMessenger/Assets/Scripts/whatsapp-adapter.js` only  
**Commit prefix:** `[DOM-Scraper]`

---

## P0 — MutationObserver throttling & consolidation

| Change | Before | After |
|--------|--------|-------|
| **domObserver sync scrape** | Single MO on 4 roots; synchronous `publishActiveThreadContext` + telemetry schedule per batch | Split into `sidebarDomObserver` (badge only) + `mainDomObserver` (#main); main path schedules `scheduleDomWork` (rAF + 300ms max-wait) |
| **outgoingObserver** | Dedicated MO on `#main` calling `publishOutgoingStatusFromDom` unthrottled | **Removed** — outgoing status coalesced in `flushDomWork` alongside telemetry (adapter-core + thread-status-auditor retain their scoped pipelines) |
| **MO overlap on #main** | 5 overlapping observers (dom + outgoing + adapter-core + auditor) | whatsapp-adapter reduced to **1 scoped main MO**; sidebar MO isolated to chat list |
| **Hidden tab CPU** | MO active when `document.hidden` | `disconnectDomObservers()` on hide; `reconnectDomObservers()` on show |
| **Render loop guard** | None | `MAX_DOM_WORK_DEPTH = 3` prevents re-entrant flush storms |

---

## P0 — Batched DOM reads

| Change | Detail |
|--------|--------|
| **Per-tick header cache** | `getHeaderForTick`, `getLabelsForTick`, `getConversationKeyForTick` — single extract per rAF flush |
| **Incremental telemetry scan** | Reverse walk from last `msg-container`; stops when both last-sent and last-received bounds found |
| **Outgoing container lookup** | `findNewestOutgoingContainer` — backward scan instead of full `querySelectorAll` + last-index assumption |
| **Shared sidebar row resolver** | `findSidebarRowForTitle` deduplicates `scrapeSidebarLabelsForTitle` / `getPreviewFromDom` row scans |

---

## P1 — Allocation reduction

| Change | Detail |
|--------|--------|
| **Label dedup** | `labelDedupScratch` object map replaces O(n²) `indexOf` |
| **Signature buffer** | Reused `signaturePartsScratch` array for telemetry dedup |
| **Payload scratch objects** | `telemetryPayloadScratch` / `outgoingPayloadScratch` mutated in-place before `postMessage` |
| **normalizeText** | Whitespace collapse without regex `.replace(/\s+/g)` in hot path |
| **Telemetry payload shape** | Fields aligned with `WhatsAppTelemetryPayload` / `WhatsAppIngressHandler.HandleTelemetry` (`conversationKey`, `customerName`, `businessLabels`, `lastReceivedAtUtc`, `lastSentKind`, `timestampUtc`, etc.) |

---

## Stage 3 purge

| Removed / gated | Rationale |
|-----------------|-----------|
| `publishActiveThreadContext` + `whatsapp-thread-context` emits | 80% overlap with `whatsapp-telemetry`; C# `HandleTelemetry` already upserts thread context |
| `outgoingObserver` | Duplicate of adapter-core debounced outgoing monitor |
| `domObserver` (monolithic) | Replaced by scoped sidebar/main split |
| `telemetryScheduled` / `telemetryTimer` | Merged into unified `scheduleDomWork` |
| `window.__umWhatsAppExtractChatHeader` / `__umWhatsAppScrapeSidebarLabels` | Gated behind `UM_DEV` (`__umDevMode` or `__umStressTestEnabled`) |
| `window.__umWhatsAppDetectDeliveryStatus` | **Retained in production** — required by `thread-status-auditor.js` |

---

## Stage 4 — Sabotage prep

Added guarded debug hook:

```javascript
window.__umStressTestDomFlood(count)  // default 5000 mutations / ~2s on #main
```

Requires `window.__umDevMode` or `window.__umStressTestEnabled`. Returns `{ ok, target, intervalMs, root, startedAtUtc }`.

---

## Not modified (per swarm boundaries)

- C# ingress queue / `WhatsAppIngressHandler` (Core-Architect)
- `adapter-core.js`, `thread-status-auditor.js`
- `bin/` output copies (rebuilt via project copy)

---

## Validation

- `node --check whatsapp-adapter.js` — syntax OK
- `dotnet test -c Release -p:Platform=x64` — ingress + backfill script tests

---

*End of STAGE 3 DOM-Scraper changelog.*
