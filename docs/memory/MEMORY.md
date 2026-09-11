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
- `python3` App Execution Alias still points at the Microsoft Store stub — use `python` (3.12.10) until aliases are disabled in Windows Settings.
- csharp-ls loads `UnifiedMessenger.sln` but emits WinUI metadata warnings until a full NuGet restore has succeeded in that environment.
- Neon MCP was removed from Cursor's `%USERPROFILE%\.cursor\mcp.json` only. As of 2026-09-11 it is still a **user-scope** server in `%USERPROFILE%\.claude.json` (loaded by every Claude session, write mode, plaintext bearer token), and `D:\Projects\taskmasterx.com` does use Neon — so the fix is to move it to that project's scope, not delete it. That move was denied by the permission classifier on 2026-09-11 and is still open. Owner decided **not** to rotate the key. See `bootstrap-mcp-removed-from-one-agent-only`.
- `.agent-bootstrap-backup-20260910-041530/` (2.1 GB, gitignored) holds unredacted copies of `.claude.json` and Cursor `mcp.json`. Owner decided 2026-09-11 **not** to delete it. Keep it out of any commit.
- Ollama is installed with **no models** yet (`ollama list` empty) — app Tier-2 AI still needs a pulled model.

## Agents (one writer per log)

| Agent | Log | Rule |
|---|---|---|
| cursor | `docs/memory/cursor/log.md` | Only Cursor appends; newest on top |
| claude | `docs/memory/claude/log.md` | Only Claude appends; newest on top |

## Lessons

See `lessons/` (immutable files) and generated `INDEX.md`.
