// Port of Services/Oversight/ReplyNeed.cs: does a customer's last message actually leave something to answer?
// The v5 file carries the measured history behind every rule (466 "waiting" of which 41 had asked anything).
// Bias is one-directional: close only on positive evidence; anything unrecognised stays counted.

export type ReplyNeedReason =
  | 'asksSomething' | 'substantive' | 'noPreviewAvailable' | 'mediaWithoutCaption'
  | 'acknowledgement' | 'greetingOrSignOff' | 'emojiOnly' | 'aiJudgedClosed'
  | 'messageNoLongerAvailable' | 'systemNotice' | 'missedCall' | 'outgoingCall' | 'callAnswered';

export interface ReplyNeedVerdict { needsReply: boolean; reason: ReplyNeedReason }

export interface LastMessage {
  preview?: string | null;
  hasLastMessage?: boolean | null; // null/undefined = unknown
  type?: string | null; // WhatsApp message type: chat, image, ptt, call_log, e2e_notification…
  waitingForMs?: number | null;
  fromMe?: boolean | null; // only consulted for calls
  callOutcome?: string | null; // Missed, Completed, AcceptedElsewhere, Rejected, Ongoing, Failed
}

const set = (words: string[]) => new Set(words.map((w) => w.toLowerCase()));

const CLOSING_WORDS = set([
  // Assent / acknowledgement
  'ok', 'okay', 'okey', 'oky', 'okk', 'okkk', 'oka', 'okie', 'k', 'kk', 'kay',
  'yes', 'yep', 'yup', 'yeah', 'ya', 'sure', 'fine', 'alright', 'right', 'correct',
  'han', 'haan', 'ha', 'hn', 'hnn', 'ho', 'hoo', 'ji', 'jee', 'g', 'gg', 'jji',
  'acha', 'achha', 'achaa', 'acchaa', 'achi', 'theek', 'thik', 'teek', 'tk', 'sahi', 'bilkul',
  'done', 'noted', 'got', 'gotcha', 'understood', 'roger',
  // Thanks, including typos and elongations measured in the live queue
  'thanks', 'thank', 'thankyou', 'thanku', 'thankz', 'thnx', 'thnks', 'thx', 'tks', 'tysm', 'ty',
  'ohky', 'okhy', 'okhay', 'okies', 'okii', 'okiii', 'thankd', 'thnkx', 'thanx',
  'appreciated', 'appreciate', 'acknowledged', 'acknowledge', 'ackn',
  'yupp', 'yuppp', 'yess', 'yesss', 'yaa', 'haanji', 'hanji',
  'shukriya', 'shukria', 'shukrya', 'jazakallah', 'jazakhallah', 'jzk', 'jazak',
  // Praise / warmth that closes rather than asks
  'great', 'good', 'nice', 'perfect', 'lovely', 'excellent', 'awesome', 'cool', 'super',
  'welcome', 'wc', 'np', 'problem', 'issue', 'worries', 'mention', 'oh', 'ohh', 'hmm', 'hm',
  // Greetings and sign-offs
  'salam', 'salaam', 'slam', 'assalam', 'assalamualaikum', 'asalamualaikum', 'walaikum',
  'walaikumsalam', 'wsalam', 'aoa', 'hi', 'hello', 'hey',
  'bye', 'goodbye', 'gudbye', 'tc', 'care', 'take', 'cu', 'khuda', 'allah', 'hafiz',
  'inshallah', 'insha', 'mashallah', 'masha', 'ameen', 'amin', 'alhamdulillah',
  // Glue words that must not by themselves stop a phrase counting as a closer
  'so', 'much', 'very', 'you', 'u', 'your', 'youre', 'its', 'it', 'is', 'no', 'not', 'for',
  'the', 'a', 'an', 'and', 'my', 'me', 'i', 'am', 'be', 'will', 'then', 'now', 'too', 'also',
  'dear', 'sis', 'sir', 'madam', 'mam', 'miss', 'bro', 'bhai', 'baji', 'api', 'apa', 'ap',
  'koi', 'baat', 'nahi', 'nahin', 'masla', 'kea', 'hai', 'he', 'hy',
  // Urdu script
  'شکریہ', 'ٹھیک', 'ہے', 'جی', 'اچھا', 'ہاں', 'اوکے', 'بہت', 'السلام', 'علیکم', 'وعلیکم',
  'جزاک', 'اللہ', 'حافظ', 'انشاء', 'ماشاء', 'آمین', 'خدا', 'بھی', 'ٹھیکہے',
]);

