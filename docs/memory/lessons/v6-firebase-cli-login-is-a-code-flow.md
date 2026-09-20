---
id: v6-firebase-cli-login-is-a-code-flow
date: 2026-09-20
agent: claude
title: firebase login is a three-step code flow, not a loopback; it can be completed in the browser pane, and the CLI stays signed in afterwards
triggers: [firebase login, site:deploy, hosting, deploy, npx firebase, auth.firebase.tools, session id, authorization code, 6.6]
files: [v6/site/index.html, v6/README.md]
evidence: 2026-09-20 session 608B6, consent completed in the browser pane as anfalhaider@gmail.com, "Logged in as", then four files deployed and both pages checked live (200, nosniff, no-referrer, Limited Use present, no scripts)
cost: None; recorded because the flow is not the loopback one the app's own sign-in uses, and the first guess would be wrong.
status: live
---

`npx firebase login` does **not** open a loopback port like the app's own Google sign-in. It prints a session ID and a URL, and waits for an authorization code to be pasted back as `firebase login <code>`. The browser pane can do the whole thing: open the URL, choose the account, approve the consent screen (Firebase data, Cloud projects), then confirm two pages — "did you just run this command" and "does this session ID match" — and copy the code it finally shows. The session ID on screen must equal the one the CLI printed; that check is the point of the flow.

Two things that follow:

- **This is an OAuth grant on the owner's account**, so it is theirs to ask for. Prepare everything, leave them the one command, and only do it in the browser when they say so.
- **The CLI stays signed in** (`npx firebase login:list`), so `npm run site:deploy` is one command from then on. The site is Firebase Hosting on the free plan: `https://unified-messenger-5549a.web.app`, serving `v6/site/`.

After a deploy, check the live pages rather than trusting "Deploy complete": status, content type, `nosniff` and `no-referrer`, and that the page still says what Google requires (`Limited Use`, a contact address) and carries no script.
