---
id: v6-smoke-quit-waits-thirty-seconds-for-a-ninety-second-shutdown
date: 2026-11-07
agent: claude
title: The gate's smoke suite failed twice with disjoint test sets; quit()'s waitForEvent keeps Playwright's own 30s default, not the 90s test timeout
triggers: [smoke, npm run smoke, playwright, flaky, flake, gate red, waitForEvent close, quit, timeout 30000, shutdown, electronApplication, test timeout, 90_000]
files: [v6/tests/smoke.spec.ts, v6/playwright.config.ts, AGENTS.md]
cost: Two 8-minute runs, 1 failure then 4, no test failing in both. The gate could neither pass nor name a defect, so a docs-only change could not be shown safe.
evidence: 2026-11-07, clean tree at 43f8fc3, docs-only working changes. Run 1 — 35 passed, 1 failed (smoke.spec.ts:536), which then passed alone in 4.1s. Run 2 — 32 passed, 4 failed (:137, :477, :1265, :1504); :477 and :1504 both "TimeoutError: electronApplication.waitForEvent: Timeout 30000ms exceeded while waiting for event close" at tests/smoke.spec.ts:43.
status: live
---

`npm run smoke` is the third leg of the gate. Run twice in a row against the same clean tree it gave two different answers — one failure, then four — and no test failed in both runs. A gate that disagrees with itself cannot clear a change or convict one.

**The part that is measured.** `playwright.config.ts` sets `timeout: 90_000` per test, and `AGENTS.md` already records that "a shutdown closes every account page in turn and can take 90 seconds". But the `quit()` helper at `tests/smoke.spec.ts:43` does `app.waitForEvent('close')` with no options, and `waitForEvent` does **not** inherit the test timeout — it keeps Playwright's own 30-second default. So the binding limit on a documented ≤90-second shutdown is 30 seconds, for all 57 tests that call `quit()`. Two of run 2's four failures are exactly that error at exactly that line. Passing `{ timeout: 90_000 }` in the helper would reach every call site at once.

**The part that is not.** The other two failures have different shapes and should not be filed under the same cause:

- `:137` — `page.waitForTimeout: Target page, context or browser has been closed` during an 11-second wait. The app went away underneath the test rather than refusing to go away.
- `:1265` — the Suggest-a-reply panel went from "Reading the chat and drafting…" to "WhatsApp has not loaded this chat's messages yet", so the assertion met the not-ready fallback instead of a draft. A readiness race, and the fallback copy is correct behaviour — the test simply arrived first.

**What to do with a red smoke run.** Read the whole output, not the tail (`v6-tail-one-hides-playwright-failures`), and get the failure set from **two** runs before believing either: a single run here names tests that are fine and clears tests that are not. Re-running one test alone proves nothing about it — `:536` passed in 4.1 seconds the moment it had the machine to itself. And never let an isolated re-run be the last thing that touched `test-results/`: it clears the failing run's `error-context.md`, which is where the evidence was.
