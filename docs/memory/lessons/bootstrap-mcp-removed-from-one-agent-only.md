---
id: bootstrap-mcp-removed-from-one-agent-only
date: 2026-09-11
agent: claude
title: Removing an MCP from one agent's config leaves it live in the other agent's
triggers: [neon, MCP, remove MCP, claude.json, mcp.json, multi-agent, R14, reject tool]
files: []
cost: Neon stayed loaded in every Claude Code session — write mode, destructive tools exposed, plaintext bearer token — a day after a lesson recorded it as removed.
evidence: 2026-09-11 probe — %USERPROFILE%\.claude.json user-scope mcpServers.Neon (type http, Authorization header present) while %USERPROFILE%\.cursor\mcp.json lists 0 servers
status: live
supersedes: [bootstrap-reject-neon-mcp]
---

The 2026-09-10 bootstrap removed Neon from Cursor's `mcp.json` and wrote `bootstrap-reject-neon-mcp` as
though that settled it. Claude Code reads a different file — user-scope servers in `%USERPROFILE%\.claude.json` —
and the entry is still there. The reject decision was right; its reach was assumed rather than probed.

The reach was wrong in the other direction too. "Wrong stack" was true of this repository, not of the machine:
`D:\Projects\taskmasterx.com` uses Neon (34 Neon tool calls in its Claude sessions, Neon settings in its
`.env.local`), so deleting a user-scope server to protect one repo breaks another. Pulling it out of Cursor's
user-scope `mcp.json` has probably already done that for Cursor sessions in taskmasterx.com (UNKNOWN, not tested).

When a tool is rejected, check every agent's config on disk, not just the one the rejecting agent runs in, and
check which other projects use it before removing it from a user-wide scope. Prefer moving it to the scope of
the project that needs it. That is a machine-scoped change to a credential-bearing file: back up with the token
redacted (R6), and rotate the token at the vendor, because an unredacted copy already sits in the bootstrap backup.
