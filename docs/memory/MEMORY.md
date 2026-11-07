# Agent memory index

Cross-session memory for Unified Messenger. **Retrieval, not re-reading AGENTS.md.**

## How to use

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 mem "<topic>"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 check
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 index
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 report
```

- Keep `-ExecutionPolicy Bypass`: without it a plain shell refuses to load the script (every policy scope here is Undefined). It is process-scoped and changes no setting.
- `mem` prints **only** live lessons whose triggers match; a lesson named in another lesson's `supersedes` is skipped.
- `check` fails on: always-loaded rules over the budget ceiling; a lesson with no frontmatter, a missing required field, an id that is not its filename, a non-`YYYY-MM-DD` date, or quoted `files`; a `supersedes` target that does not exist; a `files` entry that no longer exists; any non-ASCII byte in `mem.ps1`. A missing `evidence` field is a warning.
- To correct a lesson, add a new one with `supersedes: [old-id]`. Never edit the old file.
- `INDEX.md` and `LAST_REPORT.md` are **generated** — re-run the script; do not hand-edit.

## Open threads

- `FIRECRAWL_API_KEY` sits in plaintext in `.env` (gitignored). Owner decided 2026-09-11 **not** to rotate it — do not raise it again unless it leaks.
- `python3` App Execution Alias still points at the Microsoft Store stub (`--version` prints the Store message and exits non-zero) — use `python`. **Re-probed 2026-11-07: `python` is 3.10.11** at `…\Programs\Python\Python310`; the 3.12.10 this thread used to name is installed at `…\Python312` but is not first on PATH. Probe, do not trust this line.
- csharp-ls (0.27.0) and .NET 10.0.403 are still installed, but v5 was retired on 2026-09-20 and `UnifiedMessenger.sln` is no longer in the working tree (only gitignored `bin`/`obj` leftovers). Neither is part of the v6 gate; treat both as reference tooling for reading v5 in git history.
- Neon MCP was removed from Cursor's `%USERPROFILE%\.cursor\mcp.json` only. As of 2026-09-11 it is still a **user-scope** server in `%USERPROFILE%\.claude.json` (loaded by every Claude session, write mode, plaintext bearer token), and `D:\Projects\taskmasterx.com` does use Neon — so the fix is to move it to that project's scope, not delete it. That move was denied by the permission classifier on 2026-09-11 and is still open. Owner decided **not** to rotate the key. **Re-probed 2026-11-07: still user-scope** (`mcpServers.Neon` in `%USERPROFILE%\.claude.json`; this project's own scope has none), and its ~150 tool names ride in every Claude session here. See `bootstrap-mcp-removed-from-one-agent-only`.
- `.agent-bootstrap-backup-20260910-041530/` (2.1 GB, gitignored) holds unredacted copies of `.claude.json` and Cursor `mcp.json`. Owner decided 2026-09-11 **not** to delete it. Keep it out of any commit.
- Ollama: **re-probed 2026-11-07, three models are pulled** — `gemma3:4b` (3.3 GB), `gemma3:1b` (815 MB), `llama3.2:3b` (2.0 GB). This thread previously said `ollama list` was empty; that is no longer true. `gemma3:4b` is the one the Phase-1 proof scored.
- Java: only Temurin **17** is on PATH (`C:\Program Files\Eclipse Adoptium`). The portable **JDK 21.0.12.1+1** that `npm run rules:test` needs is still in `%USERPROFILE%\.jdks` — confirmed present 2026-11-07. See `v6-firestore-emulator-needs-java-21`.
- `.agent-bootstrap-backup-20260910-041530/` was **not found** on 2026-11-07 in the repository root, `%USERPROFILE%`, `D:\`, `D:\Projects`, OneDrive or `%LOCALAPPDATA%` (top level of each). It may have been moved or deleted; its whereabouts are UNKNOWN. If it still exists it holds unredacted `.claude.json` and Cursor `mcp.json` — keep it out of any commit.

## Agents (one writer per log)

| Agent | Log | Rule |
|---|---|---|
| cursor | `docs/memory/cursor/log.md` | Only Cursor appends; newest on top |
| claude | `docs/memory/claude/log.md` | Only Claude appends; newest on top |

## Lessons

See `lessons/` (immutable files) and generated `INDEX.md`.
