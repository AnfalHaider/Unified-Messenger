---
id: v6-a-refusal-test-can-pass-for-the-wrong-reason
date: 2026-09-19
agent: claude
title: A "this write is refused" test can pass because of a different rule; break the rules on purpose to check each one
triggers: [firestore rules, assertFails, rules test, emulator, security rules, mutation, refused, permission denied, 6.2]
files: [v6/cloud/firestore.rules, v6/cloud/rules.spec.ts]
cost: A test named "no promoting oneself" would have let a rule allowing exactly that ship.
evidence: 2026-09-19 mutation run: adding 'role' to the member's own changedOnly list left all 12 tests green until the test's write carried a valid lastSeen
status: live
---

All 12 rules tests passed on their first run. Breaking the rules on purpose, three ways, showed one test proving nothing: "a member cannot make themselves admin" wrote `{ role: 'admin' }` alone, and the member's own-update rule also demands `lastSeen == request.time`, so the write was refused for the missing time, and would still have been refused after the role check was deleted. `assertFails` only says *something* refused it. Write each refused case as the allowed write plus the one thing that must be refused (here `{ lastSeen: serverTimestamp(), role: 'admin' }`), and after writing rules tests, apply a few deliberate breaks (a `sed` on a copy, restore after) and confirm each turns a test red. The emulator's "evaluation error at Lx" lines in the output show which rule refused a write when a result is surprising.
