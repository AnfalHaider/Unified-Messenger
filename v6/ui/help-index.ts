// Which help page belongs to which screen, and the order the Help screen lists them in. Plain data with no bundler
// in it, so the unit test can check every screen has a page and every page is listed. The pages are Markdown in
// help/, and their pictures in help/shots/ are taken from invented data by `npm run help:shots`.
import type { Route } from '../app/view-model.ts';

export type HelpGroup = 'Start here' | 'Guides' | 'Screens';
export interface HelpPage { id: string; title: string; group: HelpGroup }

export const HELP_PAGES: HelpPage[] = [
  { id: 'start', title: 'Getting started', group: 'Start here' },
  { id: 'owner-guide', title: 'For the owner', group: 'Guides' },
  { id: 'front-desk-guide', title: 'For the front desk', group: 'Guides' },
  { id: 'keys', title: 'Keyboard', group: 'Guides' },
  { id: 'privacy', title: 'What stays on this PC', group: 'Guides' },
  { id: 'line', title: 'The line', group: 'Screens' },
  { id: 'dock', title: 'A chat beside the line', group: 'Screens' },
  { id: 'set-aside', title: 'Set aside', group: 'Screens' },
  { id: 'digest', title: 'Morning digest', group: 'Screens' },
  { id: 'accounts', title: 'Accounts', group: 'Screens' },
  { id: 'account-detail', title: 'One account’s figures', group: 'Screens' },
  { id: 'lost-login', title: 'Reading record', group: 'Screens' },
  { id: 'reader', title: 'Channel readers', group: 'Screens' },
  { id: 'reviews', title: 'Reviews', group: 'Screens' },
  { id: 'reports', title: 'Reports', group: 'Screens' },
  { id: 'assistant', title: 'Assistant', group: 'Screens' },
  { id: 'settings', title: 'Settings', group: 'Screens' },
  { id: 'owner', title: 'Owner console', group: 'Screens' },
];

/** The page the ? and F1 open on each screen. Every route has one; the unit test fails when a new screen has none. */
export const HELP_FOR_ROUTE: Record<Route, string> = {
  line: 'line', dock: 'dock', 'set-aside': 'set-aside', digest: 'digest',
  accounts: 'accounts', 'account-detail': 'account-detail', reader: 'reader', 'lost-login': 'lost-login',
  reviews: 'reviews', reports: 'reports', assistant: 'assistant', settings: 'settings', owner: 'owner',
  help: 'start',
};

export const pageById = (id: string) => HELP_PAGES.find((p) => p.id === id) ?? HELP_PAGES[0];
