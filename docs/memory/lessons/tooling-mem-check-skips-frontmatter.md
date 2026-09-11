---
id: tooling-mem-check-skips-frontmatter
date: 2026-09-11
agent: claude
title: mem.ps1 check passes lessons with missing frontmatter and silently drops lessons with none
triggers: [mem.ps1, check, frontmatter, evidence, lesson format, malformed, CHECK OK]
files: [scripts/mem.ps1]
cost: All 10 seeded lessons lack evidence — the field that makes a lesson provable — and check reported CHECK OK; a lesson file with no frontmatter vanishes from mem and index without a warning.
evidence: `powershell -NoProfile -File scripts/mem.ps1 check` exit 0 on 2026-09-11 while `grep -l '^evidence:' docs/memory/lessons/*.md` matched 0 of 10; Get-Frontmatter returns $null and Get-Lessons filters it out
status: live
---

`check` validates two things: the always-loaded character budget and whether `files:` entries exist. The
bootstrap spec also asks it to catch malformed frontmatter, and it does not.

Fixed the same day: `check` now fails on a missing frontmatter block, a missing required field, an id that is
not the filename, a bad date, quoted `files`, and a `supersedes` target that does not exist. A missing
`evidence` is only a warning, because the ten lessons that lack it are immutable — a hard failure would have
made `check` fail forever on files nobody may edit. When a checker is added after its data, decide up front
which rules the existing data can never satisfy.
