---
id: bootstrap-recorded-machine-facts-decay
date: 2026-11-07
agent: claude
title: Two open threads about this machine had gone false while being trusted — re-probe a recorded version before relying on it
triggers: [open threads, MEMORY.md, python, python3, store stub, ollama, models, stale memory, re-probe, machine facts, bootstrap, phase 0]
files: [docs/memory/MEMORY.md, scripts/mem.ps1]
cost: Two of six open threads were wrong after six weeks; `check` cannot catch either, because both name a fact outside the repository.
evidence: 2026-11-07 — MEMORY.md said "use python (3.12.10)" but `python --version` is 3.10.11 (Python310 first on PATH; Python312 also installed); MEMORY.md said Ollama had "no models (`ollama list` empty)" but `ollama list` returns gemma3:4b, gemma3:1b, llama3.2:3b.
status: live
---

`scripts/mem.ps1 check` guards the repository's own memory: frontmatter, `supersedes` targets, `files` entries that no longer exist, the context budget. It cannot guard a sentence about the machine. Six weeks after they were written, two of the six open threads in `MEMORY.md` were false:

- **"use `python` (3.12.10)"** — `python` resolves to 3.10.11. Python 3.12.10 is still installed, one directory along, but is not first on PATH. The surrounding fact (the `python3` App Execution Alias is still a Microsoft Store stub that prints an error and exits non-zero) was still true, which is what makes this kind of drift easy to miss: the interesting half of the line held, and the version in brackets did not.
- **"Ollama is installed with no models yet"** — three models are pulled, `gemma3:4b` among them, the one the Phase-1 proof scored. A whole open thread ("app Tier-2 AI still needs a pulled model") had closed itself without anyone writing it down.

A recorded version number, path or "is not installed yet" is a probe result with a date on it, not a fact. The rule that costs nothing: when an open thread names something outside the repository, **run its probe before you act on it**, and write the re-probe date into the line — `python --version`, `ollama list`, `ls ~/.jdks` are one command each. A lesson in `lessons/` is immutable and gets superseded; `MEMORY.md`'s open threads are the opposite and must be corrected in place, with the date, the moment a probe disagrees with them.
