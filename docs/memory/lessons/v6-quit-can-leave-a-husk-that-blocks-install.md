---
id: v6-quit-can-leave-a-husk-that-blocks-install
date: 2026-09-13
agent: claude
title: A v6 quit can log "quit" and leave the main process alive, and the installer then silently installs nothing
triggers: [installer did nothing, Setup exits, app not reading, quit, will-quit, husk, zombie process, tasklist, IsRunning, WaitUntilClosed, --quit, UnifiedMessenger6.exe still running]
files: [v6/app/main.ts, v6/installer.iss]
cost: The owner's app stopped reading for about eight hours overnight; one install silently did nothing.
status: live
---

Seen once on the owner's PC: a copy started by the installer was quit with `--quit` about two minutes after launch, while WhatsApp was still loading. `app.log` showed `quitting`, `pages-closed` (no stuck pages), `will-quit` and `quit` code 0, yet the main `UnifiedMessenger6.exe` stayed alive for hours with no child processes, no window and no single-instance lock (a new copy started normally beside it). The next `/VERYSILENT` install asked the running copy to quit, which exited, but `installer.iss` `IsRunning()` matches any process by image name, so it saw the husk, waited out its 20 s and 30 s limits and aborted without a message. Nothing reopened the app. How to check: after an install, look for a `startup` line in `app.log`; if it is missing, list `UnifiedMessenger6.exe` processes with `CommandLine` not containing `--type=` and compare `CreationDate` with the last `quit` in the log. A main process with no children that already logged `quit` has no pages writing storage; with the owner's permission it was ended with `Win32_Process Terminate` and the install then worked. The cause is unknown; later quits exited normally.
