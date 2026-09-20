---
id: v6-installer-must-wait-for-a-slow-quit
date: 2026-09-20
agent: claude
title: The installer's 20-second wait was shorter than a real shutdown, so it installed under the running app and left nothing open
triggers: [installer, install, relaunch, no startup, single instance, --quit, WaitUntilClosed, installer.iss, update, 7.3]
files: [v6/installer.iss]
cost: One install on the owner's PC ended with the app closed and no sign of why; the log's last line was a quit 88 seconds after the files were replaced.
evidence: 2026-09-20 files written 20:29:56, quit logged 20:31:24 (88 s), no startup; after raising the wait to 120 s, quit 20:43:42 and startup 20:44:25, reads resumed
status: live
---

`CloseRunningApp` asked the app to quit and waited 20 seconds. A shutdown closes every account page in turn, and six of them took 88 seconds on the owner's PC, so the installer gave up, fell through to `taskkill` (which the app also does not obey at once), replaced the files under the still-running copy, and ran its post-install launch — which met the old copy still holding the single-instance lock and exited. The result is an app that is simply not running, with nothing in the log to explain it.

The wait is now two minutes, and the sequence is worth remembering when reading `app.log`: a `quitting` line **after** the installed files' timestamp means exactly this race. It matters more now that updates (7.3) run the same Setup silently from inside the app: there the app quits itself first, so the installer finds nothing running, but a customer running Setup by hand takes this path.
