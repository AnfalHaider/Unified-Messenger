---
id: v6-empty-read-ask-signin-before-not-ready
date: 2026-09-13
agent: claude
title: An empty read has three causes; ask sign-in first, then loading, and only then call the reader broken
triggers: [read-empty, reader health, not ready, no-store, QR, signed out, canvas, overlapping ticks, setInterval, selftest]
files: [v6/app/main.ts, v6/channels/whatsapp/index.ts]
cost: Every start reported the WhatsApp reader as broken, and a signed-out account read as "still loading".
status: live
---

Order in `readAccount`: probe sign-in with v5's selectors (`[data-testid="qrcode"]`, `canvas[aria-label*="QR" i]`; not a bare `canvas`, which the loading screen also draws), then the module's `notReady` (store-bridge stages `no-store` or `no-models`, script not injected yet, Instagram's unsynced badge), and only then count a health failure. Guard the read timer against re-entry: a slow page let four `setInterval` passes land at once and multiplied the failures.
