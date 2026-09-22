---
id: v6-access-is-a-filter-on-what-leaves-not-on-what-is-shown
date: 2026-09-22
agent: claude
title: Per-member access filters the setup before it reaches their PC, so a login they may not have never arrives
triggers: [per-account access, setupFor, accountsAllowed, invitation accounts, member accounts, narrow access, hide accounts]
files: [v6/core/workspace-sync.ts, v6/app/workspace.ts, v6/cloud/firestore.rules]
cost: None yet; written while the feature was built, because the tempting shape is the wrong one.
status: live
---

The obvious way to give a member fewer accounts is to send them everything and hide the rest on screen. That
is the wrong shape for this app: the setup carries the accounts a PC then **signs in to**, so anything that
arrives is something that PC can hold a login for. Access is applied where the setup is read
(`setupFor(readSetup(doc), allowed)` in `pullNow`), before a single account reaches the config — so the
accounts a member was not given never exist on their machine at all.

Three details that each answer a real question:

- **Null is everything; an empty list is nothing.** An admin who ticks no boxes has said something, and it is
  not "give them the business". The picker keeps "the whole business" as its own choice rather than every box
  ticked, so an account added next month reaches them too — which is what the admin meant.
- **Narrowing has to force a re-apply.** Membership is checked far more often than the workspace's setup
  changes, so comparing the setup's `updateTime` alone would leave a removed account sitting on that PC until
  someone happened to edit the business. `checkNow` clears `appliedUpdateTime` when the access list differs
  from the one kept here, and the existing removal path then wipes what was lost, login and all.
- **The rules must stop a member widening their own.** `changedOnly(['role', 'status', 'accounts'])` is an
  admin's write; a member's own write is `changedOnly(['lastSeen', 'name'])`. Both were proved by adding
  `accounts` to the member's own list on purpose and watching the emulator test go red.
