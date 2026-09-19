import { test } from 'node:test';
import assert from 'node:assert/strict';
import { engineSentence, hasModel, offState, pullFraction, runtimeCandidates, suggestModel } from './assistant.ts';

test('the Ollama the owner installed is tried first, then v5\u2019s, then the app\u2019s own', () => {
  const c = runtimeCandidates({ localAppData: 'C:\\Users\\x\\AppData\\Local', data: 'C:\\Users\\x\\AppData\\Roaming\\unified-messenger-v6' });
  assert.deepEqual(c.map((r) => r.kind), ['installed', 'v5', 'bundled']);
  assert.equal(c[0].exe, 'C:\\Users\\x\\AppData\\Local\\Programs\\Ollama\\ollama.exe');
  assert.equal(c[0].models, null, 'the owner\u2019s own Ollama keeps its own models folder');
  assert.equal(c[1].models, 'C:\\Users\\x\\AppData\\Local\\UnifiedMessenger\\ollama\\models');
  assert.equal(c[2].exe, 'C:\\Users\\x\\AppData\\Roaming\\unified-messenger-v6\\ollama\\runtime\\ollama.exe');
});

test('the model suggested fits the PC, and never the large one', () => {
  assert.equal(suggestModel(4).model, 'gemma3:1b');
  assert.equal(suggestModel(8).model, 'gemma3:4b');
  assert.equal(suggestModel(32).model, 'gemma3:4b', 'answers must stay quick on a busy front-desk PC');
  assert.equal(suggestModel(2).model, 'gemma3:1b', 'a PC below every minimum still gets the smallest, with the warning in Settings');
});

test('a model is found whatever tag spelling Ollama uses', () => {
  assert.ok(hasModel(['gemma3:4b', 'llama3.2:3b'], 'gemma3:4b'));
  assert.ok(hasModel(['phi3:latest'], 'phi3'));
  assert.ok(!hasModel(['gemma3:1b'], 'gemma3:4b'));
});

test('download progress comes from the lines that carry sizes', () => {
  assert.equal(pullFraction({ status: 'pulling manifest' }), null);
  assert.equal(pullFraction({ status: 'pulling abc', total: 200, completed: 50 }), 0.25);
  assert.equal(pullFraction({ total: 100, completed: 150 }), 1);
});

test('every state is said in plain words', () => {
  const s = offState('gemma3:4b');
  assert.match(engineSentence(s), /Nothing is downloaded/);
  assert.equal(engineSentence({ ...s, phase: 'ready', runtime: 'installed' }), 'Ready, using gemma3:4b through the Ollama installed on this PC.');
  assert.equal(engineSentence({ ...s, phase: 'downloading-model', progress: 0.42 }), 'Downloading the gemma3:4b model\u2026 42%');
  assert.equal(engineSentence({ ...s, phase: 'error', problem: 'Ollama did not start.' }), 'Ollama did not start.');
});
