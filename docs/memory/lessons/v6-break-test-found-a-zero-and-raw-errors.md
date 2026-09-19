---
id: v6-break-test-found-a-zero-and-raw-errors
date: 2026-09-19
agent: claude
title: The break test passed on isolation and still found two defects: a broken reader's account read "0 waiting", and Electron's raw error reached the screen
triggers: [break test, reader not working, 0 waiting, broken reader, read-failed, Script failed to execute, renderer console, plainError, error message on screen, isolation]
files: [v6/tests/smoke.spec.ts, v6/ui/screens/accounts.tsx, v6/app/main.ts, v6/app/view-model.ts, v6/tests/fixtures/broken-instagram.html]
cost: Found by 3.4 on invented pages before any owner saw it.
status: live
---

Isolation held exactly as designed — a channel whose reader throws on every read costs only its own figures, and the other channel is read after it on every pass — but asserting on what the owner would *see* turned up two defects the unit tests could not:

- **A zero standing in for "unknown".** The Accounts cell drew `waiting` whenever it was not null, and a counted account with no snapshot has `waiting` 0, so the broken Instagram account read "0 waiting" under a "Reader not working" label, and the headline counted it as reading. The cell now says the customers are not being counted and "This is not zero", and the headline says "N not being read". Any figure for a channel whose reader is down must be absent, not zero.
- **Electron's wording on screen.** `executeJavaScript` rejects with "Script failed to execute, this normally means an error was thrown. Check the renderer console for the error." and that string went straight into the reader's health line and the timeline (cut at 80 characters, mid-word, with a doubled full stop). `plainError` in main now turns read failures into words the owner can act on; the raw message is still logged for support.

How to make a page break on purpose: a fixture that keeps replacing the reader the app injected (`setInterval` redefining `window.__umReadInstagramThreads` as a function that throws), because the app injects its script after the page's own scripts have run. Keep break tests on invented pages: a second copy of the app opening the owner's real sessions is its own risk to their logins.
