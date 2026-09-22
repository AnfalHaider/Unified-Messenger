---
id: v6-an-anchor-that-looks-unique-may-not-be
date: 2026-09-22
agent: claude
title: A scripted edit anchored on a line that appears in two tests silently lands in the wrong one
triggers: [scripted edit, replace, anchor, smoke.spec.ts, wrong test, Playwright, timeout waiting for, insert after]
files: [v6/tests/smoke.spec.ts]
cost: One full 2.4-minute Playwright run red, on a test that had nothing to do with the change.
status: live
---

Adding assertions to `smoke.spec.ts` by anchoring on three lines that ended a test —

```
await win.getByRole('navigation', { name: 'Screens' }).getByRole('button', { name: /^The line/ }).click();
await expect(heading(win, '1 customer is waiting')).toBeVisible();
await quit(app, win);
```

— put them in a **different test**, 800 lines earlier, because that ending is how several tests finish. A
`replace(a, b, 1)` takes the first match, and the first match was not the intended one. The new assertions then
timed out on a screen that test never opens, and the failure named a test that had nothing to do with the
change.

Two habits that would have caught it in seconds:

- **Count the matches before replacing.** If an anchor appears more than once, anchor on something that does
  not: a line unique to that test (`await expect.poll(() => config().settings.notCustomers.numbers)`), or find
  the test by its `test('…'` line first and search only inside it.
- **Read back where it landed**, not just that the edit reported success. `sed -n` around the insertion point
  costs one call.

Related: after inserting a test, **break one of its assertions on purpose and watch it go red** before moving
on. Here the host test passed in 2.3 seconds either way, which looked exactly like the new assertions running
and exactly like them not existing.
