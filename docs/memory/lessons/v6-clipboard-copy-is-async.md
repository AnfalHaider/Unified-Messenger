---
id: v6-clipboard-copy-is-async
date: 2026-09-20
agent: claude
title: A test that reads the clipboard straight after pressing Copy races the page; wait for the text instead
triggers: [clipboard, copy, playwright, saved reply, readText, flaky, navigator.clipboard]
files: [v6/tests/smoke.spec.ts]
cost: One red full run on an unrelated change, which read like a regression in saved replies.
evidence: 2026-09-20 the customer-panel test read the previous test's text from the clipboard, alone and in the suite; with expect.poll it passed 3 of 3
status: live
---

The page copies with `navigator.clipboard.writeText`, which resolves later, so `app.evaluate(({ clipboard }) => clipboard.readText())` straight after the click can return whatever was on the clipboard before, including text a previous test copied. It passed for weeks and then failed every time on a slower run. Read it with `await expect.poll(() => app.evaluate(({ clipboard }) => clipboard.readText())).toBe(...)`. The clipboard is also the whole PC's: a test must never assume it starts empty.
