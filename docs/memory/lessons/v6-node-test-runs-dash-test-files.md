---
id: v6-node-test-runs-dash-test-files
date: 2026-09-19
agent: claude
title: node --test runs any file named *-test.ts, not only *.test.ts; a script with that name becomes a test
triggers: [node --test, npm test, test glob, assistant-test, assistant-eval, flaky, CI red, ollama]
files: [v6/scripts/assistant-eval.ts, v6/package.json]
cost: CI went red on a push, and the failure was reported to the owner as "one that did not repeat" when it was one that depended on Ollama running.
status: live
---

`node --test` with no arguments matches `**/*.test.*`, `**/*-test.*`, `**/*_test.*`, `**/test-*.*` and `**/test.*`. The real-model run for the assistant (5.5) was named `scripts/assistant-test.ts`, so `npm test` ran it: it passed while an Ollama was up (and made the suite take 170 s instead of under a second), and failed as soon as Ollama stopped and on CI, where there is none. It looked like an intermittent failure; it was a fixed dependency on a running program. It is now `scripts/assistant-eval.ts` (`npm run assistant:test` still runs it). Name scripts so they match none of those patterns, and when "one test fails, then passes", read which test before calling it flaky: `grep -B2 -A14 "^✖"` on the saved output names it.