// Any of these means the customer wants something back, and overrides every closing rule.
// "contact", "problem" and "issue" are deliberately absent (see ReplyNeed.cs for the collisions).
const REQUEST_WORDS = set([
  'what', 'when', 'where', 'which', 'who', 'whom', 'whose', 'why', 'how',
  'can', 'could', 'would', 'should', 'shall', 'may', 'will',
  'do', 'does', 'did', 'is', 'are', 'was', 'were', 'have', 'has',
  'please', 'plz', 'pls', 'kindly', 'want', 'need', 'needed', 'looking', 'interested',
  'send', 'share', 'tell', 'let', 'confirm', 'check', 'help', 'reply', 'call',
  'available', 'availability', 'book', 'booking', 'bookings', 'appointment', 'appointments',
  'price', 'prices', 'pricing', 'rate', 'rates', 'charge', 'charges', 'cost', 'fee', 'fees',
  'discount', 'offer', 'package', 'deal', 'timing', 'timings', 'time', 'open', 'close', 'closed',
  'slot', 'slots', 'address', 'location', 'number', 'menu', 'list', 'detail', 'details', 'info',
  'cancel', 'reschedule', 'change', 'refund', 'complaint', 'wrong', 'bad',
  // Roman Urdu
  'kya', 'kia', 'kiya', 'kb', 'kab', 'kabhi', 'kahan', 'kahaan', 'kaha', 'kidhar',
  'kaise', 'kaisay', 'kese', 'kesay', 'kitna', 'kitni', 'kitne', 'kitnay',
  'konsa', 'kaunsa', 'kon', 'kaun', 'chahiye', 'chahye', 'chaiye', 'chahie', 'chahta', 'chahti',
  'karani', 'karana', 'karwana', 'karwani', 'karna', 'karni', 'krna', 'krni', 'krwana',
  'milega', 'milegi', 'milta', 'milti', 'mil', 'batao', 'bataen', 'bata', 'batayen', 'batadein',
  'bhej', 'bhejo', 'bhejein', 'bhejden', 'dedo', 'dedein', 'chahiay', 'zarurat', 'zaroorat',
  'raate', 'kimat', 'qeemat', 'waqt',
  // Urdu script
  'کیا', 'کب', 'کہاں', 'کیسے', 'کتنا', 'کتنی', 'کتنے', 'کون', 'چاہیے', 'چاہئے',
  'بھیج', 'بتائیں', 'بتاو', 'ملے', 'قیمت', 'ریٹ', 'وقت', 'پتہ', 'نمبر',
]);

// WhatsApp's stand-in for a message with no text. Counted, not closed: an uncaptioned photo is usually a question.
const MEDIA_PLACEHOLDERS = set([
  'photo', 'photos', 'image', 'video', 'videos', 'voice', 'audio', 'voice message', 'sticker',
  'gif', 'document', 'file', 'contact', 'location', 'poll', 'message', 'shared a contact',
]);

const GREETING_WORDS = set([
  'salam', 'salaam', 'slam', 'assalam', 'assalamualaikum', 'asalamualaikum', 'walaikum',
  'walaikumsalam', 'wsalam', 'aoa', 'hi', 'hello', 'hey', 'bye', 'goodbye', 'gudbye',
  'tc', 'care', 'take', 'cu', 'khuda', 'allah', 'hafiz', 'us', 'o', 'sir', 'madam', 'mam',
  'miss', 'dear', 'bhai', 'baji', 'api', 'apa',
  'السلام', 'علیکم', 'وعلیکم', 'خدا', 'حافظ',
]);

// Opens a conversation, so on its own it never closes one: an unanswered "Salam" is a new lead.
const OPENING_GREETINGS = set(['hi', 'hello', 'hey', 'salam', 'salaam', 'slam', 'assalam', 'assalamualaikum', 'asalamualaikum', 'aoa', 'السلام', 'علیکم']);
// "And upon you": can only ever answer a greeting, so it does close one.
const RECIPROCAL_GREETINGS = set(['walaikum', 'walaikumsalam', 'wsalam', 'walikum', 'waalaikum', 'وعلیکم']);

const SYSTEM_NOTICE_TYPES = set(['e2e_notification', 'protocol', 'notification_template', 'gp2', 'broadcast_notification', 'ciphertext', 'revoked', 'keychange', 'payment_notification']);
const ANSWERED_CALL_OUTCOMES = set(['completed', 'acceptedelsewhere', 'ongoing']); // Rejected and Failed stay counted
const CALL_TYPES = set(['call_log', 'call']);
const MEDIA_TYPES = set(['image', 'video', 'ptt', 'audio', 'document', 'sticker', 'vcard', 'location']);

export const MAX_CLOSING_WORDS = 5;
export const MISSING_MESSAGE_IS_GONE_AFTER_MS = 2 * 24 * 60 * 60 * 1000;

const is = (list: Set<string>, value?: string | null) => list.has((value ?? '').trim().toLowerCase());
// Matches C# char.IsLetterOrDigit: letters plus decimal digits, in every script.
const WORD_CHAR = /[\p{L}\p{Nd}]/u;

