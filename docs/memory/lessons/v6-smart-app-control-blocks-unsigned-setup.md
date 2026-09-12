---
id: v6-smart-app-control-blocks-unsigned-setup
date: 2026-09-13
agent: claude
title: Smart App Control can block a freshly built unsigned Setup.exe, and Win32_Process.Create only says "8"
triggers: [install-local, Setup.exe, installer did nothing, Win32_Process, ReturnValue 8, Smart App Control, Code Integrity, 3077, 3033, unsigned, code signing, no startup in app.log]
files: [v6/scripts/dist.mjs, v6/installer.iss]
cost: About twenty minutes of polling a log for a restart that could never come, and one install that did not happen.
status: live
---

The owner's PC runs Windows Smart App Control. It decides per file from cloud reputation, so an unsigned `UnifiedMessenger6Setup.exe` can install fine three times in one night and then be blocked on the fourth build. The symptoms: `Invoke-CimMethod Win32_Process Create` returns `ReturnValue 8` with no process id (piping it to `Out-Null` hides that), no Setup process appears, and `app.log` shows the old copy still reading with no `quitting` or `startup`. Through `cmd /c` the launch returns 0 but prints nothing and writes no `/LOG`. The proof is in `Microsoft-Windows-CodeIntegrity/Operational`: events 3033 and 3077 ("did not meet the Enterprise signing level requirements", naming the Setup path) and 3118 "Smart App Control Block". Always print the Create return value after an install. Do not try to get past the block by rebuilding for a new hash or changing security settings: that is the owner's decision, and the durable fix is code signing (roadmap 7.4).
