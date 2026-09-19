// The assistant's test set (roadmap 5.5): one invented business day, and 30 questions an owner would ask, each with
// what a right answer must contain. Five ask for figures the app does not have; the only right answer to those is
// "The app doesn't have that figure." Pass mark, the owner's: 30 of 30, and no made-up figure anywhere.
//
// Run against the real model with `npm run assistant:test` (needs Ollama running with the model). Everything is
// invented; no customer here is real.
import type { Check } from '../core/assistant-grade.ts';
import type { SummaryInput } from '../core/assistant-summary.ts';

export const NOW = new Date(2026, 8, 19, 15, 41);

const row = (customer: string, location: string, channel: 'whatsapp' | 'instagram', waited: number, tone: 'ok' | 'due' | 'late', preview = '') =>
  ({ customer, accountName: `${location} ${channel === 'instagram' ? 'Instagram' : 'WhatsApp'}`, location, channel, waited, tone, preview });

export const DAY: SummaryInput = {
  freshness: { text: 'Updated just now' },
  settings: { slaMinutes: 15, backlogAfterDays: 7 },
  queue: [
    row('Sample Customer D', 'Main branch', 'whatsapp', 3180, 'late', 'Is the offer still on?'),
    row('Sample Customer K', 'North branch', 'whatsapp', 180, 'late', 'Hello, anyone there?'),
    row('Sample Customer M', 'East branch', 'instagram', 95, 'late'),
    row('Sample Customer E', 'Main branch', 'whatsapp', 40, 'late'),
    row('Sample Customer C', 'Main branch', 'whatsapp', 26, 'late', 'Can I change my booking to next week?'),
    row('Sample Customer H', 'Main branch', 'instagram', 18, 'late'),
    row('Sample Customer B', 'Main branch', 'whatsapp', 12, 'due', 'What time do you open tomorrow?'),
    row('Sample Customer J', 'North branch', 'whatsapp', 11, 'due', 'Price for the full package?'),
    row('Sample Customer I', 'Main branch', 'instagram', 7, 'ok'),
    row('Sample Customer A', 'Main branch', 'whatsapp', 3, 'ok', 'Do you have space on Friday afternoon?'),
  ],
  queueTotal: 10,
  split: { needsReply: 10, backlog: 3, closedAutomatically: 4 },
  figures: [
    { label: 'Answered on time', value: '82', unit: '%', note: '65 replies measured, target 90%' },
    { label: 'First reply', value: '11', unit: 'min median', note: '65 replies measured' },
    { label: 'Caught up', value: '74', unit: '%', note: '' },
  ],
  setAside: [{ why: 'Handled' }, { why: 'Handled' }, { why: 'Snoozed' }, { why: 'Closed by rule' }, { why: 'Closed by rule' }, { why: 'Closed by rule' }, { why: 'Closed by rule' }, { why: 'Not a customer' }],
  setAsideTotal: 8,
  accounts: [
    { name: 'Main branch WhatsApp', channel: 'whatsapp', location: 'Main branch', signedOut: false, counted: true, reads: true },
    { name: 'Main branch Instagram', channel: 'instagram', location: 'Main branch', signedOut: false, counted: true, reads: true },
    { name: 'North branch WhatsApp', channel: 'whatsapp', location: 'North branch', signedOut: false, counted: true, reads: true },
    { name: 'North branch Instagram', channel: 'instagram', location: 'North branch', signedOut: true, counted: true, reads: true },
    { name: 'East branch Instagram', channel: 'instagram', location: 'East branch', signedOut: false, counted: true, reads: true },
    { name: 'Main branch Google', channel: 'googlebusiness', location: 'Main branch', signedOut: false, counted: false, reads: false },
  ],
  modules: [{ name: 'WhatsApp', status: 'Healthy', tone: 'ok' }, { name: 'Instagram', status: 'Healthy', tone: 'ok' }],
};

export const QUESTIONS: Check[] = [
  { question: 'Who has waited longest?', must: ['Sample Customer D', '2 days 5 h'] },
  { question: 'How many customers are waiting right now?', must: ['10'] },
  { question: 'How many customers are past the target?', must: ['6'] },
  { question: 'How many will pass the target in the next five minutes?', must: ['2'] },
  { question: 'What is our reply target?', must: ['15'] },
  { question: 'How many people are waiting on Instagram?', must: ['3'] },
  { question: 'How many people are waiting on WhatsApp?', must: ['7'] },
  { question: 'Which location has the most people waiting?', must: ['Main branch', '7'] },
  { question: 'How many are waiting at North branch?', must: ['2'] },
  { question: 'Who has waited longest at North branch?', must: ['Sample Customer K', '3 h'] },
  { question: 'How many customers at Main branch are past the target?', must: ['4'] },
  { question: 'Is anyone waiting at East branch?', anyOf: ['Sample Customer M', '1 customer'] },
  { question: 'What percentage of replies were on time today?', must: ['82'] },
  { question: 'What is the median first reply time?', must: ['11'] },
  { question: 'How many replies have been measured?', must: ['65'] },
  { question: 'Does any account need signing in again?', must: ['North branch Instagram'] },
  { question: 'How many customers are in the backlog?', must: ['3'] },
  { question: 'How many chats were closed by the "ended the chat" rule?', must: ['4'] },
  { question: 'How many chats are set aside in total?', must: ['8'] },
  { question: 'How many chats were marked handled?', must: ['2'] },
  { question: 'What was the last thing Sample Customer C said?', must: ['change my booking'] },
  { question: 'How long has Sample Customer B been waiting?', must: ['12 min'] },
  { question: 'Which account is Sample Customer M waiting on?', must: ['East branch Instagram'] },
  { question: 'Is Sample Customer A past the target?', anyOf: ['within the target', 'not past', 'no,', 'no.'], mustNot: ['is past the target'] },
  { question: 'What share of today’s active chats have an answer?', must: ['74'] },
  { question: 'Which customer asked about a price?', must: ['Sample Customer J'], mustNot: ['Sample Customer B', 'Sample Customer D'] },
  { question: 'How many Google reviews have no reply?', notCovered: true },
  { question: 'How many missed calls did we have today?', notCovered: true },
  { question: 'What was our on-time percentage last week?', notCovered: true },
  { question: 'How many customers wrote to us yesterday?', notCovered: true },
];
