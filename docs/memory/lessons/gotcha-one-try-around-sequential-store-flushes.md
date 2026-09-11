---
id: gotcha-one-try-around-sequential-store-flushes
date: 2026-08-10
agent: claude
title: One try block around seven sequential shutdown flushes let the first failing store discard the rest while shutdown reported success
triggers: [shutdown, flush, FlushPersistentStateAsync, try catch, durable store, AwaitingOverrideStore, lost state, LastFlushFailures, partial persistence, mark handled resurfaces]
files: [UnifiedMessenger/Services/ApplicationLifecycleService.cs, UnifiedMessenger.Tests/ApplicationLifecycleFlushTests.cs]
cost: S1 (F-CRASH-01). ResponseTimeTracker, ContactHistoryStore, AwaitingOverrideStore and KpiTrendStore could silently fail to persist, which re-surfaced chats the owner had marked handled. The loss varied by which store failed first, so it read as general flakiness.
evidence: 9b6f120 (v4.99.1); ApplicationLifecycleFlushTests 6 tests, 4 of which fail when reverted to the shared-try shape.
status: live
---
`FlushPersistentStateAsync` wrapped all store flushes in a single try. The catch only logged, so an exception from store N skipped stores N+1 onward and the app still exited normally. Independent side effects need independent failure handling. Each flush now has its own try/catch inside a loop over a named store array, so a newly added store inherits the isolation instead of being appended to a shared block. Failures are collected in `LastFlushFailures`, and cancellation counts as a failure because a cancelled flush is data that did not reach disk. The loop lives in `FlushStoresAsync` so the guarantee is testable without real singletons.
