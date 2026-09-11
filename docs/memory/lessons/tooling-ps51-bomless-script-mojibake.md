---
id: tooling-ps51-bomless-script-mojibake
date: 2026-09-11
agent: claude
title: Windows PowerShell 5.1 decodes a BOM-less UTF-8 script as ANSI and corrupts its non-ASCII literals
triggers: [PowerShell 5.1, BOM, encoding, mojibake, em dash, mem.ps1, generated file, Set-Content]
files: [scripts/mem.ps1]
cost: Both generated memory files ship a corrupted do-not-edit header; the defect is in the generator, so hand-fixing the output is both forbidden and undone by the next run.
evidence: scripts/mem.ps1 has no BOM; docs/memory/INDEX.md and docs/memory/LAST_REPORT.md contain bytes C3 A2 E2 82 AC (an em dash decoded as cp1252 and re-encoded as UTF-8), measured 2026-09-11
status: live
---

`powershell.exe` 5.1 — this machine's default shell — reads a script with no byte-order mark in the system ANSI
code page. The em dash in a string literal becomes three Latin-1 characters, and `Set-Content -Encoding UTF8`
then faithfully writes those. Text read at runtime with `Get-Content -Encoding UTF8` is unaffected; only
literals inside the script are.

Keep scripts that 5.1 runs ASCII-only, or save them with a UTF-8 BOM. Then re-run the generator rather than
editing its output. `mem.ps1` is now ASCII-only, writes UTF-8 without a BOM, and `check` fails if a non-ASCII
byte ever appears in it again.
