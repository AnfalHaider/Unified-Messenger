---
id: v6-small-model-chooses-app-facts
date: 2026-09-19
agent: claude
title: A 4B local model cannot be trusted to write figures; make it choose the app's facts by id, check each, and choose again
triggers: [assistant, ollama, gemma3, local model, hallucination, made-up figure, test set, 5.5, selection, prompt]
files: [v6/core/assistant-summary.ts, v6/scripts/assistant-test.ts, v6/tests/assistant-set.ts]
cost: Four design rounds against the real model to go from 24 to 30 of 30.
status: live
---

Given the figures and told to answer only from them, gemma3:4b still misread them in its own sentences (3 as 5, 74 as 84, the wrong customer): 24 of 30. Prompting harder does not fix this. What reached 30 of 30, each step measured with `npm run assistant:test`:

1. **The model only chooses.** The app writes every figure as a numbered sentence; the model replies `{"facts": ["F3"]}` (`format: 'json'`) and the owner sees those sentences word for word. An invented figure becomes impossible, not unlikely.
2. **Do not offer "none" as a choice.** Without it the model never declined; with it, it declined questions it could answer. Declining belongs to a separate step.
3. **Check each chosen fact on its own**, a yes/no question the model gets right far more often than choosing among 50. Asked for the verdict alone it still said yes too easily; it improved when it had to name both topics first (in one to three words, or it just echoes the sentences) and when the fact had to state "the very thing asked for".
4. **Choose again without a fact the check turned down**, up to three rounds; then "The app doesn't have that figure."
5. **Word facts topic first** ("The backlog is 3 customers…", "X is waiting on the account Y…"). A topic buried mid-sentence was what the model missed.

Tried and dropped: narrowing the list by word overlap before choosing made it worse. Keep one shared `answerQuestion` for the app and the test run, or the test measures a different path from the one the owner uses. Check any change against reworded questions outside the set (10 of 10 here) so the set does not become the thing being tuned.
