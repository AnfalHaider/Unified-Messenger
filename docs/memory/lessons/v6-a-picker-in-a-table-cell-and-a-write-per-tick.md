---
id: v6-a-picker-in-a-table-cell-and-a-write-per-tick
date: 2026-09-23
agent: claude
title: A chooser dropped into a table cell had no room, and saving on every tick made the screen crawl
triggers: [members table, AccountAccess, Change, slow, stuck, UI breaks, wrapped, colSpan, write per click, setMemberAccounts]
files: [v6/ui/screens/settings.tsx, v6/tests/smoke.spec.ts]
cost: The owner opened Change on a real member and the screen was unusable: every account name wrapped over three lines, the row's own buttons were pushed out of reach, and each tick froze it.
status: live
---

Two mistakes in one small feature, and both are the kind that only show on real data:

- **A picker inside a `<td>` gets the column's width, not the table's.** With three branches and nine accounts
  it wrapped every name over three lines and pushed Make admin and Remove off the row. Tested on a fixture with
  one short branch, it looked fine. Anything taller than a line or two belongs in a row of its own
  (`<tr><td colSpan={n}>`) or a dialog, never in the cell that shows its summary.
- **Saving on every tick is a round trip per tick.** `onChange` wrote to the workspace and then reloaded every
  member and invitation, so ticking a branch of three accounts meant three writes and three reloads. Keep the
  choice in a draft and write once on Save; the screen stays still, half-made choices never reach the
  workspace, and Cancel becomes possible.

The test now proves the second point rather than describing it: after ticking a box it asserts the summary
**still** reads `2 of 3`, and only after Save does it read `1 of 3`. Without that assertion a per-tick write
would pass just as happily.
