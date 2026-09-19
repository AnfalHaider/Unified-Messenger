---
id: v6-browser-sign-in-tested-by-a-redirecting-fake
date: 2026-09-19
agent: claude
title: Test the browser sign-in end to end with a fake whose authorize page redirects straight back to the loopback port
triggers: [sign-in, oauth, google, firebase, loopback, pkce, playwright, fake server, openExternal, cloud, 6.1]
files: [v6/app/cloud.ts, v6/core/cloud-auth.ts, v6/tests/smoke.spec.ts]
cost: None; recorded so the next cloud step (6.2 onward) reuses the seam instead of reaching Google from a test.
evidence: 2026-09-19 Playwright 'signing in with Google' passes against the fake, five app launches in about 5 s
status: live
---

Google's desktop sign-in opens the system browser, which a test cannot drive. The seam in v6: `UM_CLOUD_ENDPOINT` points all four addresses (authorize, token, signInWithIdp, refresh) at a server inside the test, and `UM_SIGNIN_OPEN=fetch` (honoured only together with `UM_CLOUD_ENDPOINT`) makes the app `fetch` the authorize URL instead of `shell.openExternal`. The fake's authorize answers `302` to the app's own `redirect_uri` with `code` and `state`, and `fetch` follows it, so the real loopback listener, the state check and the PKCE exchange all run. The fake records every call, so the test can check the scope and that `sha256(code_verifier)` equals the `code_challenge`. Every launch in `open()` also sets `UM_CLOUD_CONFIG` to an invented project and `UM_CLOUD_ENDPOINT` to a closed port, because `v6/cloud-config.json` exists on the dev PC and the app would otherwise find the real project. Workspace calls in 6.3 should hang off the same endpoint override.
