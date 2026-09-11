---
id: tooling-cdp-verify-probe-target
date: 2026-09-05
agent: claude
title: Two CDP probes returning identical output probably hit the same WebView; verify the target before concluding
triggers: [CDP, remote-debugging-port, json/list, Runtime.evaluate, probe, compare accounts, identical output, desync, transient]
files: [docs/scraper-inventory/instagram.md, docs/scraper-inventory/README.md, CHANGELOG.md]
cost: v4.99.89 shipped a guard for a nonexistent "unsynced window". Its badge bug then hid all 15 unread threads on the busiest Instagram account, so v4.99.90 had to retract it in the changelog.
evidence: CHANGELOG.md v4.99.89 retraction and v4.99.90 "Both probes had hit the same account"; docs/scraper-inventory/instagram.md retracted-finding note; app.log "Discarded an Instagram read: 15 thread(s) ... against the client's own badge of 0."
status: live
---
One account's CDP read showed every thread unread, and a probe of "the other account" returned a small count. The agent took this as a sync transient and shipped a guard against it. Both probes had reached the same WebView: the calls for target indices 0 and 1 returned byte-identical output, and nobody questioned it. The 15-of-15 read was correct. When driving CDP via `http://127.0.0.1:9333/json/list`, each probe should also return an identifying field such as `location.href`, `document.title` or the account handle. Identical output from two targets means the target selection is wrong, not that the data agrees. Never write a guard or record a "transient" based on a cross-account comparison until each probe's target is confirmed.
