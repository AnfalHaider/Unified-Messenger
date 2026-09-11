---
id: gotcha-check-then-act-across-await-on-ui-thread
date: 2026-08-17
agent: claude
title: Running on the UI thread does not protect a check-then-act guard that awaits before it writes; the second caller must join the in-flight task
triggers: [race, double attach, already has a registered WebView, EnsureSessionCoreAsync, await, UI thread, in flight, SessionFailed, check-then-act, re-entrancy]
files: [UnifiedMessenger/Services/Session/InstanceSessionManager.cs]
cost: Two rapid activations of one account built a second WebView2 (~1 GB profile-backed process) that then threw, and the resulting SessionFailed left the account in Error from an aborted navigation (F-OFFLINE-07).
evidence: 9cdb3b6; owner app.log "Instance … already has a registered WebView" (twice); after fix "Session initialisation already in flight; joining it instead of starting a second." with 0 registered-WebView errors; comment at InstanceSessionManager.cs.
status: live
---
`EnsureSessionCoreAsync` checked `_sessions` and then awaited three times (session cap, WebView2 construction, attach) before writing to it. `await` releases the UI thread, so a second caller in that window passed the same guard. The stack showed two `EnsureSessionCoreAsync` frames via `SwitchToCoreAsync`, and it was reproduced by sending two Enters to one sidebar row with no wait. Moving the check later cannot make it safe. Record the in-flight `Task` before the first await and have later callers await that task. When you see "single-threaded, so no race" reasoning around an `async` method, look for an await between the check and the write.
