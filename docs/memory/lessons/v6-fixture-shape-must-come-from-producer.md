---
id: v6-fixture-shape-must-come-from-producer
date: 2026-09-13
agent: claude
title: The Instagram module passed its tests while rejecting every real thread, because its fixture used the WhatsApp shape
triggers: [fixture, test data, parser, Instagram reader, read-empty, shape, port from v5, contract]
files: [v6/channels/instagram/index.ts, v6/channels/reader.test.ts]
cost: Instagram read as empty on real accounts; time went on cookies and navigation first.
status: live
---

Write a parser's fixtures from what the producer actually emits (its source, or a captured read), never from another producer's shape. v5's `instagram-adapter.js` returns `{ conversations: [{ key, name, username, unread, awaiting, lastActivityMs }], unreadBadge, unreadBadgeCapped, diag.stage }`, read from the home feed's Relay prefetch; v6 fed it to the WhatsApp scan parser, so every row was skipped. Porting a reader means porting its consumer too (`InstagramSnapshotReader.cs`), including guards such as the unsynced-badge check. Opening the inbox page was the wrong fix: the reader never navigates.
