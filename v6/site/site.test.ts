// The two public pages (roadmap 6.6). They are what Google's consent screen points at, so the things Google and a
// reader need must stay on them: what is kept, what is sent, the Limited Use wording, and a way to get in touch.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const page = (name: string) => readFileSync(new URL(`./${name}`, import.meta.url), 'utf8');
// Read with the line breaks flattened, so a sentence that wraps in the source still reads as one sentence.
const flat = (s: string) => s.replace(/\s+/g, ' ');
const home = flat(page('index.html')), privacy = flat(page('privacy.html'));
const homeRaw = page('index.html'), privacyRaw = page('privacy.html');
const CONTACT = 'anfalhaider@gmail.com';

test('both pages stand on their own: a title, a description, the stylesheet, and each other', () => {
  for (const [name, html] of [['home', homeRaw], ['privacy', privacyRaw]] as const) {
    assert.match(html, /<html lang="en">/, name);
    assert.match(html, /<title>[^<]{10,}<\/title>/, name);
    assert.match(html, /<meta name="description" content="[^"]{20,}"/, name);
    assert.match(html, /<link rel="stylesheet" href="\/style\.css">/, name);
    assert.ok(html.includes(CONTACT), `${name} says how to get in touch`);
    // Nothing to fetch and nothing to run: no scripts, no third-party requests, no trackers.
    assert.ok(!/<script/i.test(html), `${name} has no scripts`);
    assert.ok(!/https?:\/\/(?!developers\.google\.com)/.test(html.replace(/https?:\/\/[^"']*"?\s*rel="noreferrer"/g, '')), `${name} fetches nothing from elsewhere`);
  }
  assert.ok(home.includes('href="/privacy"'), 'the home page links to the policy');
  assert.ok(privacy.includes('href="/"'), 'the policy links back');
});

test('the policy says what Google requires, and what the app actually does', () => {
  for (const line of [
    'Google API Services', 'Limited Use', // Google's own requirement for apps using its user data
    'name and email', 'never uploaded', 'Removing it', 'not affiliated with WhatsApp, Meta or Google',
  ]) assert.ok(privacy.toLowerCase().includes(line.toLowerCase()), `the policy says: ${line}`);
  assert.match(privacy, /In force from \d{1,2} \w+ \d{4}\./);
  // The claims that must not drift from the app: what leaves the PC is the setup, never customers or logins.
  assert.ok(privacy.includes('never an account login'));
  assert.ok(/No messages and no figures/.test(privacy));
  // The one thing about a customer that a workspace shares (6.4). The policy has to name it, and name what it is.
  assert.ok(/marked handled, snoozed or "not a customer"/.test(privacy));
  assert.ok(/the customer's phone number/.test(privacy), 'the policy says what the identifier is, not just that there is one');
  assert.ok(/not their name, not a message/.test(privacy));
});

test('the home page says plainly that the app only reads', () => {
  assert.ok(home.includes('It reads; you reply.'));
  assert.ok(/never sends a message/.test(home));
});
