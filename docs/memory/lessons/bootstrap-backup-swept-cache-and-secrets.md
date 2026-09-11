---
id: bootstrap-backup-swept-cache-and-secrets
date: 2026-09-11
agent: claude
title: A directory-level config backup copied 2 GB of cache and duplicated credentials into the repo folder
triggers: [backup, R6, agent-bootstrap-backup, mcp.json, claude.json, secrets, gitignore, disk]
files: [.gitignore]
cost: 1,858 files / 2.1 GB landed in the repo root, 1,853 of them Cursor agent-worker JS cache; copies of two token-bearing configs now live in a second place that is one .gitignore edit away from a commit.
evidence: .agent-bootstrap-backup-20260910-041530/ measured 2026-09-11 — C__Users_anfal_.claude.json, C__Users_anfal_.cursor_mcp.json, plus ...Cursor_User_globalStorage/anysphere.cursor-agent-worker/... ; ignored only by the `.agent-bootstrap-backup-*/` rule
status: live
---

R6 says back up what you will modify. The change was one entry in one `mcp.json`; the backup recursed into a
Cursor storage directory and took its worker cache with it.

Back up the exact files you will write, by path. Treat a backup of credential-bearing config as a secret in its
own right: keep it outside the repository tree, and when the token it contains is rotated, the backup stops
being a rollback and becomes a leak — delete or re-take it then.
