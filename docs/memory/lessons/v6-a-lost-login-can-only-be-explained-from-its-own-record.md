---
id: v6-a-lost-login-can-only-be-explained-from-its-own-record
date: 2026-09-18
agent: claude
title: A lost login can only be explained from a record written as it happened, and the screen must follow that record rather than this minute's flags
triggers: [lost login, signed out, reader timeline, events.json, reading record, what happened, sign-in screen, timeline, seeded store in Playwright]
files: [v6/core/events.ts, v6/app/main.ts, v6/ui/screens/accounts.tsx, v6/tests/smoke.spec.ts]
cost: Found while building 4.10; the live state knows an account is signed out but nothing about the minutes that led there.
status: live
---

The running state answers "is this account signed out" and nothing else: the reads that came before it, the page reloading itself, the reader stalling, are gone the moment the next read replaces them. So the lost-login and reader screens cannot be computed — they need their own record, and `core/events.ts` writes one outcome per read (`events.json`, 60 per account, counts and stages only, saved as it happens so a sign-out at midnight is explained in the morning). Two consequences worth keeping:

- **The screen follows the record, not the flags.** Just after a restart the `signedOut` set is empty because nothing has been read yet, so a headline keyed on it says "is signed in" above a timeline ending in "Still signed out". Both screens key on `signedOutSince(events, id)` instead.
- **Runs must collapse when drawn, not when written.** Storing "12 good reads" as one row loses the times; collapsing at draw time (`collapse` in `events.ts`) keeps the file honest and the screen short.

A Playwright note from the same step: a test that seeds a store the app also writes to will find the app's own lines mixed in with the seeded ones by the time it asserts — the reading-record test seeds `events.json`, and the running app appends `awake` and `not-ready` for its `about:blank` accounts within seconds. Assert on the seeded lines and on relative ordering, never on the whole list or on the absence of a line the app could legitimately add ("Nothing read for N minutes" was such an assertion, and it was wrong because reading was working).
