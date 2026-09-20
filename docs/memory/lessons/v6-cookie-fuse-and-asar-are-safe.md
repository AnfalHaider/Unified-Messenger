---
id: v6-cookie-fuse-and-asar-are-safe
date: 2026-09-20
agent: claude
title: Turning cookie encryption on keeps existing logins, and asar does not break Node's TypeScript stripping — both proved before installing
triggers: [EnableCookieEncryption, fuses, asar, OnlyLoadAppFromAsar, cookies, logins, dist.mjs, packaging, 7.1, 7.2]
files: [v6/scripts/dist.mjs]
cost: None; the two changes most likely to log every customer out or stop the app starting, both checked first.
evidence: 2026-09-20 fused Electron read a cookie written by an unfused one; a cookie written fused is not plaintext in the file; the packed app injected its reader and quit; after the install all three Instagram accounts (cookie logins) read again
status: live
---

Two packaging changes that could each have broken every install, and how each was proved cheaply first.

**Cookie encryption** (`EnableCookieEncryption`, flipped in `scripts/dist.mjs` after packaging). Chromium reads a cookie's plain `value` when there is no encrypted one, so logins written before the change keep working; new ones are encrypted. Proved without touching the owner's data: copy `node_modules/electron/dist`, flip the fuse on the copy, write a cookie with the unfused Electron into a throwaway profile, read it back with the fused one, and grep the SQLite file for the value before and after (present, then absent). Then the install itself: the three Instagram accounts, whose logins are cookies rather than WhatsApp's IndexedDB, read again straight away.

**asar** (`asar: true`, with `OnlyLoadAppFromAsar`). Node's type stripping works inside the archive, and so does `readFileSync` of the channel scripts: run the packed exe against a throwaway data folder with one `about:blank` account and `UM_SELFTEST`; `reader-not-ready` with stage `no-store` is the injected script answering, which is the proof that the archive is being read from.

A third thing this turned up: a Playwright test whose data folder came from a v5 import cannot end with `app.close()`, because the imported settings leave "closing keeps reading in the background" on, the app sits in the tray, and the worker hangs for its whole timeout. End that kind of test with `app.evaluate(({ app }) => app.exit(0))`.
