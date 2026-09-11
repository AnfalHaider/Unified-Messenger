---
id: scraper-signin-evaluate-logged-out-first
date: 2026-09-04
agent: claude
title: Test "signed out?" before "signed in?", and never treat the client's own host as proof of a session
triggers: [sign-in, signed out, QR screen, connection-handshake, urlLoggedIn, Connected, caught up, zero read, SignInGate, false calm]
files: [UnifiedMessenger/Assets/Scripts/connection-handshake.js, UnifiedMessenger/Services/Oversight/SignInGate.cs, UnifiedMessenger/Services/Oversight/OversightSnapshotReader.cs, UnifiedMessenger.Tests/ConnectionHandshakeScriptTests.cs, UnifiedMessenger.Tests/SignInGateTests.cs]
cost: A WhatsApp account parked on the QR screen reported "Connected - Signed in", was scraped, returned zero and showed as caught up; the greeting claimed all accounts connected.
evidence: 795cb03 (v4.99.85), ff9d45b (greeting fix); ConnectionHandshakeScriptTests.LoggedOutIsEvaluatedBeforeLoggedIn, NoPlatformTreatsItsOwnHostAsProofOfASession
status: live
---
`evaluateConnection` checked the signed-in markers first. WhatsApp's profile also listed `web.whatsapp.com` under `urlLoggedIn`, so a QR-screen page matched "signed in" and the QR check never ran. The generic signed-in markup (`main, nav, header`) is present on login pages too. Sign-in markup is specific (a QR canvas, a password field), so check signed-out first. A URL tells you which client is loaded, never whether anyone is signed in. The one allowed URL signal is signed-out: Google's redirect to accounts.google.com. `OversightSnapshotReader` also never checked sign-in state, and a successful read of zero looks the same as a quiet account downstream. That is why `SignInGate` now answers both "may this account be scanned?" and "may its figures be shown?". Two existing tests had pinned the defect: one required the host to appear in the script, the other required googlebusiness to have no profile. Read an old test as a record of past behaviour, not as intent.
