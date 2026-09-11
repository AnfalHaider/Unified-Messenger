---
id: bootstrap-cursor-mcp-silent-drop
date: 2026-09-10
agent: cursor
title: Cursor silently drops MCP schemas past roughly 40 tools
triggers: [MCP, cursor, tool limit, silent, schema]
files: []
cost: Tools appear enabled in UI but the model never receives their schemas.
status: live
---

Prefer disabling unused MCPs over stacking more. Verify with a real call after enable. Host hooks outside the repo were not smoke-tested until backup approval — treat unverified host guards as untrusted.
