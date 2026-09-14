---
id: v6-tail-one-hides-playwright-failures
date: 2026-09-14
agent: claude
title: Playwright prints "N failed" above "N passed", so tail -1 reports a failing run as green
triggers: [playwright, smoke, tail -1, passed, failed, test summary, false green, npm run smoke]
files: [v6/tests/smoke.spec.ts]
cost: A failing test almost went into a commit and an install; caught only because 8 passed did not match 9 tests.
status: live
---

Playwright's line reporter ends with the failures first and the pass count last: `1 failed`, the failing test's name, then `8 passed (41.5s)`. Piping the run through `tail -1` or `tail -2` shows only the pass line, so a red run reads as green. Filter for every summary line instead, `grep -E "^\s+[0-9]+ (passed|failed|flaky|skipped)"`, or check the count against the number of tests. The failure here was a test still expecting wording a change had replaced ("Recording since"): any change to on-screen sentences should be followed by a search of `tests/` for the old text.
