// Sample figures for the screens whose feature is not connected yet: the morning digest, reviews,
// reports, the assistant, workspace and owner screens, and parts of Settings. Every screen that draws from here
// shows a "Sample figures" marker, so none of this can pass for the owner's real data. As each feature is
// wired, its screen switches to the view model and its block here is deleted.
//
// Customers, reviews, members and figures are invented. The same invented week is used everywhere, so the
// numbers agree from one screen to the next.

export const SAMPLE_LOCATIONS = ['F-11', 'DHA-2', 'Men DHA-2'];

export const OWED = [
  { who: 'Zainab T.', account: 'DHA-2 WhatsApp', message: 'Still waiting to hear if 3pm is confirmed', since: 'since 8:40 pm' },
  { who: 'Ayesha K.', account: 'F-11 Instagram', message: 'Package price? Date is 14 Nov', since: 'since 9:55 pm' },
  { who: 'Bilal R.', account: 'Men DHA-2 WhatsApp', message: 'Walk-in possible today?', since: 'since 10:31 pm' },
];

export const YESTERDAY = [
  { location: 'F-11', onTime: 78, trend: [84, 86, 83, 88, 85, 82, 79, 81, 84, 80, 77, 83, 80, 78], median: 13 },
  { location: 'DHA-2', onTime: 80, trend: [90, 88, 91, 87, 86, 89, 84, 85, 83, 86, 82, 84, 81, 80], median: 12 },
  { location: 'Men DHA-2', onTime: 91, trend: [86, 84, 88, 87, 89, 90, 88, 91, 89, 92, 90, 88, 93, 91], median: 7 },
];

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

export const REVIEW_PROFILES = [
  { location: 'F-11', rating: 4.6, total: 991, spread: [620, 210, 71, 38, 52] },
  { location: 'DHA-2', rating: 4.6, total: 1671, spread: [1105, 330, 102, 55, 79] },
  { location: 'Men DHA-2', rating: 4.7, total: 435, spread: [318, 71, 20, 9, 17] },
];

export const REVIEWS = [
  { stars: 1, who: 'Areej S.', location: 'DHA-2', when: '2 days ago', text: 'Waited 40 minutes past my appointment and nobody told me why. I won’t come back.', replied: false },
  { stars: 2, who: 'Hassan M.', location: 'Men DHA-2', when: '3 days ago', text: 'Called twice to book, no answer. Walked in and there was no slot.', replied: false },
  { stars: 2, who: 'Mehwish T.', location: 'F-11', when: '5 days ago', text: 'Price on WhatsApp was different from what I paid at the counter.', replied: false },
  { stars: 3, who: 'Rida A.', location: 'F-11', when: 'last week', text: 'Good service, but it was very crowded on Sunday.', replied: true },
  { stars: 5, who: 'Saad K.', location: 'Men DHA-2', when: 'last week', text: 'Quick and friendly, Imran is great.', replied: true },
];

export const REVIEW_DRAFT = 'Dear Areej, thank you for telling us, and we’re sorry you were kept waiting 40 minutes without an explanation. That isn’t the visit we want anyone to have. Our DHA-2 manager would like to make it right; please message us on 0300 7654321.';

export const WEEKS = ['2 Aug', '', '16 Aug', '', '30 Aug', '', '13 Sep'];

export const ON_TIME_BY_LOCATION = [
  { label: 'Men DHA-2', values: [86, 88, 87, 90, 89, 91, 92], nudge: -6 },
  { label: 'DHA-2', values: [89, 87, 88, 86, 85, 84, 83], dash: '6 4', nudge: 2 },
  { label: 'F-11', values: [85, 86, 83, 82, 80, 79, 77], dash: '1.5 3.5', width: 2.4, nudge: 6 },
];

export type Fact = { label: string; value: string; unit: string; note: string; tone: 'ok' | 'due' | 'late' | 'neutral'; trend?: number[] };

