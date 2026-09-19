---
id: v6-scripted-replace-reads-dollar-patterns
date: 2026-09-19
agent: claude
title: A scripted String.replace edit treats "$&" in the new text as "insert the match", and silently writes broken code
triggers: [edit script, String.replace, "$&", replacement pattern, regex escape, scripted edit, broken code after edit, .mjs edit, python replace escaping]
files: [v6/core/snapshot.ts]
cost: One broken edit to core/snapshot.ts and two failed repair attempts while adding the not-a-customer rules.
status: live
---

The edit scripts used in this repo (`s.replace(anchor, newText)` in a `.mjs`, because the Edit tool cannot match multi-line text in CRLF files) pass the new text as a *replacement string*, and JavaScript reads `$&`, `$1`, `` $` `` and `$'` inside it as patterns. Adding a regex-escape helper — `word.replace(/[...]/g, '\\$&')` — therefore spliced the anchor text itself into the middle of the line, and the file stopped compiling. Two rules:

- **Pass a function, never a string, as the replacement:** `s.replace(anchor, () => newText)`. A function's return value is inserted literally. The shared `edit()` helper in later scripts does this.
- **Repair a single broken line with the Edit tool, not another script.** Python and shell each add their own layer of escaping (`\\]`, `$&`, `\\$&`), and each attempt to fix the escaping through a script got it wrong in a new way; a one-line Edit with the exact text on screen was right first time.
