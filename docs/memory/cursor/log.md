# Cursor agent log

Newest entries on top. Append only — never edit an older entry.

## 2026-09-10 — Agent environment bootstrap (Phases 0–8)

- Mode: **MIGRATION** (AGENTS.md / CLAUDE.md / .cursorrules / docs already present).
- Multi-agent: Cursor + Claude (Co-Authored-By trailers in git history).
- Installed: .NET SDK 10.0.401, Python 3.12.10, Ollama 0.33.3, csharp-ls 0.27.0 (`+d48ab778…`).
- Disabled: Neon MCP in user `mcp.json` (backup under `.agent-bootstrap-backup-20260910-041530`).
- Scaffolded: `docs/memory/` + `scripts/mem.ps1`.
- Gate: `dotnet test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release` (kill app first).
- Do not push `main` or `v*` tags without explicit owner permission.
