---
id: tooling-mem-ps1-blocked-by-execution-policy
date: 2026-09-11
agent: claude
title: The documented mem.ps1 command fails from any shell that does not bypass execution policy
triggers: [mem.ps1, execution policy, running scripts is disabled, UnauthorizedAccess, powershell -File, Git Bash, memory command]
files: [scripts/mem.ps1, AGENTS.md]
cost: The memory system's only entry point exits 1 from Git Bash on this machine, so an agent following AGENTS.md retrieves no lessons and sees an error that reads like a broken script.
evidence: 2026-09-11 — `Get-ExecutionPolicy -List` shows every scope Undefined (Windows client default: Restricted); `powershell -NoProfile -File scripts/mem.ps1 mem publish` from Git Bash → "cannot be loaded because running scripts is disabled on this system", exit 1; the same invocation from Claude Code's PowerShell tool → exit 0; no Zone.Identifier stream on the script
status: live
---

The command was verified from one host and written down as though it worked from all of them. Claude Code's
PowerShell tool runs it; a plain `powershell.exe` launched from Git Bash refuses to load any script at all.
A success from one agent's shell is not evidence for another agent's shell.

Write the command as `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 <command>`. A
`-ExecutionPolicy` argument is process-scoped and changes no machine setting. Do not "fix" it with
`Set-ExecutionPolicy`, which is a persistent security-control change. AGENTS.md, `.cursorrules`, `MEMORY.md`
and the script's own help now all carry the Bypass form; verified from Git Bash on 2026-09-11 (without it:
exit 1; with it: exit 0).
