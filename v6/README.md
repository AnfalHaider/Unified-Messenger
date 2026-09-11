# Unified Messenger v6

The Electron + TypeScript rebuild (MASTER-PLAN D-11). The v5 app in `UnifiedMessenger/` keeps shipping
until v6 reaches parity. Roadmap: the "Revamp Blueprint" artifact; Phase 1 results: `docs/revamp/phase-1-proof.md`.

| Folder | Holds |
|---|---|
| `core/` | Pure logic: who is waiting, reply times, SLA, rollups. Every number the UI shows is computed here. |
| `app/` | Electron main process: window, tray, account sessions, read scheduler, updates. |
| `channels/<name>/` | One module per channel: reader, find-a-chat, health check, tests. |
| `ui/` | React screens. |
| `assistant/` | Local Ollama chat. Off by default. |
| `cloud/` | Firebase sign-in, membership and configuration sync. |

Folders appear when their first file does.

```
npm install
npm test          # node --test, runs *.test.ts directly (Node strips the types)
npm run typecheck # tsc --noEmit
```

Rules: follow the ponytail guideline (built-ins before dependencies, no abstractions without a second
user); port v5 behaviour with its test cases before changing it; never commit `firebase-config.json`,
`oauth-client.json` or any other credential.
