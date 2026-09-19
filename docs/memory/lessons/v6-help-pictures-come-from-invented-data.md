---
id: v6-help-pictures-come-from-invented-data
date: 2026-09-19
agent: claude
title: Help pictures are taken by a Playwright run on invented data, never captured from the owner's app, and must be retaken when a screen changes
triggers: [help, help pages, screenshots, help:shots, documentation, F1, help drawer, markdown, help/shots, user guide]
files: [v6/help, v6/ui/help.tsx, v6/ui/help-index.ts, v6/tests/smoke.spec.ts, v6/scripts/help-shots.mjs]
cost: Design decision for 4.16; the tempting shortcut was a screenshot of the owner's own window, which would have put real customer names into the repo and the installer.
status: live
---

The help pages show what each screen looks like, and the obvious way to get those pictures — capture the running app — would copy real customer names, numbers and previews into `v6/help/shots`, into git, and into every installer. The pictures come instead from the `help screenshots` Playwright test, which builds a data folder of "Sample Customer" chats, invented day records and replies, and photographs every screen; it is skipped in an ordinary run and runs with `npm run help:shots` (it sets `UM_HELP_SHOTS`). Never add a picture any other way.

Consequences to keep: the pictures go stale when a screen changes, so a screen change is not finished until its page in `v6/help/` is updated and `npm run help:shots` has been run; `ui/help-index.test.ts` fails when a route has no page, a page is unlisted, or a page links to a page or picture that does not exist, so a renamed screen or picture is caught. The pages are drawn from a small Markdown subset as React elements, never as injected HTML, and take no dependency. JPEG at quality 82 keeps 22 full-window pictures near 2 MB.
