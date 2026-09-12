---
id: v6-crlf-files-defeat-scripted-multiline-edits
date: 2026-09-13
agent: claude
title: v6 sources are CRLF on disk, so a scripted replace with "\n" in its pattern silently changes nothing
triggers: [sed, node -e, string replace, multiline, CRLF, line endings, edit did nothing, no-op]
files: [v6/core/config.ts, v6/ui/preview-state.ts]
cost: Two edits silently skipped; one only surfaced as a missing field in the next grep.
status: live
---

Git checks the repo out with CRLF, so most files under `v6/` end lines with `\r\n`. A `node -e` or `sed` replacement whose search text spans a line break with `\n` does not match and exits cleanly, leaving the file unchanged (a mixed-endings file is the tell: `file` reports "CRLF, LF"). Single-line `sed` substitutions work. For anything spanning lines use the Edit tool, or match with `\r?\n` and make the script throw when the pattern is not found.
