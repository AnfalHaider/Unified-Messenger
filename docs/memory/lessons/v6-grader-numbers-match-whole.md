---
id: v6-grader-numbers-match-whole
date: 2026-09-19
agent: claude
title: An answer grader must match numbers whole; "2" is inside "82" and "12"
triggers: [grader, test set, assistant, includes, substring, numbers, made-up figure, assistant-grade]
files: [v6/core/assistant-grade.ts]
cost: The first test-set runs passed answers that did not contain the required figure.
status: live
---

The first grader checked required figures with `answer.includes('2')`, so an answer saying "82%" passed a question whose right answer was 2. It also treated the fact list's own labels (`F12:` and list numbering) as known numbers, so a figure copied from a label was never flagged as made up. `core/assistant-grade.ts` now pulls whole numbers out of both sides (`numbersIn`) and compares those, after stripping fact ids and list labels. Any grader or assertion about figures in free text needs the same care: compare tokens, not substrings, and test the grader itself with a near-miss ("82" for "2") before trusting a pass rate from it.
