---
id: v6-removing-an-account-reaches-every-store
date: 2026-09-14
agent: claude
title: Removing an account must reach every store keyed by its id, and a new store must be added to forgetAccount
triggers: [remove account, delete account, wipe, forgetAccount, new store, calls.json, history.json, alerts.json, leftover data, orphaned account data]
files: [v6/core/accounts.ts, v6/app/main.ts]
cost: Found while building 4.9; the old wipe handler only cleared the session and left figures, marks and calls behind.
status: live
---

An account's data is spread across six stores in main, each keyed by the account id in its own way: `snapshots[id]`, `overrides[id]`, `history[id]`, `times.pending/watchStart/samples[id]`, calls whose `account` is the id, and alert ids of the form `kind:<id>:…`. Wiping the session (`clearStorageData`) removes the login but none of these, so a removed account would keep appearing in reports, the digest and alerts. `forgetAccount` in `core/accounts.ts` deletes from all of them in place (in place, because main holds and saves those same objects), and the `remove-account` handler then saves every store. Whenever a new store keyed by account is added, add it to `forgetAccount` and its test, or removal silently leaves it behind. In-memory maps in main (`lastReadAt`, `lastUsedAt`, `lastRead`, `focusRequest`, `signedOut`) are cleared in the handler too. Also: a Playwright check that waits for a heading already visible behind a dialog passes before the dialog's action finishes; wait for the dialog to close or poll the saved file.
