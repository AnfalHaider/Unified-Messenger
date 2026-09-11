---
id: process-live-capture-on-build-predating-fix
date: 2026-08-16
agent: claude
title: A live UI Automation capture taken on a binary older than the fix produced a confident, entirely fictional defect
triggers: [verify live, UI Automation, capture, FileVersion, stale binary, finding withdrawn, fix did not take effect, audit finding, build number]
files: [docs/audit/findings/offline.md, docs/audit/HANDOFF.md]
cost: F-OFFLINE-05 ("the sidebar offline wording did not take effect") was carried as the branch's one known-broken fix, listed first in next steps, and shipped in a merged report and a public release before being withdrawn.
evidence: 26d2e5f; the capture was on 4.99.21.0 but ComposeRowSubtitle gained its connectionDetail parameter only in v4.99.22; re-verified on v4.99.27 showing "No internet — reconnecting…" offline and normal text online.
status: live
---
The finding came with a suspect list (`MainWindow.OnSessionFailed`, `PlatformAdapters`) and a next step. Its own evidence line recorded the binary as 4.99.21.0, built before the change existed, and nobody compared that version with the one the fix landed in. A stale capture is more dangerous than none because it looks like evidence. Before concluding anything from the running app, read the exe's `FileVersion` and confirm it is at or above the version that introduced the change under test. "The latest publish" is not enough. When a finding is wrong, withdraw it in place with the disproof, as `offline.md` does, rather than deleting it.
