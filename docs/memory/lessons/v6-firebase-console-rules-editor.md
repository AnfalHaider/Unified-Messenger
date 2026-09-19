---
id: v6-firebase-console-rules-editor
date: 2026-09-19
agent: claude
title: Publishing Firestore rules through the console: focus a text line first, type the file, and read it back by hash before Publish
triggers: [firestore rules, publish rules, firebase console, deploy rules, codemirror, browser, rules editor, owners]
files: [v6/cloud/firestore.rules]
cost: One silent failure: 8,057 characters typed into nothing, noticed only because the editor was read back before publishing.
evidence: 2026-09-19 first attempt left the editor at its 163-character default; second, after clicking line 1, read back 8,057 chars with sha256 c00c9eced21b5351, equal to the file; published 10:57 pm
status: live
---

The Firestore Rules tab is a CodeMirror 6 editor. Clicking the empty area below the text does not focus it, so typing goes nowhere and nothing says so; click on a line of text, then Ctrl+A, Delete, and type the file. Typing inserts literally (no auto-closed brackets or auto-indent: checked with a small `{` test first). Before Publish, read the editor back with `document.querySelector('.cm-content').cmView.view.state.doc.toString()` and compare its length and SHA-256 with the file (LF line endings); read it again after Publish. The Users table hides the User UID column in a narrow window: widen the viewport to read it. The Firestore database was created in `asia-south1` (permanent), Standard edition, production mode; backups need the paid Blaze plan and stay off.
