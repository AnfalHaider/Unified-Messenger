// Case-for-case port of UnifiedMessenger.Tests/ReplyNeedTests.cs (ReplyNeedTests, CallLogDirectionTests,
// CallOutcomeTests). Examples are verbatim from the owner's real data, spelling included.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { classify, explain, MAX_CLOSING_WORDS, type ReplyNeedReason } from './reply-need.ts';

const DAY = 86_400_000, HOUR = 3_600_000;
const each = (name: string, cases: string[], fn: (c: string) => void) =>
  test(name, () => { for (const c of cases) fn(c); });

// ---- Things that must ALWAYS stay in the count

each('a real customer message is never closed', [
  'V v v unprofessional staff Came for waxing yesterday My girls got bruises on legs',
  'Warna main kahin aur chali jawon', 'Thanku bhot expensive hn ap',
  'Aaj ka nahae pocha rahie wasie kiya charge kerain gaye', 'Any chance of discount',
  'Mujy full body waxing karani hai', 'i want to dye my hair with a group of 4-5 friends',
  'Ill be there around 12.30 pm', 'Near chandni chok', 'Both signature and senior artist',
], (p) => assert.ok(classify(p).needsReply, `'${p}' was dropped from the count`));

each('an acknowledgement with a question attached is still a question', [
  'ok but what time', 'Thanks, kitna charge hoga?', 'okay and can you send the address', 'ji kab available hain', 'sure, price kya hai',
], (p) => assert.deepEqual(classify(p), { needsReply: true, reason: 'asksSomething' }));

each('an opening greeting is an unanswered lead, not a closed chat', ['Hi', 'Salam', 'Hello', 'Assalam o alaikum'],
  (p) => assert.ok(classify(p).needsReply, `'${p}' was treated as a sign-off`));

each('an uncaptioned photo is usually a question with no words in it', ['Photo', 'Voice', 'photo', 'Video', 'Document'],
  (p) => assert.deepEqual(classify(p), { needsReply: true, reason: 'mediaWithoutCaption' }));

test('a chat whose preview could not be read is never judged', () => {
  for (const p of [null, '', '   ', '\n']) assert.deepEqual(classify(p), { needsReply: true, reason: 'noPreviewAvailable' });
});

each('"problem" and "issue" do not make a closing message a complaint', ['no problem', 'Ok no issue', 'Ok fine no problem'],
  (p) => assert.equal(classify(p).needsReply, false));

test('an unrecognised word keeps the whole message', () => {
  assert.ok(classify('ok bhijwa dain').needsReply);
  assert.ok(classify('thanks mel to mel').needsReply);
});

test('a long message is never closed no matter what words it uses', () => {
  assert.ok(classify(Array(MAX_CLOSING_WORDS + 1).fill('ok').join(' ')).needsReply);
});

// ---- Things that should genuinely stop being counted

each('a conversation closer stops being counted', [
  'Ok', 'okay', 'Oky', 'Okk', 'Oka', 'Okie', 'Ji', 'G', 'Gg', 'Ho', 'Sure', 'Done', 'Np', 'Its ok', 'Ok ok', 'Ok miss',
  'Ok Thx', 'Ok thanks', 'Ok thank you', 'Okay tysm', 'Ok JazakAllah', 'Thankyou so much', 'Thanku so much',
  'Your welcome', 'Thank you dear', 'okay, thank you',
], (p) => assert.deepEqual(classify(p), { needsReply: false, reason: 'acknowledgement' }, `'${p}' is still counted`));

test('a reciprocal greeting closes but an opening one does not', () => {
  assert.equal(classify('Walaikum us salam').needsReply, false);
  assert.equal(classify('walaikum salam').needsReply, false);
  assert.ok(classify('Salam').needsReply);
});

each('a reaction emoji is not a waiting customer', ['👍', '😍', '☺️', '👍🏻', '...'],
  (p) => assert.deepEqual(classify(p), { needsReply: false, reason: 'emojiOnly' }));

each('punctuation and emoji do not stop a message being recognised', ['Ok thnx 👍🏻', 'Ok fine no problem🖤😁', 'Your welcome 😊', 'Oky...', 'Sure.....'],
  (p) => assert.equal(classify(p).needsReply, false, `'${p}' was not recognised as a closer`));

test('Urdu script is handled as well as Roman Urdu', () => {
  assert.equal(classify('شکریہ').needsReply, false);
  assert.equal(classify('ٹھیک ہے').needsReply, false);
  assert.ok(classify('کتنا وقت').needsReply);
  assert.ok(classify('قیمت کیا ہے').needsReply);
});

// ---- The property the design rests on

test('asking something always wins over every closing rule', () => {
  for (const closer of ['ok', 'thanks', 'ji', 'walaikum us salam', '👍', 'sure', 'okay tysm']) {
    for (const question of ['kitna', 'what time', '?', 'price', 'kab', 'send address']) {
      const p = `${closer} ${question}`;
      assert.deepEqual(classify(p), { needsReply: true, reason: 'asksSomething' }, `'${p}' was closed despite asking something`);
    }
  }
});

test('every reason has plain English for the owner', () => {
  const reasons: ReplyNeedReason[] = ['asksSomething', 'substantive', 'noPreviewAvailable', 'mediaWithoutCaption', 'acknowledgement',
    'greetingOrSignOff', 'emojiOnly', 'aiJudgedClosed', 'messageNoLongerAvailable', 'systemNotice', 'missedCall', 'outgoingCall', 'callAnswered'];
  for (const r of reasons) {
    assert.ok(explain(r).trim());
    assert.ok(!explain(r).includes('_'));
  }
});

