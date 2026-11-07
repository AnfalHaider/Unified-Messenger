---
id: v6-the-quit-wait-is-fixed-and-one-relaunch-race-is-left
date: 2026-11-07
agent: claude
title: Handing the close wait to the test timeout removed every quit failure and halved the suite; one relaunch race survives, so smoke still needs two runs
triggers: [smoke, npm run smoke, playwright, flaky, flake, gate red, waitForEvent close, quit, timeout 0, shutdown, test timeout, 180_000, relaunch, single instance lock, requestSingleInstanceLock]
files: [v6/tests/smoke.spec.ts, v6/playwright.config.ts]
cost: Before the fix the gate could not be run twice with the same answer. After it, one test in two runs still flakes, so a single green run is still not proof.
evidence: 2026-11-07, same tree. Before — 7.9m 1 failed, 8.3m 4 failed, two of them "waitForEvent: Timeout 30000ms exceeded while waiting for event close" at tests/smoke.spec.ts:43. After — 4.6m 1 failed, 4.1m 0 failed (exit 0), no close timeout in either run.
supersedes: [v6-smoke-quit-waits-thirty-seconds-for-a-ninety-second-shutdown]
status: live
---

The diagnosis in the superseded lesson held. `quit()` did `app.waitForEvent('close')` with no options, and `waitForEvent` keeps Playwright's own 30-second default rather than inheriting the test timeout, so a slow-but-healthy shutdown failed a test that still had a minute in hand — in the one helper all 57 ending tests call.

**The fix, in two parts, because one alone does nothing.** `timeout: 0` on the close wait hands the limit to the per-test timeout; and the per-test timeout in `playwright.config.ts` went 90s to 180s, because a quit alone can take up to 90 seconds and a test does its work *and then* quits. Leaving the test timeout at 90 would only have moved the same failure from a confusing `waitForEvent` timeout to an equally confusing test timeout.

**What it bought, measured:** no close timeout in two runs, and the suite went from 7.9–8.3 minutes to 4.1–4.6. The missing four minutes were the failures themselves paying 30 seconds each — a flaky suite is slow *because* it is flaky, so a sudden halving of the runtime is evidence the fix landed, not a coincidence to explain away.

**What survives.** `tests/smoke.spec.ts:142` ("a chat near its target alerts exactly once, across passes and a restart") failed one run in two, both before and after, always in its second phase: after the in-test quit and relaunch the window renders and asserts fine, then `page.waitForTimeout(6_000)` dies with "Target page, context or browser has been closed". So the relaunched copy goes away on its own within six seconds of drawing a screen. Unexplained. Worth checking first, and **not yet checked**: `app/main.ts:56` exits any copy that cannot take the single-instance lock, so a relaunch overlapping a predecessor whose process has not died would exit at once — except that this window *does* render before it goes, which that mechanism alone does not account for. Do not file this under the quit timeout; it is a different fault with a different shape.

Until it is found, **two green runs, not one, are what clears a change here** — and the failing sets across runs are disjoint, so one run names tests that are fine and clears tests that are not.
