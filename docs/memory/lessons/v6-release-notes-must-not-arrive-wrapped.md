---
id: v6-release-notes-must-not-arrive-wrapped
date: 2026-09-21
agent: claude
title: A wrapped release body reaches the update drawer cut mid-sentence, and the copies that read it cannot be fixed afterwards
triggers: [release notes, notesFrom, update drawer, wrapped markdown, CHANGELOG, release body, half sentences, release:notes, blocksOf]
files: [v6/core/update.ts, v6/core/update.test.ts, v6/scripts/release-notes.mjs, v6/README.md]
cost: Caught while verifying the v6.0.0 release, before any customer saw it. It would otherwise have shown six half-sentences to every PC offered v6.1.0.
status: live
---

`CHANGELOG.md` is written wrapped at about 115 characters, the way every file in this repository is, and the
v6.0.0 release body was that text pasted as it stood. `notesFrom` split the body on newlines and treated each
line as a bullet, so the drawer would have shown *"…longest wait first, with the"* and stopped. The bug is
invisible on GitHub, because Markdown reflows the wrap when it renders: only the app sees it.

Two fixes, and **both are needed, because they cover different copies of the app**:

- `notesFrom` now gathers wrapped lines back into blocks (`blocksOf`): a line that does not begin a block of
  its own — no `- `, `#` or `>` — joins the line above. While there, `rest` stopped excluding a bare `*`, which
  had been throwing away every paragraph starting `**Bold.**`, and the 160-character cut became
  surrogate-safe.
- **The release body itself must be written unwrapped**, one line per bullet: `npm run release:notes` prints
  the newest `CHANGELOG` entry that way. This is the half that matters most, because the app reading a
  release's notes is the *old* copy, the one already installed — a fix to `notesFrom` can never reach it.

The general shape is worth carrying: when the app reads something published elsewhere, a parsing fix only
helps future versions, so the published thing has to be right for the versions already out there.
