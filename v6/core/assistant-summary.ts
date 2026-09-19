// What the assistant is told: the figures the screens already show, worked out here and written as numbered facts,
// so a small local model only has to find the right line, never to count or add. Built from the view model only, so
// the assistant cannot see anything the owner cannot, and every number it can quote is one the app computed.
//
// The instructions ask for the facts' own wording and say what to do when a question is not covered: say so. A
// made-up figure fails the assistant's test set outright (roadmap 5.5), whatever else it gets right.
import { durationText } from './duration.ts';

/** The parts of the view model the summary reads. The view model satisfies this as it is. */
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

const CHANNEL: Record<string, string> = { whatsapp: 'WhatsApp', whatsappbusiness: 'WhatsApp Business', instagram: 'Instagram' };
const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;
const where = (loc: string) => loc || 'No location';

/** How many of each channel, most first: "7 on WhatsApp, 2 on Instagram". */
function byChannel(rows: SummaryInput['queue']) {
  const counts = new Map<string, number>();
  for (const r of rows) counts.set(r.channel, (counts.get(r.channel) ?? 0) + 1);
  return [...counts].sort((a, b) => b[1] - a[1]).map(([c, n]) => `${n} on ${CHANNEL[c] ?? c}`).join(', ');
}

/** The facts, numbered, and the instructions that go with them. `now` is when the question is asked. */
export function buildSummary(s: SummaryInput, now: Date, maxCustomers = 25): string {
  const target = s.settings.slaMinutes;
  const late = s.queue.filter((r) => r.tone === 'late');
  const due = s.queue.filter((r) => r.tone === 'due');
  const fig = (label: string) => s.figures.find((f) => f.label === label);
  const facts: string[] = [];
  const add = (line: string) => facts.push(line);

  add(`It is ${now.toLocaleString('en-GB', { weekday: 'long', day: 'numeric', month: 'long', hour: 'numeric', minute: '2-digit', hour12: true })}. The figures were ${s.freshness.text.toLowerCase()}.`);
  add(`The reply target is ${target} minutes.`);
  add(`Customers waiting for a reply now: ${s.queueTotal}${s.queue.length ? ` (${byChannel(s.queue)})` : ''}.`);
  add(`Customers past the ${target}-minute target: ${late.length}. Customers who will pass it within 5 minutes: ${due.length}.`);
  const longest = s.queue[0];
  add(longest ? `The longest wait is ${longest.customer}, waiting ${durationText(longest.waited)}, on ${longest.accountName} at ${where(longest.location)}.` : 'Nobody is waiting.');
  const onTime = fig('Answered on time'), reply = fig('First reply'), caught = fig('Caught up');
  add(onTime && onTime.value !== '—' ? `Answered on time: ${onTime.value}% (${onTime.note}).` : 'Answered on time: no replies have been measured yet.');
  add(reply && reply.value !== '—' ? `Median first reply: ${reply.value} minutes (${reply.note}).` : 'Median first reply: no replies have been measured yet.');
  if (caught) add(`Caught up: ${caught.value}% of the chats active today have an answer.`);
  add(`Backlog: ${s.split.backlog} customers have waited longer than ${plural(s.settings.backlogAfterDays, 'day')} and are counted separately, not on the line.`);
  add(`Chats closed by the "ended the chat" rule (for example "ok thanks"): ${s.split.closedAutomatically}.`);
  const kinds = ['Handled', 'Snoozed', 'Closed by rule', 'Not a customer'].map((k) => `${s.setAside.filter((x) => x.why === k).length} ${k.toLowerCase()}`);
  add(`Set aside (off the line without a reply): ${s.setAsideTotal} in all: ${kinds.join(', ')}.`);

  const locations = [...new Set(s.accounts.filter((a) => a.reads && a.counted).map((a) => where(a.location)))];
  for (const loc of locations) {
    const here = s.queue.filter((r) => where(r.location) === loc);
    const lateHere = here.filter((r) => r.tone === 'late').length;
    add(`${loc}: ${plural(here.length, 'customer')} waiting${here.length ? ` (${byChannel(here)})` : ''}, ${lateHere} past the target${here[0] ? `; longest ${here[0].customer}, ${durationText(here[0].waited)}` : ''}.`);
  }

  const out = s.accounts.filter((a) => a.signedOut);
  add(out.length ? `Accounts that need signing in again, so their customers are not being counted: ${out.map((a) => a.name).join(', ')}.` : 'Every account is signed in.');
  const broken = s.modules.filter((m) => m.tone === 'late' || m.tone === 'due');
  if (broken.length) add(`Readers with problems: ${broken.map((m) => `${m.name} (${m.status.toLowerCase()})`).join(', ')}.`);

  const shown = s.queue.slice(0, maxCustomers);
  const customers = shown.map((r, i) =>
    `${i + 1}. ${r.customer}: waiting ${durationText(r.waited)} on ${r.accountName} at ${where(r.location)}, ${r.tone === 'late' ? 'past the target' : r.tone === 'due' ? 'due to pass the target within 5 minutes' : 'within the target'}${r.preview ? `; last message: "${r.preview.slice(0, 120)}"` : ''}.`);

  return [
    'You are the assistant inside Unified Messenger, an app a business uses to see which customers are waiting for a reply on WhatsApp and Instagram.',
    'Answer ONLY from the facts below. Quote numbers and names exactly as the facts give them. Never add, subtract, estimate or guess a figure.',
    'If the facts do not answer the question, say "The app doesn\'t have that figure." and nothing more about it.',
    'Keep answers to one to three short sentences. You cannot send messages or change anything; the owner does that.',
    '',
    'FACTS:',
    ...facts.map((f, i) => `F${i + 1}. ${f}`),
    '',
    shown.length ? `WAITING CUSTOMERS, longest first${s.queueTotal > shown.length ? ` (the first ${shown.length} of ${s.queueTotal})` : ''}:` : 'WAITING CUSTOMERS: none.',
    ...customers,
  ].join('\n');
}
