// Going to a conversation acts on a real customer's chat, so the page scripts are held to what they may and may
// not do by reading them, the way v5's tests held its Instagram path. The behaviour itself is exercised against
// invented pages in tests/smoke.spec.ts.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { instagram } from './instagram/index.ts';
import { whatsapp } from './whatsapp/index.ts';

const HERE = dirname(fileURLToPath(import.meta.url));
const source = (file: string) => readFileSync(join(HERE, file), 'utf8')
  // Comments explain the bans by naming them; only code is held to them.
  .replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');

test('a name with quotes and line breaks cannot break out of the focus expression', () => {
  const nasty = { key: 'x"); alert(1); ("', name: 'O\'Brien "Jr"\n ', phone: '' };
  for (const module of [whatsapp, instagram]) {
    const expression = module.focus!(nasty);
    // It parses as one expression, and the strings arrive intact.
    assert.doesNotThrow(() => new Function(`return (${expression});`), module.id);
    assert.ok(expression.includes(JSON.stringify(nasty.name)), module.id);
  }
});

test('the Instagram focus never opens a thread, never clicks and never touches a message box', () => {
  const code = source('instagram/instagram-focus.js');
  for (const banned of ['.click(', 'dispatchEvent(new MouseEvent', 'PointerEvent', 'contenteditable', 'role="textbox"', 'KeyboardEvent', "'Enter'", 'sendMessage', '.submit(']) {
    assert.ok(!code.includes(banned), `instagram-focus.js must not contain ${banned}`);
  }
  // A thread path appears once, as something to detect and leave; the only destination is the inbox.
  assert.equal(code.split('THREAD_PATH').length - 1, 2, 'THREAD_PATH is declared and checked, never assigned to location');
  assert.match(code, /location\.assign\(location\.origin \+ '\/direct\/inbox\/'\)/);
});

test('the WhatsApp focus opens a chat and types into nothing', () => {
  const code = source('whatsapp/whatsapp-focus.js');
  for (const banned of ['contenteditable', 'footer', 'KeyboardEvent', "'Enter'", 'sendMessage', 'sendText', '.submit(', 'execCommand', 'HTMLInputElement', '.value', "new Event('input'"]) {
    assert.ok(!code.includes(banned), `whatsapp-focus.js must not contain ${banned}`);
  }
  // The one WhatsApp command it may call is the one that opens a chat.
  const commands = [...code.matchAll(/cmd\.(\w+)/g)].map((m) => m[1]);
  assert.deepEqual([...new Set(commands)], ['openChatBottom']);
});
