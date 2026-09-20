---
id: v6-group-query-needs-an-index-the-emulator-never-asks-for
date: 2026-09-21
agent: claude
title: A collection-group query needs an index the Firestore emulator never asks for, so 25 green tests hid a live 400
triggers: [workspace-check-failed, query 400, FAILED_PRECONDITION, allDescendants, collection group, collectionGroup, firestore.indexes.json, fieldOverrides, COLLECTION_GROUP, emulator, rules:test]
files: [v6/app/workspace.ts, v6/cloud/firestore.indexes.json, v6/firebase.json, v6/package.json]
cost: Workspace sync was broken on the owner's PC from the moment they signed in until it was found in app.log a day later. Every test was green throughout.
status: live
---

A PC asks which workspace it belongs to with a **collection-group** query — `members` across every workspace,
by the signed-in address (`app/workspace.ts`, `allDescendants: true`) — and the same for `invites`. Firestore
indexes a field automatically only **within one collection**; a group query needs that field listed with
`COLLECTION_GROUP` scope, or the service refuses it with **400 FAILED_PRECONDITION**. The Firestore emulator
does not enforce index requirements at all, so all 25 emulator tests passed while the live database refused
the very same query. Only `app.log` showed it, as `workspace-check-failed error=query 400`.

The fix is `v6/cloud/firestore.indexes.json`, wired into `firebase.json` and deployed with
`npm run cloud:deploy`. It is a **`fieldOverride`, not an `index`**, and an override *replaces* the automatic
indexing of that field, so the three ordinary scopes (ASCENDING, DESCENDING, ARRAY_CONTAINS at COLLECTION)
must be repeated alongside the COLLECTION_GROUP one or ordinary queries on the field stop working.

Two things to carry beyond this bug:

- **It is a server-side fix, so it repairs copies already installed.** The app running on the owner's PC went
  from `workspace-check-failed` to `workspace-none invitations=0` on the next restart, with no new build.
- **`query 400` was not enough to act on.** `commit()` already carried Firestore's own condition; `query()`
  did not, so the log said the request failed without saying why. A status word like FAILED_PRECONDITION is
  Firestore's name for the fault, never a name, an address or a figure, so it belongs in `app.log`.
