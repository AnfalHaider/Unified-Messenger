---
id: v6-v5-retired-lives-in-git-history
date: 2026-09-20
agent: claude
title: The v5 app is retired; its files are gone from the tree but its lessons still apply, so the memory check counts those paths instead of failing on them
triggers: [retire v5, UnifiedMessenger, WinUI, dotnet, AGENTS.md, stale files, mem.ps1 check, 7.6, installer.iss]
files: [AGENTS.md, scripts/mem.ps1, README.md, THIRD-PARTY-NOTICES.md]
evidence: 2026-09-20 637 files removed with git rm; mem check reports "71 reference(s) to the retired v5 app"; v6 suites unchanged at 352 unit and 31 on screen
cost: None, because the deletion went through git rather than the filesystem: every file is one `git show` away.
status: live
---

v5 (WinUI 3 / .NET) was retired at 7.6: `UnifiedMessenger/`, its tests, its UI smoke tests, the four Inno scripts, the solution, `Directory.Build.props`, `third_party/ollama`, the Ollama fetch script and the two v5 CI workflows. `AGENTS.md`, `README.md`, `THIRD-PARTY-NOTICES.md` and `.cursorrules` were rewritten for v6, and the v5 planning docs (`MASTER-PLAN.md`, `remaining-work.md`, `phase-status.md`) each carry a banner saying they are history, because the reasoning behind many product decisions was written there first.

Two things worth knowing next time a tree is retired:

- **Delete through `git rm`, not the filesystem.** Everything stays recoverable by SHA, and the agent harness refuses a bare `rm -rf` of a large tracked tree for exactly that reason. Untracked build output (`bin/`, `obj/`, `dist/`) stays behind on disk; that is the owner's to clear.
- **27 lessons name v5 files, and they are still true** (a failed read reading as "caught up", check-then-act across an await, a semaphore that is not a throttle). Rather than supersede them — which would hide them from `mem` — `scripts/mem.ps1 check` now matches those paths against a retired-tree pattern and reports them as a count: `NOTE: 71 reference(s) to the retired v5 app`. A lesson's worth is its reasoning, not whether its file still exists.
