---
id: v6-firestore-query-rules-cannot-see-ids
date: 2026-09-19
agent: claude
title: A Firestore rule for a query cannot test the document id; and spec files sharing one emulator must run one at a time
triggers: [firestore rules, collection group, query, list, Null value error, runQuery, emulator, test-concurrency, clearFirestore, members, invites]
files: [v6/cloud/firestore.rules, v6/cloud/sync.spec.ts, v6/scripts/rules-run.mjs]
cost: Two failed runs: one rule refused every membership query, and five good tests failed because two files cleared each other's data.
evidence: 2026-09-19 "Null value error. for 'list'" on `memberId == uid()` in a /{path=**}/members rule; 14 of 19 until --test-concurrency=1, then 19 of 19
status: live
---

Firestore checks a query's rule against the query's filters, not against each document, so a condition on the document id (`memberId == uid()` in `match /{path=**}/members/{memberId}`) cannot be proven and the whole query is refused with "Null value error. for 'list'". Test a stored field the query filters on instead (`resource.data.email == myEmail()`, queried with `where('email', '==', …)`), and make sure only the right person can ever store that value (here, only the member themself can create their entry, with their own verified address). The app still checks the id on what comes back.

`node --test` runs test files in parallel. `cloud/rules.spec.ts` and `cloud/sync.spec.ts` share one emulator and each test calls `clearFirestore()`, so running both at once failed tests that pass alone. `scripts/rules-run.mjs` passes `--test-concurrency=1`.
