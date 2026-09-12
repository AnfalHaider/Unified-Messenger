---
id: v6-force-kill-damages-sessions-and-hangs-quit
date: 2026-09-13
agent: claude
title: Killing Electron with account pages open damages their storage, and damaged storage is what makes quit hang
triggers: [electron, quit, app.exit, hang, taskkill /F, SIGKILL, process.kill, WhatsApp logged out, QR code, IndexedDB Consistency Error, WebContentsView, close]
files: [v6/app/main.ts, v6/installer.iss]
cost: Two live WhatsApp logins lost after a forced restart; a day of wrongly blaming Electron and shipping a self-kill workaround.
status: live
---

A `WebContentsView`'s page is not destroyed with its window. Shut down by closing each page, awaiting its `destroyed` event, then `app.quit()`. Never end the process (`process.kill`, `taskkill /F`, `Stop-Process`) while pages are open: a page stopped mid-write leaves its IndexedDB damaged, the next launch shows a QR code, and later quits hang in native teardown after `quit` fires. The "Electron will not exit" symptom measured in the agent sandbox was that damaged storage; a clean data folder exits with code 0 in a second. To stop a running v6 from a script, run `UnifiedMessenger6.exe --quit`; `taskkill` without `/F` only hides it now that closing goes to the tray.
