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

## What is in `core/` today

Each module is a port of the v5 logic named beside it, with v5's own test cases. Nothing here touches the
disk, the network or the clock without being told the time: stores are plain objects that the `app/` layer
saves as JSON, and "now" is always a parameter, which is what makes every rule testable.

| Module | What it decides | Ported from |
|---|---|---|
| `chat-entry` | A channel's scan JSON becomes chat entries; a bad row costs only itself. | `ChatEntryParser` |
| `reply-need` | Whether a customer's last message still needs an answer, and why in plain English. | `ReplyNeed` |
| `snapshot` | The one "is this chat waiting" rule, plus windowed counts, the awaiting split and the digest. | `OversightChatSnapshotService` |
| `rollup` | Per account or per location: caught up, waiting, past target, at risk, worst first. | `OversightRollupBuilder` |
| `response-times` | First response time, measured forward from what we actually see happen. | `ResponseTimeTracker` |
| `awaiting-overrides` | Marked handled or snoozed, both expiring on their own. | `AwaitingOverrideStore` |
| `business-hours` | Elapsed minutes inside a location's working hours. | `BusinessHoursCalculator` |
| `days` | Local calendar days that survive a clock change. | `LocalDayBoundary` |
| `percent` | A percentage that never rounds up to 100 or down to 0. | `MetricMath` |
| `freshness` | How old the numbers are, said the way a person would. | `DataFreshness` |
| `config` | Accounts, locations and settings in one object; parsing never throws. | `AppSettings` + `InstanceRegistryService` |
| `import-v5` | A v5 install's files become a v6 config, with a report of what was guessed or left behind. | — |
| `schedule` | Which account to read next, what may sleep, when to stay quiet. | `InstanceSessionManager` + `OversightAlertMonitor` |

Rules: follow the ponytail guideline (built-ins before dependencies, no abstractions without a second
user); port v5 behaviour with its test cases before changing it; never commit `firebase-config.json`,
`oauth-client.json` or any other credential.
