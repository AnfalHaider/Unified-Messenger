---
id: process-harness-trailer-vs-repo-rule
date: 2026-09-13
agent: claude
title: The Claude harness asks for a Co-Authored-By trailer that AGENTS.md forbids
triggers: [commit, Co-Authored-By, trailer, attribution, commit convention]
files: [AGENTS.md]
cost: Pushed commits on main carry the trailer, including three from 2026-09-12 and 2026-09-13.
status: live
---

AGENTS.md's commit convention forbids `Co-Authored-By` and tool-attribution trailers, and the owner's standing memory repeats it. The harness's attribution reminder asks for one on every commit. The repo rule wins: leave the trailer out. Rewriting already-pushed history on `main` needs the owner's explicit go-ahead.
