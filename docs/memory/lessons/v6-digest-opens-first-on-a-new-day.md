---
id: v6-digest-opens-first-on-a-new-day
date: 2026-09-14
agent: claude
title: The first window opening of each day lands on the morning digest, so a test or check expecting the line must allow for it
triggers: [morning digest, digest, first open, heading not found, customers are waiting, Playwright, smoke test, digest.json, ready, showWindow]
files: [v6/app/main.ts, v6/core/digest.ts, v6/tests/smoke.spec.ts]
cost: Would have failed every smoke test with accounts the moment the digest was switched on by default.
status: live
---

`settings.morningDigest` is on by default. The first time the main window is shown on a local day (app start, or back from the tray) with at least one readable account, main sends `open digest` and records the day in `digest.json`. Anything that launches the app and expects "N customers are waiting" on the first screen will instead see "Good morning…". The Playwright helper `dataFolder` sets `morningDigest: false` unless a test passes its own settings; a check against the owner's installed app should expect the digest on the day's first launch. The trigger listens for `ready` only from the main window's webContents, because the hidden report window sends `ready` too. Deleting `digest.json`, or a new local day, shows it again.
