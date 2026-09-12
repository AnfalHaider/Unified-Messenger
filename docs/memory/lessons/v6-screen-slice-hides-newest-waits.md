---
id: v6-screen-slice-hides-newest-waits
date: 2026-09-13
agent: claude
title: UiState.queue is cut to 60 longest waits, so anything about new waits must use waitingQueue
triggers: [alerts, notifications, near target, queue, slice, UiState, waitingQueue, backlog, missing rows]
files: [v6/app/view-model.ts, v6/app/main.ts, v6/core/alerts.ts]
cost: Caught before shipping; with 90 waiting, the near-target alert would never have fired.
status: live
---

`buildUiState` sorts the queue longest wait first and sends only `queue.slice(0, 60)` to the screens. A chat about to pass a 15-minute target has one of the *shortest* waits, so on a real account (80–90 waiting) it is exactly the row the slice drops. Main-process logic that needs every waiting chat — alerts, counts, anything "newest" — calls `waitingQueue(config, snapshots, overrides, now, signedOut)` from `view-model.ts`, which is the same rows unsliced. Never derive such a decision from `state.queue`.
