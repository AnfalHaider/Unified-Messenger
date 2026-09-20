// Sample data for design work only: opening the screens in a plain browser has no main process to ask, so
// they draw this instead and say so on screen. Loaded on demand, so it never ships inside the app's own bundle.
//
// The figures are invented and deliberately match the approved designs, not anyone's real business.
import { defaultSettings } from '../core/config.ts';
import { MODELS, offState, suggestModel } from '../core/assistant.ts';
import { LOST_LOGIN, READER_TIMELINE } from './sample.ts';
import type { QueueRow, SetAsideRow, UiState } from '../app/view-model.ts';

const row = (customer: string, preview: string, waited: number, accountName: string, location: string, channel = 'whatsapp'): QueueRow => {
  const target = 15;
  const remaining = target - waited;
  return {
    accountId: accountName, accountName, location, channel, key: customer, customer, preview, waited,
    status: remaining < 0 ? 'Past target' : remaining <= 5 ? `Due in ${remaining} min` : 'On time',
    tone: remaining < 0 ? 'late' : remaining <= 5 ? 'due' : 'ok',
    fill: Math.min(100, (waited / (target * 3)) * 100),
    target: 100 / 3,
    targetMinutes: target,
    lastActivity: Date.now() - waited * 60_000,
    open: true,
  };
};

const aside = (why: SetAsideRow['why'], customer: string, accountName: string, preview: string, next: string, hoursAgo: number, backInHours?: number): SetAsideRow => ({ canPutBack: why !== 'Closed by rule',
  accountId: accountName, accountName, key: customer, customer, preview, why, next,
  at: Date.now() - hoursAgo * 3_600_000, until: backInHours ? Date.now() + backInHours * 3_600_000 : null,
});

