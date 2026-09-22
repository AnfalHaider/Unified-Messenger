// Invented figures for the design preview only: opening the screens in a plain browser has no main process to
// ask, so ui/preview-state.ts draws these and the page says so. **Nothing here reaches the installed app.**
// Every screen in the app now draws the owner's own data; as each one was wired, its block here was deleted.

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
