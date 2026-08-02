# ADR-006: One channel-event shape, one derived session state

## Status

Accepted (v4.89.0)

## Context

Two problems surfaced while adding the store bridge (ADR-005).

**Adapters each grew their own plumbing.** The WhatsApp scan, the Google review scraper and the (still
unwritten) Telegram/Messenger scrapers had no shared way to say "something happened". Every consumer —
oversight rollup, dashboard, notifications — reached into each adapter individually.

**`Connected` was overloaded.** `InstanceConnectionStatus` says whether the page is logged in. It does
not say whether data is actually flowing. An account whose reads had quietly stopped still displayed as
"connected", and nothing reconciled connection status against snapshot freshness or adapter health.

The gateway projects surveyed for ADR-005 (WAHA, Evolution API) had both problems solved in a shape worth
borrowing: a normalized webhook payload, and an explicit session lifecycle
(`STARTING → SCAN_QR → WORKING → FAILED`).

## Decision

**Borrow the shape, not the transport.** `IChannelEvent` and its records mirror the gateways' webhook
payload structure but are strictly in-process. There is no HTTP surface, no webhook, no serialization to
a network — those would violate the app's local-only constraint outright.

`ChannelEventBus` is a single in-process publish/subscribe point. Publishing is fire-and-forget and
swallows subscriber exceptions: a scraper must never fail because a dashboard handler threw, since that
trades a cosmetic bug for lost oversight data. Handlers run on the publishing thread, so UI subscribers
marshal via `DispatcherQueue.TryEnqueue` — the same rule the AI callbacks already follow.

`SessionState` (`Starting / ScanQr / Working / Degraded / Failed`) is deliberately a **projection, not a
store**. `SessionStateProjection.Resolve` derives it from connection status plus snapshot freshness. The
truth stays in `InstanceConnectionStatusService` and `OversightChatSnapshotService`; adding a fourth
place to store state is exactly how the three existing ones came to disagree.

`Degraded` is the state that did not previously exist and is the reason for the whole exercise:
connected, but the numbers have gone stale.

## Consequences

- Adding a channel means publishing `ChannelSnapshotEvent`, not wiring new consumers.
- Events carry their `Source` ("store-bridge" / "indexeddb"), so "why is this stale?" is answerable.
- Poll cadence became adaptive: 25s when every polled account is on the cheap bridge read, 90s otherwise,
  so one fallen-back account cannot drag the expensive IndexedDB reader into a fast loop.
- An account whose data has rotted now renders a **Stale** chip rather than looking healthy.
- The projection is pure, so it unit-tests without a WebView.
- The bus currently has one publisher pair. That is intentional groundwork, and it is a real cost if the
  Telegram/Messenger scrapers never land — a risk accepted because those scrapers are blocked only on
  access to live accounts, not on design.
