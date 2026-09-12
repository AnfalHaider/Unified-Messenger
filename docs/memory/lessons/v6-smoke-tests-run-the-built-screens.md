---
id: v6-smoke-tests-run-the-built-screens
date: 2026-09-13
agent: claude
title: The Electron smoke test draws dist-ui, so a UI edit without vite build tests the old screens
triggers: [playwright, smoke, electron, _electron.launch, dist-ui, vite build, screenshot unchanged, testMatch, closeToBackground, app.close hangs]
files: [v6/tests/smoke.spec.ts, v6/playwright.config.ts, v6/app/main.ts]
cost: A layout fix looked like it had no effect across two screenshot rounds.
status: live
---

`app/main.ts` loads `dist-ui/index.html`, not `ui/`. After editing anything under `ui/`, run `npx vite build` (or `npm run smoke`, which builds first) before `npx playwright test`, or the test and any screenshot show the previous screens and the fix looks like it did nothing. Two more traps in the same harness: Playwright's default `testMatch` also matches the `*.test.ts` files that belong to `node --test`, so `playwright.config.ts` pins `testDir: 'tests'`; and with the default `closeToBackground: true` a window close only hides the app, so a test must seed `settings.closeToBackground: false` and quit through the Quit button, or the process never ends. Seed a temp `UM_DATA` with a `config.json` so no v5 import runs, and point an account's `url` at `about:blank` so a seeded snapshot is never replaced by a real read.