export const isSystemNoticeType = (type?: string | null) => is(SYSTEM_NOTICE_TYPES, type);
export const isAnsweredCallOutcome = (outcome?: string | null) => is(ANSWERED_CALL_OUTCOMES, outcome);
export const isCallType = (type?: string | null) => is(CALL_TYPES, type);
export const isMediaType = (type?: string | null) => is(MEDIA_TYPES, type);

/** Lower-cased words: letters and digits in every script; apostrophes dropped ("you're" -> "youre"). */
export function tokenize(text: string): string[] {
  const words: string[] = [];
  let current = '';
  for (const c of text) {
    if (WORD_CHAR.test(c)) current += c.toLowerCase();
    else if (c === "'" || c === '’') continue;
    else if (current) { words.push(current); current = ''; }
  }
  if (current) words.push(current);
  return words;
}

export function asksSomething(preview?: string | null): boolean {
  const text = (preview ?? '').trim();
  if (!text) return false;
  if (/[?？؟]/.test(text)) return true;
  return tokenize(text).some((w) => REQUEST_WORDS.has(w));
}

export const isMediaPlaceholder = (text: string) => MEDIA_PLACEHOLDERS.has(text.trim().replace(/[.! ]+$/, '').toLowerCase());

const isClosingWord = (w: string) => CLOSING_WORDS.has(w) || GREETING_WORDS.has(w);

function classifyClosing(words: string[]): ReplyNeedReason | null {
  if (words.some((w) => RECIPROCAL_GREETINGS.has(w))) return 'greetingOrSignOff';
  let acknowledgement = false, openingGreeting = false;
  for (const w of words) {
    if (OPENING_GREETINGS.has(w)) openingGreeting = true;
    else if (!GREETING_WORDS.has(w) && CLOSING_WORDS.has(w)) acknowledgement = true;
  }
  // "Salam" alone opens a conversation; "ok salam" ends one.
  if (openingGreeting && !acknowledgement) return null;
  return acknowledgement ? 'acknowledgement' : 'greetingOrSignOff';
}

const verdict = (needsReply: boolean, reason: ReplyNeedReason): ReplyNeedVerdict => ({ needsReply, reason });

export function classify(input: string | null | undefined | LastMessage): ReplyNeedVerdict {
  const m: LastMessage = typeof input === 'object' && input !== null ? input : { preview: input };
  const text = (m.preview ?? '').trim();

  // Not messages: WhatsApp's bookkeeping, and calls, are judged by type before any text.
  if (isSystemNoticeType(m.type)) return verdict(false, 'systemNotice');
  if (isCallType(m.type)) {
    if (m.fromMe === true) return verdict(false, 'outgoingCall');
    return isAnsweredCallOutcome(m.callOutcome) ? verdict(false, 'callAnswered') : verdict(true, 'missedCall');
  }

  if (!text) {
    // Positively reported gone, and far older than any sync delay.
    if (m.hasLastMessage === false && m.waitingForMs != null && m.waitingForMs > MISSING_MESSAGE_IS_GONE_AFTER_MS) {
      return verdict(false, 'messageNoLongerAvailable');
    }
    return isMediaType(m.type) ? verdict(true, 'mediaWithoutCaption') : verdict(true, 'noPreviewAvailable');
  }

  // A question beats every closing rule, so "ok but what time" is never filed as an "ok".
  if (asksSomething(text)) return verdict(true, 'asksSomething');
  if (isMediaPlaceholder(text)) return verdict(true, 'mediaWithoutCaption');
  if (!WORD_CHAR.test(text)) return verdict(false, 'emojiOnly');

  const words = tokenize(text);
  if (words.length === 0 || words.length > MAX_CLOSING_WORDS) return verdict(true, 'substantive');
  // Every word must be a closer; one unrecognised word keeps the chat counted.
  if (!words.every(isClosingWord)) return verdict(true, 'substantive');

  const reason = classifyClosing(words);
  return reason ? verdict(false, reason) : verdict(true, 'substantive');
}

export function explain(reason: ReplyNeedReason): string {
  switch (reason) {
    case 'acknowledgement': return 'Last message was an acknowledgement';
    case 'greetingOrSignOff': return 'Last message was a greeting or sign-off';
    case 'emojiOnly': return 'Last message was only an emoji';
    case 'aiJudgedClosed': return 'Conversation looks finished';
    case 'asksSomething': return 'Customer asked something';
    case 'mediaWithoutCaption': return 'Customer sent a photo, voice note or contact';
    case 'messageNoLongerAvailable': return 'The message no longer exists — deleted or expired';
    case 'systemNotice': return 'Not a message — a WhatsApp system notice';
    case 'missedCall': return 'Customer called and did not get through';
    case 'outgoingCall': return 'You called them — nothing is waiting on a reply';
    case 'callAnswered': return 'Customer called and the call was answered';
    case 'noPreviewAvailable': return 'Message could not be read';
    default: return 'Customer sent a message';
  }
}
