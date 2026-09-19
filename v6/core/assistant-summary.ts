// What the assistant knows, and how it answers. The app works out every figure and writes it as a numbered fact; the
// local model's only job is to choose which facts answer the question, and the app shows those facts word for word.
// So an answer can never carry a number the app did not compute: the owner's rule for the test set (5.5) is 30 of 30
// with no made-up figure, and a 4-billion-parameter model writing its own sentences misread 3 as 5 and 74 as 84.
//
// Built from the view model only, so the assistant cannot see anything the owner cannot.
import { durationText } from './duration.ts';

/** The parts of the view model the facts are built from. The view model satisfies this as it is. */
export interface SummaryInput {
  freshness: { text: string };
  settings: { slaMinutes: number; backlogAfterDays: number };
  queue: { customer: string; accountName: string; location: string; channel: string; waited: number; tone: string; preview: string }[];
  queueTotal: number;
  split: { needsReply: number; backlog: number; closedAutomatically: number };
  figures: { label: string; value: string; unit: string; note: string }[];
  setAside: { why: string }[];
  setAsideTotal: number;
  accounts: { name: string; channel: string; location: string; signedOut: boolean; counted: boolean; reads: boolean }[];
  modules: { name: string; status: string; tone: string }[];
}

/** One fact the assistant can give. Customer facts carry the customer's name, so the screen can offer their chat. */
export interface Fact { id: string; text: string; customer?: string }

const CHANNEL: Record<string, string> = { whatsapp: 'WhatsApp', whatsappbusiness: 'WhatsApp Business', instagram: 'Instagram' };
const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;
const where = (loc: string) => loc || 'No location';
const list = (rows: { customer: string }[]) => rows.map((r) => r.customer).join(', ');

