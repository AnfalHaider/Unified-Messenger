---
id: v6-rules-tested-on-the-emulator-are-not-rules-deployed
date: 2026-09-23
agent: claude
title: A security rule green on the emulator and never deployed broke the owner's invitations for a day
triggers: [PERMISSION_DENIED, commit 403, rules:test, cloud:deploy, firestore.rules, invite not saving, member-change-failed, emulator only]
files: [v6/cloud/firestore.rules, v6/package.json, v6/app/workspace.ts]
cost: Every invitation the owner tried for a day was refused. Three attempts in three minutes, and they had to come and say it was broken.
status: live
---

Per-account access (4.19) added `accounts` to what an invitation may carry, and to `cloud/firestore.rules`. The
emulator tests went green, the feature shipped in 6.1.0, and **the rules were never deployed**. The live project
still refused any invitation carrying `accounts`, so the owner's real invites failed with
`commit 403 PERMISSION_DENIED` while every test in the repository passed.

The rule to carry: **`npm run rules:test` proves the rule is right; `npm run cloud:deploy` is what makes it
true.** A change under `cloud/` is not finished when the emulator is green — it is finished when it is
released, and released rules are what a customer's app meets. Deploy in the same breath as the code that needs
them, and before the build that sends it out.

Two smaller things this dug up:

- **The log had the answer all along.** `member-change-failed error="commit 403 PERMISSION_DENIED"` was written
  three times. The first grep, for `member-invite-failed` and `workspace-error`, found nothing and nearly led
  to the wrong conclusion — that the write never happened. Grep the event names the code actually writes
  (`git grep "event: 'member-"`), not the ones that sound right.
- **The message blamed the wrong thing.** A refused write said "Check the connection and try again", which
  sends an admin to look at their wifi. A 403 is the workspace refusing, and now says so.
