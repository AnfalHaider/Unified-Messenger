---
id: v6-google-api-reader-built-switched-off
date: 2026-09-20
agent: claude
title: The Google Business API reader exists and is switched off; its tests run against an invented Google, never the real one
triggers: [google business profile api, business.manage, reviews, 3.1b, googleApi.enabled, refresh token, access_type offline, mybusiness]
files: [v6/core/google-api.ts, v6/app/google-api.ts, v6/app/google-api.test.ts, v6/app/oauth-loopback.ts]
cost: None; recorded so nobody rebuilds it, and so the switch is found when Google approves.
evidence: 2026-09-20 6 core tests, 5 against a fake Google, 2 config tests; settings.googleApi.enabled defaults false and has no switch on screen
status: live
---

Google grants access to the Business Profile APIs by application, so the reader could not be tried against the real thing. It is built anyway and inert: `settings.googleApi.enabled` defaults false, `readGoogle` takes the page path unless that is on **and** the profile is connected, and there is no button to connect one yet. Switching it on when approval arrives is a setting plus a Connect button; nothing else changes, because `core/google-api.ts` turns Google's JSON into the same `ReviewCard` the page reader produces (including the age as the words `ageMinutes` reads back, so sorting and filtering work unchanged).

Three things worth keeping:

- **`access_type=offline` and `prompt=consent`, or Google sends no refresh token** and the connection dies within the hour. `authorizeUrl` takes both as options; ordinary sign-in keeps `prompt=select_account`.
- **The scope is `business.manage`.** Google offers no read-only scope for these APIs, so the consent screen says "Manage your Business Profile"; the app only reads, and the homepage and privacy policy say so.
- **Keep Electron out of a file you want to test.** `app/google-api.ts` takes encryption from its host, as `app/workspace.ts` does, so `node --test` runs the whole connect-and-read path against a fake Google in-process. The loopback listener both it and sign-in need lives in `app/oauth-loopback.ts`.
