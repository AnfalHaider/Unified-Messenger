// Sample figures for the screens whose feature is not connected yet: reviews,
// the assistant, workspace and owner screens, the customer panel and parts of Settings and Accounts. Every screen that draws from here
// shows a "Sample figures" marker, so none of this can pass for the owner's real data. As each feature is
// wired, its screen switches to the view model and its block here is deleted.
//
// Customers, reviews, members and figures are invented. The same invented week is used everywhere, so the
// numbers agree from one screen to the next.

export const CUSTOMER = {
  history: [['Today', 'Waiting now'], ['2 Sept', 'Answered in 6 min'], ['19 Aug', 'Answered in 41 min'], ['Since', 'July, 7 conversations']],
  tags: ['Regular', 'Evenings'],
  note: 'Prefers evening appointments. Asked for a callback last time rather than a message.',
  saved: [
    { title: 'Reschedule', body: 'Of course. Tomorrow at the same time is free…' },
    { title: 'Prices', body: 'Our current price list is attached…' },
    { title: 'Location', body: 'We are at Shop 4, F-11 Markaz…' },
  ],
};

export const READER_TIMELINE = [
  { at: '10:14', tone: 'late', title: 'First unrecognised read', detail: 'F-11 Instagram. The page loaded and was signed in, but the inbox list had a new layout.' },
  { at: '10:15', tone: 'late', title: 'Same at DHA-2 and Men DHA-2', detail: 'Three accounts, one cause. Reported as the reader.' },
  { at: '10:16', tone: 'neutral', title: 'Instagram figures hidden', detail: 'Waiting counts for Instagram show “not reading” instead of 0.' },
  { at: '10:16', tone: 'ok', title: 'WhatsApp checked separately', detail: '148 good reads since, none failed.' },
  { at: '12:10', tone: 'neutral', title: 'Still retrying every 5 minutes', detail: 'It recovers by itself if Instagram changes back.' },
] as const;

export const LOST_LOGIN = [
  { at: '11:23 pm', tone: 'ok', title: 'Good read', detail: '500 chats, 6 waiting. Nothing unusual.' },
  { at: '11:24 pm', tone: 'ok', title: 'Good read', detail: '500 chats, 6 waiting.' },
  { at: '11:25 pm', tone: 'due', title: 'Page reloaded itself', detail: 'WhatsApp refreshed the page. The app did not ask it to.' },
  { at: '11:26 pm', tone: 'late', title: 'QR code on screen', detail: 'The link-a-device screen replaced the chats. Marked Sign in needed; figures hidden.' },
  { at: 'Now', tone: 'neutral', title: 'Still signed out, 9 h 36 min', detail: 'Messages sent since then are not counted anywhere.' },
] as const;

export type Fact = { label: string; value: string; unit: string; note: string; tone: 'ok' | 'due' | 'late' | 'neutral'; trend?: number[] };

export const KEPT = [
  { what: 'Account logins', kept: 'Until you sign out or wipe', size: '486 MB', action: 'Wipe an account' },
  { what: 'Who was waiting, reply times', kept: '90 days, then summarised', size: '38 MB', action: 'Clear history' },
  { what: 'Notes, tags and saved replies', kept: 'Until deleted', size: '0.2 MB', action: '' },
  { what: 'Assistant conversations', kept: '30 days', size: '1.1 MB', action: 'Clear' },
  { what: 'Assistant model', kept: 'Until the assistant is turned off', size: '3.3 GB', action: 'Remove' },
];
