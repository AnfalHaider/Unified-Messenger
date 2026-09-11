---
id: gotcha-executescriptasync-no-timeout-under-gate
date: 2026-09-04
agent: claude
title: ExecuteScriptAsync has no timeout, so never await it while holding an account's refresh gate
triggers: [ExecuteScriptAsync, SemaphoreSlim, gate, finally, hung page, timeout, WaitAsync, selector health, diagnostics]
files: [UnifiedMessenger/Services/Oversight/OversightSnapshotReader.cs, UnifiedMessenger/Services/Session/IInstanceConnection.cs, UnifiedMessenger/Services/Oversight/SelectorHealth.cs]
cost: v4.99.77 through v4.99.81 shipped a diagnostic read that could keep an account's refresh gate shut for the life of the process, silently stopping oversight for that branch. Found only on a later recheck (v4.99.82).
evidence: OversightSnapshotReader.cs finally block ("Gate FIRST, diagnostics after") and CaptureSelectorHealthAsync's `.WaitAsync(TimeSpan.FromSeconds(10))`; fix commit 81d4b9c.
status: live
---
The A4 selector-health capture was placed inside the scan's `finally`, before `gate.Release()`. The assumption was that a diagnostic round trip is cheap and always returns. It is not: `ExecuteScriptAsync` has no timeout of its own, and a page that stops answering never completes the task, so the gate is never released. This is a different trap from the AGENTS.md row about `ExecuteScriptAsync` not awaiting JS promises. Release the gate first and run optional reads after it. Bound every script call with `.WaitAsync(...)`, and make a timeout cost one skipped reading rather than a live task. Diagnostics must never be able to block the thing they diagnose.
