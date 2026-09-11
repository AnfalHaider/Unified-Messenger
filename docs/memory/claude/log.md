# Claude agent log

Newest entries on top. Append only — never edit an older entry.

## 2026-09-12 — v6 revamp decided; Phase 1 proof build done

- Owner decisions: v6 rebuilt as Electron + TypeScript + React, ponytail code; Firebase Spark only for sign-in, membership and configuration; access by membership; AI assistant off by default. Rules changed in AGENTS.md (`dae053b`) and MASTER-PLAN (D-11–D-14). Single branch `main`, no co-author trailers.
- Artifacts: "Revamp Blueprint" (stack, rules, roadmap) and "Unified Messenger v6 Screens" (39 artboards incl. title bar).
- Phase 1 proof build outside the repo (`D:\Projects\um-v6-proof`): steps 1, 2, 3, 5, 6, 8 passed; 4 skipped by owner; 7 reduced to `gemma3:4b` (7/10). Results: `docs/revamp/phase-1-proof.md`.
- Firebase project `unified-messenger-5549a` created through the owner's Chrome: Analytics and Gemini off, Google provider on, Desktop OAuth client. Credential files stay in the proof folder.
- Found: two Ollama installs with separate model folders (start order decides which model is reachable); Chrome downloads land on the OneDrive Desktop, so the OAuth client JSON reached OneDrive before being moved.
- Not done: Neon MCP scope move (blocked by the permission classifier earlier); 2.3 GB of partial `phi4-mini` download left in `%USERPROFILE%\.ollama\models\blobs`.

## 2026-09-11 — Bootstrap Phases 6–7 (seed, prove) and mem.ps1 fixes

- Found Cursor's 2026-09-10 bootstrap present and working but uncommitted; skipped Phases 2–5.
- Seeded 34 lessons: 5 from this session's Phase 0 findings, 29 mined by 6 subagents from 6 Claude sessions (69.4 MB, 0.60 MB of message text), 1 Cursor transcript (Cursor `agent-tools/` is cached text, not transcripts), and 39 correcting commits plus 7 CHANGELOG retractions. 4 duplicates dropped before placement. Privacy-scanned: no customer names, handles or numbers.
- `scripts/mem.ps1`: ASCII-only, UTF-8 output without BOM, `check` now validates frontmatter, `supersedes` targets and the script's own encoding; new `supersedes: [id]` field is the way to correct an immutable lesson. Documented command now carries `-ExecutionPolicy Bypass` (AGENTS.md, `.cursorrules`, MEMORY.md).
- Gate on `8e4ab6d`: 2204/2204 passed locally; CI verify, package and ui-smoke green at that sha.
- Failed step, reported: while proving `check` in a scratchpad mirror, `.env` was copied because a lesson lists it in `files`. Never printed; replaced with an empty stub.
- Blocked: moving the Neon MCP from user scope to `taskmasterx.com` project scope in `%USERPROFILE%\.claude.json` was denied by the permission classifier. Owner to rotate the key.
- Nothing committed. Open: AGENTS.md forbids `Co-Authored-By` trailers, the Claude harness adds one — resolve before any commit.

## 2026-09-10 — Bootstrap noted (Cursor-run)

Cursor ran the agent-environment bootstrap on this repo. Claude sessions should:

- Read `AGENTS.md` first (via `CLAUDE.md` → `@AGENTS.md`).
- Use `scripts/mem.ps1 mem <topic>` before re-deriving gotchas already captured as lessons.
- Append here when Claude completes a material session; do not edit Cursor’s log.
- Never add `Co-Authored-By` trailers to commits in this repo (AGENTS.md commit convention).