/** Every fact, figures first (F1…), then one per waiting customer (C1…), longest first. */
export function buildFacts(s: SummaryInput, now: Date, maxCustomers = 25): Fact[] {
  const target = s.settings.slaMinutes;
  const late = s.queue.filter((r) => r.tone === 'late');
  const due = s.queue.filter((r) => r.tone === 'due');
  const within = s.queue.filter((r) => r.tone !== 'late' && r.tone !== 'due');
  const fig = (label: string) => s.figures.find((f) => f.label === label);
  const facts: string[] = [];
  const add = (text: string) => facts.push(text);

  add(`The current time is ${now.toLocaleString('en-GB', { weekday: 'long', day: 'numeric', month: 'long', hour: 'numeric', minute: '2-digit', hour12: true })}, and the figures were ${s.freshness.text.toLowerCase()}.`);
  add(`The reply target is ${target} minutes.`);
  add(`${plural(s.queueTotal, 'customer is', 'customers are')} waiting for a reply.`);
  for (const [channel, name] of Object.entries(CHANNEL)) {
    const n = s.queue.filter((r) => r.channel === channel).length;
    if (n || channel !== 'whatsappbusiness') add(`${plural(n, 'customer is', 'customers are')} waiting on ${name}.`);
  }
  add(late.length ? `${plural(late.length, 'customer is', 'customers are')} past the ${target}-minute target: ${list(late)}.` : `Nobody is past the ${target}-minute target.`);
  add(due.length ? `${plural(due.length, 'customer', 'customers')} will pass the target within 5 minutes: ${list(due)}.` : 'Nobody will pass the target in the next 5 minutes.');
  add(within.length ? `${plural(within.length, 'customer is', 'customers are')} still within the target: ${list(within)}.` : 'Nobody waiting is still within the target.');
  const longest = s.queue[0];
  add(longest ? `The longest wait is ${longest.customer}, ${durationText(longest.waited)}, on ${longest.accountName} at ${where(longest.location)}.` : 'Nobody is waiting.');

  const onTime = fig('Answered on time'), reply = fig('First reply'), caught = fig('Caught up');
  add(onTime && onTime.value !== '—' ? `Today ${onTime.value}% of measured replies were within the target (${onTime.note}).` : 'No replies have been measured yet today, so there is no on-time figure.');
  add(reply && reply.value !== '—' ? `The median first reply today is ${reply.value} minutes (${reply.note}).` : 'No replies have been measured yet today, so there is no reply time.');
  if (caught) add(`${caught.value}% of the chats active today have an answer (caught up).`);
  add(`The backlog is ${plural(s.split.backlog, 'customer')}: those who have waited longer than ${plural(s.settings.backlogAfterDays, 'day')}, counted separately from the line.`);
  add(`${plural(s.split.closedAutomatically, 'chat was', 'chats were')} closed by the "ended the chat" rule, for example "ok thanks".`);
  const aside = (k: string) => s.setAside.filter((x) => x.why === k).length;
  add(`${plural(s.setAsideTotal, 'chat is', 'chats are')} set aside (off the line without a reply) in all.`);
  add(`${plural(aside('Handled'), 'chat was', 'chats were')} marked handled.`);
  add(`${plural(aside('Snoozed'), 'chat is', 'chats are')} snoozed.`);
  add(`${plural(aside('Not a customer'), 'chat is', 'chats are')} marked not a customer.`);

  // Each location, and the comparisons worked out here, so the model never has to compare numbers.
  const locations = [...new Set(s.accounts.filter((a) => a.reads && a.counted).map((a) => where(a.location)))];
  const perLocation = locations.map((loc) => {
    const here = s.queue.filter((r) => where(r.location) === loc);
    return { loc, here, late: here.filter((r) => r.tone === 'late').length };
  });
  for (const { loc, here, late: l } of perLocation) {
    const channels = [...new Set(here.map((r) => r.channel))].map((c) => `${here.filter((r) => r.channel === c).length} on ${CHANNEL[c] ?? c}`).join(', ');
    add(here.length
      ? `${loc}: ${plural(here.length, 'customer', 'customers')} waiting (${channels}), ${l} past the target; the longest wait there is ${here[0].customer}, ${durationText(here[0].waited)}.`
      : `${loc}: nobody is waiting.`);
  }
  if (perLocation.length > 1) {
    const most = [...perLocation].sort((a, b) => b.here.length - a.here.length)[0];
    const lateMost = [...perLocation].sort((a, b) => b.late - a.late)[0];
    if (most.here.length) add(`The location with the most customers waiting is ${most.loc}, with ${most.here.length}.`);
    if (lateMost.late) add(`The location with the most customers past the target is ${lateMost.loc}, with ${lateMost.late}.`);
  }

  const out = s.accounts.filter((a) => a.signedOut);
  add(out.length ? `${out.map((a) => a.name).join(', ')} ${out.length === 1 ? 'needs' : 'need'} signing in again; until then its customers are not counted.` : 'Every account is signed in.');
  const broken = s.modules.filter((m) => m.tone === 'late' || m.tone === 'due');
  add(broken.length ? `Readers with problems: ${broken.map((m) => `${m.name} (${m.status.toLowerCase()})`).join(', ')}.` : 'Every channel reader is working.');

  const people: Fact[] = s.queue.slice(0, maxCustomers).map((r, i) => ({
    id: `C${i + 1}`, customer: r.customer,
    text: `${r.customer} is waiting on the account ${r.accountName} at ${where(r.location)}, for ${durationText(r.waited)}, and is ${r.tone === 'late' ? 'past the target' : r.tone === 'due' ? 'due to pass the target within 5 minutes' : 'within the target'}${r.preview ? `; their last message was "${r.preview.slice(0, 120)}"` : ''}.`,
  }));
  return [...facts.map((text, i) => ({ id: `F${i + 1}`, text })), ...people];
}

/** The model's instructions: choose, never write. Declining is left to the check below: offered "none" as a choice,
 *  the small model declined questions it could answer; without it, it chose every answerable fact rightly. */
