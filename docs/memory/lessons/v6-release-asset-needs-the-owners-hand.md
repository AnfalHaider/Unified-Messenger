---
id: v6-release-asset-needs-the-owners-hand
date: 2026-09-21
agent: claude
title: An agent can publish the release but not attach the Setup; the update check is built so a release without it is harmless
triggers: [GitHub release, release asset, file_upload, 10 MB, gh not installed, UnifiedMessenger6Setup.exe, publish v6.0.0, browser upload, readRelease, no update offered]
files: [v6/core/update.ts, v6/scripts/dist.mjs, docs/revamp/roadmap.md]
cost: None this time, because the update code already refused an assetless release; it would have been a broken update offer on every installed copy otherwise.
status: live
---

Publishing a release is browser work an agent can do end to end — the form at
`/releases/new?tag=<tag>` takes a title, the `CHANGELOG` entry as the body, and Latest — but **attaching the
installer is not.** Claude's own browser pane is signed in to nothing, and the owner's Chrome (Claude in
Chrome) uploads only files the owner has handed to the session, capped at 10 MB; the Setup is about 138 MB.
`gh` is not installed on this PC, and the REST upload wants a token, which an agent must not handle. So the
last step is the owner dragging `v6/dist/UnifiedMessenger6Setup.exe` onto the release's edit page. Open that
page in their browser and reveal the file in Explorer for them, rather than describing the path.

The reason this is safe to publish first is in `core/update.ts`: `readRelease` returns `null` for a release
with no asset named exactly `UnifiedMessenger6Setup.exe`, as it does for a draft, a pre-release, or a download
that is not GitHub's. Check it against the live API right after publishing rather than trusting the unit
tests — fetch `releases/latest` and run `readRelease` on the real JSON from an older version and from the
current one. Both returning `null` is the proof that a half-finished release offers nobody a broken update.