export const PREVIEW_STATE: UiState = {
  theme: 'system',
  route: 'line',
  visible: null,
  meta: '3 locations · 6 accounts read · 1 needs sign-in',
  freshness: { text: 'Updated just now', isStale: false, hasData: true },
  strip: { tone: 'due', text: 'Two customers pass the 15-minute target within 5 minutes.' },
  figures: [
    { label: 'Waiting now', value: '19', unit: 'customers', note: '115 more in backlog', tone: 'late' },
    { label: 'Past target', value: '7', unit: 'over 15 min', note: '2 due within 5 min', tone: 'late' },
    { label: 'Caught up', value: '82', unit: '%', note: 'Of the chats active today, those with an answer', tone: 'due' },
    { label: 'Answered on time', value: '76', unit: '%', note: '453 replies measured, target 90%', tone: 'late' },
    { label: 'First reply', value: '11', unit: 'min median', note: '453 replies measured', tone: 'ok' },
  ],
  split: { needsReply: 19, backlog: 115, closedAutomatically: 270, unreadable: 17 },
  queue: [
    row('Sara M.', 'Can I move my 5pm appointment to tomorrow?', 38, 'WhatsApp · Bookings', 'F-11 Markaz'),
    row('Bilal R.', 'Price for keratin treatment?', 17, 'WhatsApp · Bookings', 'F-11 Markaz'),
    row('Hina A.', 'Do you have slots on Saturday', 13, 'Instagram', 'DHA Phase 2', 'instagram'),
    row('Omar K.', 'Voice message · 0:42', 11, 'WhatsApp · Front desk', 'DHA Phase 2'),
    row('Zara T.', 'Thanks! Also, is parking available?', 6, 'WhatsApp · Bookings', 'F-11 Markaz'),
    row('Ali H.', 'Photo', 3, 'WhatsApp · Front desk', 'DHA Phase 2'),
  ],
  queueTotal: 19,
  queueByLocation: { 'F-11 Markaz': 12, 'DHA Phase 2': 7 },
  setAside: [
    aside('Snoozed', 'Maryam D.', 'DHA-2 WhatsApp', 'Running 10 min late, sorry', 'Returns when the snooze ends', 1, 1),
    aside('Handled', 'Tariq S.', 'Men DHA-2 WhatsApp', 'Thank you bhai', 'Returns if they write again', 2),
    aside('Closed by rule', 'Nida K.', 'F-11 WhatsApp', 'ok thanks', 'Last message was an acknowledgement', 3),
  ],
  setAsideTotal: 3,
  reports: null,
  digest: null,
  openingHours: {
    locations: ['F-11 Markaz', 'DHA Phase 2'].map((name) => ({
      name, accounts: 2,
      hours: { enabled: true, openMinutes: 660, closeMinutes: 1260, week: [720, 660, 660, 660, 660, 870, 660].map((open, d) => ({ open, close: d === 0 || d === 6 ? 1320 : 1260 })) },
    })),
    holidays: [{ name: 'Sample closed day', date: '2026-12-25', locations: [] }],
  },
  locations: [
    { name: 'F-11 Markaz', waiting: 12, onTimePercent: 71, tone: 'late', accounts: 2 },
    { name: 'DHA Phase 2', waiting: 7, onTimePercent: 84, tone: 'due', accounts: 3 },
    { name: 'Gulberg', waiting: 0, onTimePercent: 96, tone: 'ok', accounts: 2 },
  ],
  accounts: [
    { id: 'a1', name: 'WhatsApp · Bookings', channel: 'whatsapp', location: 'F-11 Markaz', waiting: 12, signedOut: false, asleep: false, reads: true, counted: true },
    { id: 'a2', name: 'Google reviews', channel: 'googlebusiness', location: 'F-11 Markaz', waiting: null, signedOut: false, asleep: false, reads: true, counted: true },
    { id: 'a3', name: 'WhatsApp · Front desk', channel: 'whatsapp', location: 'DHA Phase 2', waiting: 5, signedOut: false, asleep: false, reads: true, counted: true },
    { id: 'a4', name: 'Instagram', channel: 'instagram', location: 'DHA Phase 2', waiting: 2, signedOut: false, asleep: false, reads: true, counted: true },
    { id: 'a5', name: 'WhatsApp · Front desk', channel: 'whatsapp', location: 'Gulberg', waiting: 0, signedOut: false, asleep: true, reads: true, counted: true },
    { id: 'a6', name: 'Instagram', channel: 'instagram', location: 'Gulberg', waiting: null, signedOut: true, asleep: false, reads: true, counted: true },
  ],
  reads: true,
  detail: {
    id: 'a1', name: 'WhatsApp · Bookings', location: 'F-11 Markaz', channel: 'whatsapp',
    reads: true, signedOut: false, asleep: false,
    capturedAt: Date.now() - 20_000,
    freshness: { text: 'Updated just now', isStale: false, hasData: true },
    figures: [
      { label: 'Waiting now', value: '12', unit: 'customers', note: '5 past the 15-minute target', tone: 'late' },
      { label: 'First reply', value: '9', unit: 'min median', note: '124 replies measured', tone: 'ok' },
      { label: 'Within target', value: '84', unit: '%', note: 'Target is 15 minutes', tone: 'due' },
      { label: 'Chats read', value: '1,148', unit: 'in the last read', note: 'Updated just now', tone: 'neutral' },
    ],
    daily: [
      { label: 'Thu', median: 12, count: 18 }, { label: 'Fri', median: 10, count: 22 }, { label: 'Sat', median: 11, count: 19 },
      { label: 'Sun', median: 9, count: 12 }, { label: 'Mon', median: 18, count: 31 }, { label: 'Tue', median: 10, count: 24 },
      { label: 'Wed', median: 9, count: 21 },
    ],
    targetMinutes: 15,
    health: [
      { tone: 'ok', title: 'Signed in on this PC', detail: 'The login is kept in this account’s own session and survives a restart.' },
      { tone: 'ok', title: 'Reader working', detail: '1,148 chats in the last read · updated just now' },
      { tone: 'ok', title: 'Awake', detail: 'The page stays open so the numbers keep moving.' },
    ],
    queue: [
      row('Sara M.', 'Can I move my 5pm appointment to tomorrow?', 38, 'WhatsApp · Bookings', 'F-11 Markaz'),
      row('Bilal R.', 'Price for keratin treatment?', 17, 'WhatsApp · Bookings', 'F-11 Markaz'),
      row('Zara T.', 'Thanks! Also, is parking available?', 6, 'WhatsApp · Bookings', 'F-11 Markaz'),
    ],
  },
  modules: [
    { id: 'whatsapp', name: 'WhatsApp', tone: 'ok', status: 'Healthy', detail: '48 good reads since the app started' },
    { id: 'instagram', name: 'Instagram', tone: 'due', status: 'Intermittent', detail: '12 good reads, 3 failed. Last problem: scan returned nothing' },
  ],
  customer: { accountId: 'a1', suggestions: ['Regular', 'Evenings'], byKey: { 'Sara M.': {
    seen: [{ label: 'Now', value: 'Waiting since 4:12 pm' }, { label: '2 Sept', value: 'Answered in 6 min' }, { label: 'Since', value: '19 Aug, 7 times on the line' }],
    note: 'Prefers evening appointments. Asked for a callback last time rather than a message.', tags: ['Regular', 'Evenings'],
  } } },
  assistant: {
    state: { ...offState('gemma3:4b'), phase: 'downloading-model', progress: 0.64, runtime: 'installed' },
    sentence: 'Downloading the gemma3:4b model… 64%', suggested: suggestModel(16), memoryGB: 16, models: MODELS,
  },
  cloud: { phase: 'signed-out' },
  workspace: { phase: 'signed-out' },
  owner: { isOwner: false, workspaces: [] },
  update: { phase: 'none' },
  version: '6.0.0',
  upgraded: false,
  reviews: {
    profiles: [
      { accountId: 'g1', name: 'Main branch Google', location: 'Main branch', rating: 4.6, total: 991, loaded: 50, more: true, unanswered: 3, spread: [34, 9, 3, 1, 3], readAt: Date.now() - 12 * 60_000, signedOut: false },
      { accountId: 'g2', name: 'North branch Google', location: 'North branch', rating: 4.7, total: 435, loaded: 50, more: true, unanswered: 1, spread: [40, 6, 2, 1, 1], readAt: Date.now() - 12 * 60_000, signedOut: false },
    ],
    needing: [
      { id: 'g1:0', accountId: 'g1', location: 'Main branch', reviewer: 'Sample Reviewer A', text: 'Waited forty minutes past my booking and nobody said why.', stars: 1, age: '2 days ago', replied: false },
      { id: 'g2:0', accountId: 'g2', location: 'North branch', reviewer: 'Sample Reviewer B', text: 'Called twice to book and nobody answered.', stars: 2, age: '3 days ago', replied: false },
      { id: 'g1:1', accountId: 'g1', location: 'Main branch', reviewer: 'Sample Reviewer C', text: '', stars: 4, age: 'a day ago', replied: false },
    ],
    recent: [],
  },
  lostLogin: { since: Date.now() - 9.6 * 3_600_000, items: [...LOST_LOGIN] },
  readerStory: { instagram: [...READER_TIMELINE], whatsapp: [...READER_TIMELINE].slice(-2) },
  settings: defaultSettings(),
};