export const WEEK_FACTS: Fact[] = [
  { label: 'Answered on time', value: '84', unit: '%', note: '2 points down', tone: 'due', trend: [88, 87, 89, 86, 86, 85, 84] },
  { label: 'Median first reply', value: '11', unit: 'min', note: '1 min slower', tone: 'due', trend: [9, 10, 9, 10, 11, 10, 11] },
  { label: 'Customers who wrote', value: '1,284', unit: '', note: '6% more', tone: 'neutral', trend: [160, 172, 181, 170, 190, 205, 206] },
  { label: 'Waiting over a day', value: '12', unit: '', note: '4 fewer', tone: 'ok', trend: [18, 17, 16, 16, 15, 13, 12] },
  { label: 'Reopened', value: '37', unit: '', note: 'waiting again after a reply', tone: 'neutral', trend: [4, 6, 5, 5, 6, 5, 6] },
  { label: 'Missed calls', value: '23', unit: '', note: '9 not called back', tone: 'late', trend: [2, 4, 3, 3, 4, 3, 4] },
];

export const MEMBERS = [
  { name: 'Anfal Haider', email: 'you@example.com', role: 'Admin', pcs: 'Office PC, Laptop', seen: 'Now' },
  { name: 'Front desk F-11', email: 'desk.f11@example.com', role: 'Member', pcs: 'F-11 reception', seen: '6 min ago' },
  { name: 'Front desk DHA-2', email: 'desk.dha2@example.com', role: 'Member', pcs: 'DHA-2 reception', seen: '1 h ago' },
  { name: 'Men DHA-2 manager', email: 'men.dha2@example.com', role: 'Member', pcs: 'Manager laptop', seen: 'Yesterday' },
  { name: 'Sadia (left in August)', email: 'sadia.k@example.com', role: 'Removed', pcs: 'F-11 back office', seen: 'Logins wiped 28 Aug' },
  { name: 'ayesha.ops@example.com', email: 'Invited 2 days ago', role: 'Invited', pcs: 'Not signed in yet', seen: '—' },
];

export const WORKSPACES = [
  { name: 'Depilex', admin: 'Anfal Haider', members: 4, pcs: 5, seen: 'Now', active: true },
  { name: 'Northside Pharmacy', admin: 'Mehreen A.', members: 2, pcs: 2, seen: '3 h ago', active: true },
  { name: 'Clifton Auto Service', admin: 'Z. Siddiqui', members: 3, pcs: 3, seen: 'Yesterday', active: true },
  { name: 'Trial: Brightway Tutors', admin: 'owner@example.com', members: 1, pcs: 1, seen: '19 days ago', active: false },
];

export const OPENING_HOURS = [
  ['Monday', '11:00 am', '9:00 pm', ''], ['Tuesday', '11:00 am', '9:00 pm', ''], ['Wednesday', '11:00 am', '9:00 pm', ''],
  ['Thursday', '11:00 am', '9:00 pm', ''], ['Friday', '2:30 pm', '9:30 pm', 'Opens after Jummah'],
  ['Saturday', '11:00 am', '10:00 pm', ''], ['Sunday', '12:00 pm', '10:00 pm', ''],
];

export const HOLIDAYS = [
  { name: '12 Rabi ul Awal', date: 'Sunday 7 September', where: 'Passed', past: true },
  { name: 'Quaid-e-Azam Day', date: 'Thursday 25 December', where: 'All locations', past: false },
  { name: 'New Year’s Day', date: 'Thursday 1 January', where: 'F-11 and DHA-2 only', past: false },
];

/** Alerts whose reader or feature does not exist yet; Settings lists them as not connected. */
export const ALERTS = [
  { title: 'A channel reader stops working', detail: 'After 3 failed reads in a row' },
  { title: 'A one- or two-star review arrives', detail: 'Within an hour of it appearing' },
  { title: 'A missed call has not been returned', detail: 'After 30 minutes' },
];

export const KEPT = [
  { what: 'Account logins', kept: 'Until you sign out or wipe', size: '486 MB', action: 'Wipe an account' },
  { what: 'Who was waiting, reply times', kept: '90 days, then summarised', size: '38 MB', action: 'Clear history' },
  { what: 'Notes, tags and saved replies', kept: 'Until deleted', size: '0.2 MB', action: '' },
  { what: 'Assistant conversations', kept: '30 days', size: '1.1 MB', action: 'Clear' },
  { what: 'Assistant model', kept: 'Until the assistant is turned off', size: '3.3 GB', action: 'Remove' },
];
