---
id: v6-no-alert-check-quiet-hours-first
date: 2026-09-13
agent: claude
title: No alert on the owner's PC is expected behaviour inside the quiet hours imported from v5
triggers: [notification, toast, alert, no alert, quiet hours, alerts.json missing, AppUserModelID, setAppUserModelId, Windows toast not shown]
files: [v6/core/alerts.ts, v6/app/main.ts, v6/installer.iss, v6/core/config.ts]
cost: An install at 02:20 logged reads but no alert and looked like a broken feature.
status: live
---

The owner's config came across from v5 with `quietHours` enabled from 21 to 11 local time (UTC+5). Inside that window `alertsDue` returns nothing and records nothing, so there is no `alert` line in `app.log` and no `alerts.json` — correct, not broken. When alerts seem missing, read `settings.quietHours` from the real config (through `Win32_Process`, the agent shell is sandboxed) and compare with local time before debugging. Outside quiet hours a missing toast is the next suspect: Windows shows an unpackaged app's toasts only for an AppUserModelID that a Start Menu shortcut carries, so `app.setAppUserModelId('UnifiedMessenger.v6')` must match `AppUserModelID:` on both shortcuts in `installer.iss`; a run from `npm start` or a sandboxed Playwright test has no such shortcut and may log `alert` without anything appearing. As of this lesson a toast had not yet been observed on the owner's PC.