export function selectionPrompt(facts: Fact[]): string {
  return [
    'You help the owner of a business find facts in an app that shows which customers are waiting for a reply.',
    'Below are the only facts the app has. Choose the ones that answer the question: usually one, at most four.',
    'Reply with JSON only, in this form: {"facts": ["F3"]}.',
    '',
    ...facts.map((f) => `${f.id}: ${f.text}`),
  ].join('\n');
}

/** The second question: does this one fact answer the question? A yes/no check a small model gets right far more often
 *  than choosing among fifty facts, and it is what turns "the closest fact" into "the app doesn't have that figure".
 *  It names what each side is about before it answers: asked for the verdict alone, the model passed "8 chats are set
 *  aside" as the answer to both "the backlog" and "Google reviews". */
export function checkPrompt(question: string, fact: Fact): string {
  return [
    'Does the fact below answer the question? It answers it only if it is about the same thing, the same place and the same time',
    '(today is not yesterday or last week; chats and customers are not reviews, calls or sales; the backlog, set aside,',
    'handled and snoozed are different things; one location is not another), and only if it states the very thing asked for:',
    'the number, the customer, the account or the message the question wants.',
    'First name the topic of each in one to three words (for example "missed calls", "the backlog", "current time"), then decide.',
    'Reply with JSON only: {"question_topic": "...", "fact_topic": "...", "answers": true} or the same with false.',
    '',
    `Question: ${question}`,
    `Fact: ${fact.text}`,
  ].join('\n');
}

/** The check's answer. Anything but a clear yes is a no: a figure shown wrongly is worse than none. */
export function parseCheck(answer: string): boolean {
  try { return (JSON.parse(answer) as { answers?: unknown }).answers === true; } catch { return /^\s*\{?\s*"?answers"?\s*:\s*true/i.test(answer) || /^\s*yes/i.test(answer); }
}

/** How the model is asked: messages in, its reply out, always as JSON. The app's engine and the test run both fit. */
export type Ask = (messages: { role: 'system' | 'user'; content: string }[]) => Promise<string>;

/** A question answered: the model chooses facts, each is checked on its own, and a fact the check turns down is taken
 *  off the list and the model chooses again, up to three rounds. The answer is the facts that passed, word for word, or
 *  "not covered" when none did. */
export async function answerQuestion(question: string, facts: Fact[], ask: Ask, rounds = 3): Promise<Fact[]> {
  let offered = facts;
  for (let round = 0; round < rounds && offered.length; round++) {
    const chosen = parseSelection(await ask([{ role: 'system', content: selectionPrompt(offered) }, { role: 'user', content: question }]), offered);
    if (!chosen.length) return [];
    const kept: Fact[] = [];
    for (const fact of chosen) if (parseCheck(await ask([{ role: 'user', content: checkPrompt(question, fact) }]))) kept.push(fact);
    if (kept.length) return kept;
    offered = offered.filter((f) => !chosen.includes(f));
  }
  return [];
}

/** The facts the model chose, in its order, ignoring anything it made up. Never throws. */
export function parseSelection(answer: string, facts: Fact[]): Fact[] {
  let ids: unknown = [];
  try { ids = (JSON.parse(answer) as { facts?: unknown }).facts; } catch { ids = answer.match(/\b[FC]\d+\b/g) ?? []; }
  const byId = new Map(facts.map((f) => [f.id, f]));
  const list = Array.isArray(ids) ? ids.map((id) => String(id).trim().toUpperCase()) : [];
  const chosen = list.map((id) => byId.get(id)).filter((f): f is Fact => !!f);
  return [...new Set(chosen)].slice(0, 4);
}

export const NOT_COVERED = "The app doesn't have that figure.";

/** The answer the owner sees: the chosen facts word for word, or a plain "not covered". */
export const answerFrom = (chosen: Fact[]) => (chosen.length ? chosen.map((f) => f.text).join(' ') : NOT_COVERED);
