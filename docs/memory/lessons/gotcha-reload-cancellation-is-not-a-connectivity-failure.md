---
id: gotcha-reload-cancellation-is-not-a-connectivity-failure
date: 2026-08-17
agent: claude
title: A WebView reload cancels its own in-flight navigation and reports Unknown, which silently ended the offline retry chain after one attempt
triggers: [offline, retry, reconnect, NavigationRetryScheduler, OnNavigationFailed, Unknown, ConnectionAborted, WebErrorStatus, reload, ReconnectState, no internet]
files: [UnifiedMessenger/Services/Session/NavigationRetryScheduler.cs, UnifiedMessenger/Services/Adapters/PlatformNavigationHooks.cs, UnifiedMessenger.Tests/NavigationRetryTests.cs, docs/audit/findings/offline.md]
cost: F-OFFLINE-04 shipped as "five attempts over eight minutes" but made one attempt over ten seconds, and the sidebar dropped "No internet — reconnecting…" for a bare "Connection error" as soon as the retry fired.
evidence: 19a76ea (v4.99.28); NavigationRetryScheduler.cs comment with log lines "could not load (ConnectionAborted); retrying in 10s (attempt 1 of 5)" then "navigation failed (Unknown)"; NavigationRetryTests (confirmed to bite by reverting the fix).
status: live
---
`OnNavigationFailed` only scheduled the next retry when the failure looked like connectivity. The retry itself reloads the page, and that reload cancels the in-flight navigation. The cancellation surfaces as `Unknown`, not a connectivity status, so the chain stopped after its first attempt. The fix passed every unit test and was caught only by a live re-test behind a dead proxy. Treat an unrecognised failure on an account already believed offline as the same outage continuing, but still require a first failure to look like connectivity, so a certificate error is not retried five times. Use the scheduler's own `ReconnectState` (None/Retrying/GaveUp) as the authoritative UI signal, never the latest error status.
