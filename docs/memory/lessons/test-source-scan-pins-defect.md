---
id: test-source-scan-pins-defect
date: 2026-09-04
agent: claude
title: Source-scan tests that assert a literal, a count or a position lock in defects and fail on harmless edits
triggers: [source guard, script test, File.ReadAllText, Assert.Contains, Assert.DoesNotContain, ratchet, failing test after refactor, test pinned the bug]
files: [UnifiedMessenger.Tests/ConnectionHandshakeScriptTests.cs, UnifiedMessenger.Tests/InstagramFocusTests.cs, UnifiedMessenger/Assets/Scripts/connection-handshake.js]
cost: A test required the very literal that made QR-screen WhatsApp accounts report "Connected · Signed in"; several later source-scan tests failed on correct code and had to be rewritten.
evidence: CHANGELOG.md v4.99.85 "Two existing tests pinned the defect rather than the behaviour"; ConnectionHandshakeScriptTests.cs class doc; CHANGELOG.md v4.99.86 note that the status-palette ratchet tripped on its own comment "since it counts raw text occurrences"; InstagramFocusTests.cs comment on the substring "send".
status: live
---
Much of this suite guards JS and XAML by reading the shipped file and asserting on strings. One such test required `web.whatsapp.com` inside `urlLoggedIn`, the exact shortcut that made signed-out accounts look connected. Another asserted that the Google Business profile was absent. When the sign-in fix landed, both read as intent and had to be inverted (v4.99.85). The same design fails on correct code in three ways. A raw-occurrence ratchet tripped on a comment explaining it. A ban on the substring "send" would fail on the file's own prose. In v5.1.0, a count of `/direct/t/` and an "everything after the readback's name" scan broke when legitimate harvest code was added. Before trusting a red source-scan test, ask whether it checks behaviour or just the text that happened to be there. Assert the precise dangerous form (for example `assign('https://www.instagram.com/direct/t/`, not the bare path), and when inverting a test, keep its reasoning in the comment so nobody restores the old version.
