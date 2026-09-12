---
id: v6-smart-app-control-block-can-lift
date: 2026-09-13
agent: claude
title: A Smart App Control block on the unsigned v6 Setup can lift by itself; retry the same file later, never a rebuilt one
triggers: [install-local, Setup.exe, installer did nothing, Win32_Process, ReturnValue 8, Smart App Control, Code Integrity, 3077, 3033, unsigned, code signing, no startup in app.log]
files: [v6/scripts/dist.mjs, v6/installer.iss]
cost: About twenty minutes of polling for a restart that could not come; the install then waited two hours.
supersedes: [v6-smart-app-control-blocks-unsigned-setup]
status: live
---

The owner's PC runs Windows Smart App Control, which judges an unsigned `UnifiedMessenger6Setup.exe` by cloud reputation. The same night it allowed three builds, blocked the fourth five times, then allowed that identical, unchanged file about two hours later. Symptoms of a block: `Invoke-CimMethod Win32_Process Create` returns `ReturnValue 8` with no process id (piping it to `Out-Null` hides that), no Setup process appears, and `app.log` shows the old copy still reading with no `quitting` or `startup`; through `cmd /c` the launch returns 0 and writes nothing. Proof: `Microsoft-Windows-CodeIntegrity/Operational` events 3033 and 3077 ("did not meet the Enterprise signing level requirements", naming the Setup path) and 3118 "Smart App Control Block". Always print the Create return value after an install. When blocked, tell the owner and retry the same file later. Do not rebuild to get a new hash or change security settings to get past it; the durable fix is code signing (roadmap 7.4), the owner's decision.
