---
id: bootstrap-session-store-mtime-is-not-session-date
date: 2026-11-07
agent: claude
title: A session file's mtime is not when its work happened, and 169 MB of transcripts held 1.8 MB of message text
triggers: [bootstrap, phase 6, mining, seed lessons, session history, transcripts, jsonl, agent history store, unmined, mtime, resumed session]
files: [docs/memory/MEMORY.md, docs/memory/claude/log.md]
cost: Would have started a multi-subagent mining run over 95 MB that held one message. Avoided by counting first.
evidence: 2026-11-07 — b9714842…jsonl, mtime 2026-11-07, 72 MB; per-day message counts show work 2026-09-12 to 2026-09-24, then 3 messages on 2026-11-07 (the interrupted /bootstrap-environment that this run retried). A filter from 2026-09-24T12:00Z kept exactly 1 message.
status: live
---

Re-running the bootstrap on this repository, the Claude store looked like six weeks of unmined history: eight `*.jsonl`, 169 MB, the two largest with mtimes of 2026-10-30 and 2026-11-07, against lessons that stopped at 2026-09-24. The mtimes were wrong about the work. One long session (`b9714842`) spans 2026-09-12 to 2026-11-07 because it was **resumed** today — its last real work is 2026-09-24, and its three messages from today are the `/bootstrap-environment` invocation the owner interrupted. Resuming a session rewrites its file; it does not add history.

Two things follow for Phase 6, and both are cheap:

1. **Get the date range from the records, not the filesystem.** A ~30-line extractor that prints, per file, the message count and the min/max `timestamp` answers "is there anything new here" in one run and costs no context. Compare that range against the newest lesson date — here `date:` stopped at 2026-09-24 and so did `git log`, which agreed, and the real answer was "nothing to mine".
2. **The recipe's figure holds.** Message text was 1,845,192 of 169,081,823 bytes — **1.09%**. The rest is tool output and file snapshots. An extractor that reads `message.content` text blocks only, skips anything under 120 characters, and requires at least two failure/decision/reversal signal matches turned 169 MB into one candidate. Never read a raw transcript into context to find this out.

The honest outcome of a Phase 6 with nothing new to mine is zero mined lessons, reported as zero — not a thin lesson manufactured to show the phase ran.
