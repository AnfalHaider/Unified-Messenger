---
id: gotcha-restore-undone-by-shutdown-flush
date: 2026-08-26
agent: claude
title: Restoring files under live in-memory stores is silently reverted by the shutdown flush
triggers: [backup, restore, import settings, shutdown flush, overwrite, in-memory store, replace files on disk, data loss]
files: [UnifiedMessenger/Services/ApplicationLifecycleService.cs, UnifiedMessenger/Pages/SettingsPage.Data.partial.cs, docs/audit/AUDIT-2026-08-26.md]
cost: S1 — backup restore, the product's recovery path, was undone every time the app closed afterwards; it shipped that way until v4.99.46.
evidence: CHANGELOG v4.99.46 "Restoring a backup is no longer silently undone"; docs/audit/AUDIT-2026-08-26.md finding S1-02.
status: live
---
Restore replaced the store files on disk, but every store was still loaded in memory with the pre-restore state. Closing the app flushed that state back over everything restored, so the restore appeared to work and then silently reverted. The fix suppresses the shutdown flush and has the app close itself after a restore, so the next launch loads the restored files. Any feature that writes a store's file behind the store's back (import, reset, repair, migration) has the same problem. Either reload the in-memory store or suppress its flush, and verify by restarting, not by reading the file right after the write.
