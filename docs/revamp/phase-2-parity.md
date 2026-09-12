# Phase 2 parity check — v6 core against v5, on the owner's real data

**Result (2026-09-12): every figure matched exactly.** The awaiting split (needs a reply, backlog, closed by
the rules, unreadable), the per-account chat and waiting counts, and the reply-time figures (sample count,
median, share within target) were identical between v5's own classes and the v6 port, run over the same
stored data at the same pinned instant.

**The figures themselves are not recorded here.** Waiting counts, backlog and reply times are oversight data
the app derives, and the hard constraint is that it never leaves this machine — this file is pushed to
GitHub, so it carries the method and the verdict only.

## Why it was done this way

Checking the port against itself proves nothing. So v5's own `OversightChatSnapshotService` and
`ResponseTimeTracker` were loaded with the same files v6 imported, through their real startup path
(`LoadAsync`, which applies v5's load-time guards: the non-customer filter, preview sanitising and the
cold-scan retraction), and their output was diffed against v6's.

Both sides were pinned to the same instant. This matters: an unpinned run put two chats on the other side of
the 7-day backlog cutoff, which reads as a discrepancy and is not one.

## How to re-run it

1. Copy the v5 store **from outside the agent's container** — the shell can be served a private copy of
   `%LOCALAPPDATA%` without saying so, and the two paths then return identical bytes either way:
   ```powershell
   Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine='cmd /c mkdir D:\um-v5-copy & copy /Y "%LOCALAPPDATA%\UnifiedMessenger\*.json" D:\um-v5-copy\'}
   ```
   Compare the listing it writes against what the shell sees before trusting either.
2. Import it into a scratch folder: `UM_SELFTEST=1 UM_DATA=D:/um-v6-check UM_V5=D:/um-v5-copy npm start`
   in `v6/`. v5's files are only ever read.
3. Add a temporary xUnit test that loads the same two files through v5's own services and writes its figures
   to a file, then run it with
   `dotnet test UnifiedMessenger.Tests/... --filter "FullyQualifiedName~V6PortComparison"`. The suite
   redirects the whole user-data root to TEMP, so the live store is never touched.
4. Diff the two sets with the same pinned "now", then **delete the copies, the scratch folder and the
   temporary test** — they hold real customer data.

## What this does and does not cover

Covered: the chat-entry parser, the reply-need classifier, the awaiting split and its backlog cutoff, the
per-account waiting counts, and the reply-time statistics — on real data, at scale, not on fixtures.

Not covered: anything with no v6 surface yet (Google reviews, message analytics, contact history, triage),
and the rollup's own grouping, which has unit tests but no v5 equivalent to diff against since v6 computes it
from snapshots where v5 used its thread registry.

## What the real data exposed

v5 had no branch key set on any account, so v6 guessed every location from the account name. Two accounts for
the same branch produced two spellings, and the rollup groups by the name as written — that branch would have
appeared twice. Fixed in `parseConfig` (commit `f7a2bfb`), which is the one funnel every config passes
through. Synthetic fixtures would never have produced it.
