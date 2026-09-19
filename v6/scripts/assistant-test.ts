// Runs the assistant's test set against the real local model, the way the app asks it: same summary, same options.
// Needs Ollama running with the model: `npm run assistant:test`, or set UM_OLLAMA / UM_MODEL to point elsewhere.
// Prints each question's result and exits 1 below the owner's pass mark (30 of 30, no made-up figure).
import { ASK_OPTIONS } from '../app/assistant.ts';
import { grade } from '../core/assistant-grade.ts';
import { answerFrom, answerQuestion, buildFacts, type Ask } from '../core/assistant-summary.ts';
import { DAY, NOW, QUESTIONS } from '../tests/assistant-set.ts';

const endpoint = process.env.UM_OLLAMA ?? 'http://127.0.0.1:11434/';
const model = process.env.UM_MODEL ?? 'gemma3:4b';
const facts = buildFacts(DAY, NOW);
/** What an answer may quote: every fact the app has. */
const known = facts.map((f) => f.text).join('\n');

/** Asked the way the app asks: same options, JSON only. */
const ask: Ask = async (messages) => {
  const res = await fetch(`${endpoint}api/chat`, { method: 'POST', body: JSON.stringify({ model, stream: false, format: 'json', options: ASK_OPTIONS, messages }) });
  return ((await res.json()) as { message?: { content?: string } }).message?.content?.trim() ?? '';
};

let passed = 0;
const started = Date.now();
for (const [i, check] of QUESTIONS.entries()) {
  // Graded exactly as the owner would read it: the facts that passed, word for word.
  const answer = answerFrom(await answerQuestion(check.question, facts, ask));
  const g = grade(answer, check, known);
  if (g.pass) passed++;
  console.log(`${g.pass ? 'PASS' : 'FAIL'} ${String(i + 1).padStart(2)}. ${check.question}\n        ${answer.replace(/\s+/g, ' ').slice(0, 220)}${g.pass ? '' : `\n        -> ${g.why}`}`);
}
console.log(`\n${passed} of ${QUESTIONS.length} with ${model}, ${Math.round((Date.now() - started) / 1000)} s. Pass mark: ${QUESTIONS.length} of ${QUESTIONS.length}.`);
process.exit(passed === QUESTIONS.length ? 0 : 1);
