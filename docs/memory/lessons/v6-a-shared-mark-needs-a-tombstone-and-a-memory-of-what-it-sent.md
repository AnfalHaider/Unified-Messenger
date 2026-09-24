---
id: v6-a-shared-mark-needs-a-tombstone-and-a-memory-of-what-it-sent
date: 2026-09-24
agent: claude
title: Sharing a mark between PCs needs a tombstone for the undo and a record of what this PC sent, or a PC hands itself back the mark it just put back
triggers: [mark, handled, snoozed, not a customer, put back, sync, merge, workspace, two PCs, tombstone, 6.7, mark-sync]
files: [v6/core/mark-sync.ts, v6/core/mark-sync.test.ts, v6/app/workspace.ts, v6/cloud/sync.spec.ts]
cost: One emulator test red; caught before it shipped.
evidence: 2026-09-24 the two-PC emulator test "a put-back reaches it too" failed: PC B cleared the mark, then took the workspace's copy of its own mark straight back on the same pass
status: live
---

Sharing a set that people both add to and remove from is not "take the union". Two things are needed, and the
first is the one that is easy to get right:

1. **The removal needs a shape.** A put-back on one PC is an *absence* locally, and an absence says nothing to
   the other PCs. It is written as a `cleared` mark with its own moment, which the other PCs apply by deleting.
   Those tombstones are swept after 30 days (`staleCleared`), once every PC has had every chance to see them.
2. **Each PC must remember what it sent.** Without that, "gone from here" and "never here" look identical. On
   the pass after a put-back, the workspace still holds the mark, the merge sees nothing locally, and applies
   it — the PC hands itself back the thing it just undid, and the button appears not to work. `pushedMarks` in
   `kept.json` records the moment of every mark this PC last sent or last took, and the merge skips a remote
   mark when `pushed[id] === m.at` and nothing is held locally: that combination *is* the put-back.

The conflict rule is the mark's own moment (when the button was pressed), never the write's, because a mark
made offline has to be comparable with one made on another PC. Both clocks are Windows' own; a few seconds of
skew could decide a tie the wrong way, which is accepted because these presses are minutes apart in practice.

The unit tests prove the merge settles (feed a pass its own output plus what it pushed; nothing moves), but
the settling test passed while the put-back bug was live, because it never removed anything. It took the
two-PC emulator test — two real clients, real rules, real Firestore — to catch it. A merge rule's unit tests
should include *undo*, not only add-and-agree.

One more thing this step touched: it changed what leaves the PC, which is a hard rule. A change like that is
not done when the code works — it is done when `site/privacy.html` names the new thing and says what it is
(the conversation's key, which for WhatsApp is the customer's number), `site/site.test.ts` pins that wording,
Settings › Privacy says the same to the person using it, and `AGENTS.md` records the decision and its date so
the next agent reads the exception rather than the old absolute. See [[v6-rules-tested-on-the-emulator-are-not-rules-deployed]].
