// Sample data for design work only: opening the screens in a plain browser has no main process to ask, so
// they draw this instead and say so on screen. Loaded on demand, so it never ships inside the app's own bundle.
//
// The figures are invented and deliberately match the approved designs, not anyone's real business.
import type { UiState } from '../app/view-model.ts';

const row = (customer: string, preview: string, waited: number, accountName: string, location: string, channel = 'whatsapp') => {
  const target = 15;
  const remaining = target - waited;
  return {
    accountId: accountName, accountName, location, channel, customer, preview, waited,
    status: remaining < 0 ? 'Past target' : remaining <= 5 ? `Due in ${remaining} min` : 'On time',
    tone: (remaining < 0 ? 'late' : remaining <= 5 ? 'due' : 'ok') as UiState['queue'][number]['tone'],
    fill: Math.min(100, (waited / (target * 3)) * 100),
    target: 100 / 3,
  };
};

export const PREVIEW_STATE: UiState = {
  theme: 'system',
  visible: null,
  greetingName: '',
  meta: '3 locations · 6 accounts read · 1 needs sign-in',
  freshness: { text: 'Updated just now', isStale: false, hasData: true },
  strip: { tone: 'due', text: 'Two customers pass the 15-minute target within 5 minutes.' },
  figures: [
    { label: 'Waiting now', value: '19', unit: 'customers', note: '115 more in backlog', tone: 'late' },
    { label: 'Past target', value: '7', unit: 'over 15 min', note: '2 due within 5 min', tone: 'late' },
    { label: 'Answered on time', value: '82', unit: '%', note: 'Target is 90%', tone: 'due' },
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
  locations: [
    { name: 'F-11 Markaz', waiting: 12, onTimePercent: 71, tone: 'late', accounts: 2 },
    { name: 'DHA Phase 2', waiting: 7, onTimePercent: 84, tone: 'due', accounts: 3 },
    { name: 'Gulberg', waiting: 0, onTimePercent: 96, tone: 'ok', accounts: 2 },
  ],
  accounts: [
    { id: 'a1', name: 'WhatsApp · Bookings', channel: 'whatsapp', location: 'F-11 Markaz', waiting: 12, signedOut: false },
    { id: 'a2', name: 'Google reviews', channel: 'googlebusiness', location: 'F-11 Markaz', waiting: null, signedOut: false },
    { id: 'a3', name: 'WhatsApp · Front desk', channel: 'whatsapp', location: 'DHA Phase 2', waiting: 5, signedOut: false },
    { id: 'a4', name: 'Instagram', channel: 'instagram', location: 'DHA Phase 2', waiting: 2, signedOut: false },
    { id: 'a5', name: 'WhatsApp · Front desk', channel: 'whatsapp', location: 'Gulberg', waiting: 0, signedOut: false },
    { id: 'a6', name: 'Instagram', channel: 'instagram', location: 'Gulberg', waiting: null, signedOut: true },
  ],
  reads: true,
};
