---
id: v6-suspension-must-not-lock-the-owner-out
date: 2026-09-20
agent: claude
title: A full-window lock the product owner can trigger must exempt the product owner, or the console that lifts it is behind it
triggers: [suspend, owner console, lock screen, forced lock, workspace, 6.5, suspended, restore]
files: [v6/ui/App.tsx, v6/ui/screens/workspace.tsx]
cost: Caught while writing the test, before it shipped.
evidence: 2026-09-20 the product owner is also a member of the workspace they created, so the suspended lock would have covered the console holding the Restore button
status: live
---

The suspended screen is a full-window lock over everything, decided by state rather than by navigation. The product owner belongs to their own workspace, so suspending it would have locked their own app, including the console with the only Restore button; the only ways out would have been the database console or another account. The forced lock now skips the product owner (`!state.owner.isOwner` in `ui/App.tsx`), and the emulator test checks both sides: the member's PC locks, the owner's does not. Whenever a state-driven lock exists, ask who can turn that state on and whether the way to turn it off is behind the lock.

A second thing this step settled: a full-window state must be reachable another way. The owner console was only in the command palette, and the palette lists it only once the owner check has come back, so right after a launch the entry is not there and a test that opens it that way times out for the wrong reason. Settings › Workspace now carries an Owner console button for that account.
