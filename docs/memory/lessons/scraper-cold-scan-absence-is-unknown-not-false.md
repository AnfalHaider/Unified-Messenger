---
id: scraper-cold-scan-absence-is-unknown-not-false
date: 2026-08-17
agent: claude
title: A scan taken before chat.msgs fills reported "no last message" for every chat, closed the queue, and persisted that to disk
triggers: [hasLastMessage, chat.msgs, lazy, cold scan, after reload, deleted message, awaiting collapsed, coverage, snapshot load, null vs false]
files: [UnifiedMessenger/Assets/Scripts/whatsapp-store-bridge.js, UnifiedMessenger/Assets/Scripts/whatsapp-adapter.js, UnifiedMessenger/Services/Oversight/OversightChatSnapshotService.cs, UnifiedMessenger.Tests/AwaitingSplitTests.cs]
cost: 354 real waiting conversations rendered as 5 on the live dashboard, and the broken build had already written a snapshot to the owner's disk that would keep closing the queue on every launch.
evidence: d21e745; AwaitingSplitTests (both halves of the cold-scan guard); retraction comments in whatsapp-store-bridge.js and OversightChatSnapshotService.cs ("repeated on LOAD").
status: live
---
`hasLastMessage: false` was added to detect a deleted or expired message. `chat.msgs` is populated lazily, so a scan seconds after a reload has no last message for almost every chat. Read literally, that says every customer's message was deleted. AGENTS.md records the lazy fill as only lowering preview counts, but a field that asserts a negative turns it into a wrong count. Coverage is a property of the whole scan, not of one chat: when fewer than half the chats produced a message, emit `null` (unknown) for all of them rather than `false`. Apply the same retraction when loading a persisted snapshot, because a file written by a cold scan outlives the fix. It was caught on the live app, not by tests.
