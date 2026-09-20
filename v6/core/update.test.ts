import { test } from 'node:test';
import assert from 'node:assert/strict';
import { isNewer, notesFrom, readRelease, RELEASES_URL, SETUP_NAME, updateSentence } from './update.ts';

const release = (o: Record<string, unknown> = {}) => ({
  tag_name: 'v6.1.0',
  body: '- Missed calls show whether a customer wrote after calling.\n- The Instagram reader copes with the new inbox layout.',
  assets: [{ name: SETUP_NAME, size: 146_410_859, browser_download_url: 'https://github.com/AnfalHaider/Unified-Messenger/releases/download/v6.1.0/UnifiedMessenger6Setup.exe' }],
  ...o,
});

test('newer means newer, and nothing unreadable is ever newer', () => {
  assert.equal(isNewer('6.1.0', '6.0.0'), true);
  assert.equal(isNewer('6.0.1', '6.0.0'), true);
  assert.equal(isNewer('6.10.0', '6.9.0'), true, 'ten is after nine');
  assert.equal(isNewer('v6.1.0', '6.1.0'), false, 'the same version is not an update');
  assert.equal(isNewer('6.0.0', '6.1.0'), false);
  for (const nonsense of ['', 'latest', 'v6.1.0-beta', '../../etc']) assert.equal(isNewer(nonsense, '6.0.0'), false, nonsense);
});

test('a release is only offered when it is finished, newer, and carries the Windows Setup', () => {
  const found = readRelease(release(), '6.0.0')!;
  assert.equal(found.version, '6.1.0');
  assert.equal(found.size, 146_410_859);
  assert.deepEqual(found.notes, ['Missed calls show whether a customer wrote after calling.', 'The Instagram reader copes with the new inbox layout.']);
  assert.equal(readRelease(release({ draft: true }), '6.0.0'), null, 'a draft is not an update');
  assert.equal(readRelease(release({ prerelease: true }), '6.0.0'), null, 'a pre-release is not offered');
  assert.equal(readRelease(release(), '6.1.0'), null, 'the version already installed is not an update');
  assert.equal(readRelease(release({ assets: [] }), '6.0.0'), null, 'a release with no Setup is not an update');
  assert.equal(readRelease(release({ assets: [{ name: SETUP_NAME, browser_download_url: 'https://elsewhere.example.com/Setup.exe' }] }), '6.0.0'), null,
    'the download must come from GitHub');
  assert.equal(readRelease(null, '6.0.0'), null);
  assert.match(RELEASES_URL, /^https:\/\/api\.github\.com\/repos\//);
});

test('the notes are the release’s own words, short', () => {
  assert.deepEqual(notesFrom('# Heading\n\nA plain line.\n> quoted\n'), ['A plain line.']);
  assert.equal(notesFrom(Array.from({ length: 20 }, (_, i) => `- line ${i}`).join('\n')).length, 6);
  assert.equal(notesFrom(`- ${'x'.repeat(400)}`)[0].length, 160);
});

test('the owner is told in one sentence, and nothing is installed by itself', () => {
  const r = readRelease(release(), '6.0.0')!;
  assert.equal(updateSentence({ phase: 'none' }), 'Unified Messenger is up to date.');
  assert.equal(updateSentence({ phase: 'found', release: r }), 'Version 6.1.0 is ready to download.');
  assert.equal(updateSentence({ phase: 'downloading', release: r, progress: 0.42 }), 'Downloading version 6.1.0… 42%');
  assert.equal(updateSentence({ phase: 'ready', release: r, file: 'C:/x/Setup.exe' }), 'Version 6.1.0 is ready to install.');
  assert.equal(updateSentence({ phase: 'failed', error: 'The update could not be downloaded.' }), 'The update could not be downloaded.');
});
