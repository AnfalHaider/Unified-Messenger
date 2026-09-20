---
id: v6-public-pages-on-firebase-hosting
date: 2026-09-20
agent: claude
title: The product's public pages live in v6/site and deploy to Firebase Hosting; the CLI sign-in is the owner's to give
triggers: [homepage, privacy policy, firebase hosting, site, deploy, consent screen, limited use, google api, 6.6, web.app]
files: [v6/site/index.html, v6/site/privacy.html, v6/site/site.test.ts, v6/firebase.json]
cost: None; recorded so the next person does not look for a separate site repo or a paid host.
evidence: 2026-09-20 pages written and tested (3 tests), firebase.json hosting block added; `npx firebase login:list` reports no authorized account, so the deploy waits on the owner
status: live
---

Google's consent screen needs a homepage and a privacy policy on the open web. Both are plain HTML in `v6/site/`, with one stylesheet, no scripts and nothing fetched from anywhere, served free by Firebase Hosting from the same project as sign-in (`npm run site:deploy`, `public: site` in `v6/firebase.json`). `site/` is excluded from the packaged app in `scripts/dist.mjs`.

Two things to know before repeating this:

- **`firebase deploy` needs `firebase login`**, which grants a CLI access to the owner's Google account through a consent screen. That is the owner's to click, not something to do for them from an agent session, even with their browser already signed in to the console. Prepare everything and leave them the one command.
- **A `web.app` address cannot have email.** Google's Business Profile API access form checks that the contact address is on the website's domain, so that step needs a bought domain (pointed at the same free hosting) — recorded in `docs/revamp/google-api-checklist.md`, not a blocker for the consent screen in Testing.

`site/site.test.ts` pins what the pages must keep saying, so a rewrite cannot quietly drop the Limited Use wording, the contact address, or the promise that customer data never leaves the PC.
