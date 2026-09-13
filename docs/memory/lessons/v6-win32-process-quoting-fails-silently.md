---
id: v6-win32-process-quoting-fails-silently
date: 2026-09-13
agent: claude
title: A Win32_Process command line with nested quotes fails without an error; put the work in a .ps1 file
triggers: [Win32_Process, Invoke-CimMethod, Create, cmd /c, powershell -Command, quoting, output file missing, Remove-Item blocked, /VERYSILENT, protected from removal, agent shell]
files: [docs/revamp/roadmap.md]
cost: Four wasted round trips reading the owner's files; two install commands refused before they ran.
status: live
---

Reading or checking the owner's real files has to go through `Invoke-CimMethod Win32_Process Create` (lesson `v6-agent-shell-redirects-appdata-and-installs`), and that call only says whether a process started, not whether its command worked. Nested quoting fails quietly. Examples: a `cmd /c copy "…%LOCALAPPDATA%…" "D:\…"` whose destination never appeared, and a `powershell -Command "…''…''…"` loop that wrote nothing. The next step then reads a missing file or a stale one. `cmd /c "path with spaces" args` also breaks unless the whole thing is wrapped again as `cmd /c ""path" args"`. Write anything beyond one simple command into a `.ps1` under `D:\um-scratch`, run it with `powershell -NoProfile -ExecutionPolicy Bypass -File`, and have it write its result to a file you then read; delete the script afterwards. Separately, the agent's PowerShell tool refuses a whole command when it contains `Remove-Item` alongside an installer argument such as `/VERYSILENT`, which it reads as a protected path ("blocked … protected from removal"), so nothing in that command runs, the install included. Keep clean-up in its own call.
