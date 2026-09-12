---
id: v6-line-must-show-only-live-waiting
date: 2026-09-13
agent: claude
title: The line listed a signed-out account's month-old chats as customers waiting now
triggers: [queue, the line, waiting now, backlog, signed out, stale snapshot, awaitingChats, view model, huge wait]
files: [v6/app/view-model.ts]
cost: The owner's first look at the real shell showed 138 customers waiting up to 34 days.
status: live
---

"Waiting now" must apply the same backlog cutoff as `awaitingSplit` (`lastActivity >= now - backlogAfterDays`) and must exclude accounts currently signed out, whose last snapshot is history. Any new count, badge or list of waiting customers goes through `waitingNow` and the `live` account list in `buildUiState`, never straight to `awaitingChats`.
