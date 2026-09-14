---
id: v6-one-silent-page-stopped-every-read
date: 2026-09-14
agent: claude
title: A page call with no time limit stalled every account's reads, because reads run one account at a time
triggers: [no reads, reads stopped, app not reading, executeJavaScript, timeout, hang, ticking, readAccount, reader-not-ready missing, startup but no read, page-gone, did-fail-load]
files: [v6/app/main.ts]
cost: The owner's app read nothing for about ten minutes after an install; alerts still fired, so it looked alive.
status: live
---

After the 4.7 install on 2026-09-14 the owner's app logged `startup`, `awake` for every account and `digest-shown`, then no `read`, `reader-not-ready` or `signed-out` at all, while alerts kept firing from the saved snapshot. Only 13 app processes were running where about 23 is normal, so some account pages had probably not come up. `readAccount` awaited `webContents.executeJavaScript` with no limit, and `tick` reads accounts one after another behind a `ticking` flag, so one page that never answered held the flag and every other account went unread. The same build in a clean folder read normally, and a reinstall read normally too; the cause of the silent page was not recorded. The fix is `pageAnswer`, a 30-second limit on the scan and the sign-in probe, so a silent page costs its own read (`read-failed`, "the page did not answer") and the pass moves on. Pages also log `page-gone` (renderer crash) and `page-load-failed` now. This is v5's lesson `gotcha-executescriptasync-no-timeout-under-gate` again, in Electron form: never await a page call without a limit while holding a gate. After any install, confirm a `read` or `reader-not-ready` line appears after `startup`, not only `startup`.