test('a shared contact card is counted and not mistaken for a question', () => {
  assert.deepEqual(classify('Shared a contact'), { needsReply: true, reason: 'mediaWithoutCaption' });
});

// ---- Telling "the message is gone" from "we have not read it yet"

test('a conversation whose message no longer exists is not a waiting customer', () => {
  assert.deepEqual(classify({ preview: '', hasLastMessage: false, type: '', waitingForMs: 57 * DAY }), { needsReply: false, reason: 'messageNoLongerAvailable' });
});

test('an uncaptioned photo is never mistaken for a missing message', () => {
  assert.deepEqual(classify({ preview: '', hasLastMessage: true, type: 'image', waitingForMs: 57 * DAY }), { needsReply: true, reason: 'mediaWithoutCaption' });
});

each('every wordless message kind still counts', ['image', 'video', 'ptt', 'audio', 'document', 'sticker'],
  (type) => assert.ok(classify({ preview: '', hasLastMessage: true, type, waitingForMs: 30 * DAY }).needsReply));

test('a recent blank conversation keeps its place', () => {
  assert.deepEqual(classify({ preview: '', hasLastMessage: false, type: '', waitingForMs: HOUR }), { needsReply: true, reason: 'noPreviewAvailable' });
});

test("an older build's snapshot is never mass-closed", () => {
  assert.deepEqual(classify({ preview: '', hasLastMessage: null, type: '', waitingForMs: 90 * DAY }), { needsReply: true, reason: 'noPreviewAvailable' });
});

test('real text always wins over the message-existence signals', () => {
  assert.deepEqual(classify({ preview: 'kitna charge hoga', hasLastMessage: false, type: '', waitingForMs: 90 * DAY }), { needsReply: true, reason: 'asksSomething' });
});

// ---- Entries that are not messages

each("WhatsApp's own bookkeeping is not a customer waiting", ['e2e_notification', 'protocol', 'notification_template', 'gp2', 'ciphertext', 'keychange'],
  (type) => assert.deepEqual(classify({ preview: '', hasLastMessage: true, type, waitingForMs: 3 * DAY }), { needsReply: false, reason: 'systemNotice' }));

test('a system notice is not counted even if it carries text', () => {
  assert.equal(classify({ preview: 'Your security code with this contact changed', hasLastMessage: true, type: 'e2e_notification', waitingForMs: DAY }).needsReply, false);
});

each('a missed call is still worth returning but is named as a call', ['call_log', 'call'], (type) => {
  const v = classify({ preview: '', hasLastMessage: true, type, waitingForMs: 3 * DAY });
  assert.deepEqual(v, { needsReply: true, reason: 'missedCall' });
  assert.match(explain(v.reason), /called/i);
});

test('an ordinary text message is unaffected by the type checks', () => {
  assert.equal(classify({ preview: 'kitna charge hoga', hasLastMessage: true, type: 'chat', waitingForMs: 2 * HOUR }).reason, 'asksSomething');
  assert.equal(classify({ preview: 'ok thanks', hasLastMessage: true, type: 'chat', waitingForMs: 2 * HOUR }).reason, 'acknowledgement');
});

// ---- Call direction (CallLogDirectionTests)

const call = (type: string, fromMe: boolean | null, callOutcome?: string | null) =>
  classify({ preview: 'Voice call', hasLastMessage: true, type, waitingForMs: 2 * HOUR, fromMe, callOutcome });

each('an incoming call still needs calling back', ['call_log', 'call'],
  (type) => assert.deepEqual(call(type, false), { needsReply: true, reason: 'missedCall' }));

each('a call we placed is not someone to ring back', ['call_log', 'call'],
  (type) => assert.deepEqual(call(type, true), { needsReply: false, reason: 'outgoingCall' }));

test('unknown call direction stays counted', () => {
  assert.deepEqual(call('call_log', null), { needsReply: true, reason: 'missedCall' });
});

test('the outgoing explanation says why nothing is waiting', () => {
  assert.ok(explain('outgoingCall').includes('You called them'));
});

// ---- Call outcome (CallOutcomeTests)

each('an answered call is not someone to ring back', ['Completed', 'AcceptedElsewhere', 'acceptedelsewhere', 'Ongoing'],
  (o) => assert.deepEqual(call('call_log', false, o), { needsReply: false, reason: 'callAnswered' }));

each('a call that never connected still needs returning', ['Missed', 'Failed'],
  (o) => assert.deepEqual(call('call_log', false, o), { needsReply: true, reason: 'missedCall' }));

test('a declined call stays counted', () => {
  assert.deepEqual(call('call_log', false, 'Rejected'), { needsReply: true, reason: 'missedCall' });
});

test('an unknown outcome stays counted', () => {
  for (const o of ['', null, 'SomethingWhatsAppAddedLater']) assert.deepEqual(call('call_log', false, o), { needsReply: true, reason: 'missedCall' });
});

test('direction still wins over outcome', () => {
  assert.equal(call('call_log', true, 'Missed').reason, 'outgoingCall');
  assert.equal(call('call_log', true, 'Completed').reason, 'outgoingCall');
});

test('the answered explanation says someone picked up', () => {
  assert.ok(explain('callAnswered').includes('was answered'));
});
