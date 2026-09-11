---
id: bootstrap-reject-neon-mcp
date: 2026-09-10
agent: cursor
title: Neon MCP is wrong stack and burns Cursor MCP slots
triggers: [neon, MCP, postgres, context budget, silent drop]
files: []
cost: Dozens of Neon tool schemas in every request for a local WinUI app with no database.
status: live
---

This product has no Neon/Postgres. Cursor drops MCP tools silently past ~40 active. Neon was the only entry in user `mcp.json` and was removed. Rollback: restore from `.agent-bootstrap-backup-20260910-041530`. Reload MCP after changes.
