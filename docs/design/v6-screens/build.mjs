import { writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { artboard } from './lib.mjs';
import * as A from './screens-a.mjs';
import * as B from './screens-b.mjs';
import * as C from './screens-c.mjs';
import * as D from './screens-d.mjs';
import * as E from './screens-e.mjs';
import * as F from './screens-f.mjs';

const out = join(dirname(fileURLToPath(import.meta.url)), '..');
const S = (file, title, render, page, w = 1440, h = 900) => ({ file, title, render, page, w, h });

const PAGES = [['oversight', 'Oversight'], ['start', 'Start and sign-in'], ['settings', 'Settings'], ['owner', 'Owner console'], ['states', 'Dialogs and states'], ['system', 'System']];
const SCREENS = [
  S('Main', 'Command Center', A.Main, 'oversight'), S('AccountLive', 'Account · live page', () => A.AccountLive(false), 'oversight'), S('AccountDetail', 'Account · detail', A.AccountDetail, 'oversight'),
  S('Reviews', 'Reviews', B.Reviews, 'oversight'), S('Analytics', 'Analytics', B.Analytics, 'oversight'), S('Reports', 'Reports', B.Reports, 'oversight'),
  S('Assistant', 'Assistant panel', B.Assistant, 'oversight'), S('Notifications', 'Notifications panel', B.Notifications, 'oversight'), S('CommandPalette', 'Command palette', B.CommandPalette, 'oversight'),
  S('SignIn', 'Sign in', D.SignIn, 'start'), S('SignInBrowser', 'Finish sign-in in browser', D.SignInBrowser, 'start'), S('CreateWorkspace', 'Join or create workspace', D.CreateWorkspace, 'start'),
  S('NewDevice', 'New PC · accounts to sign in', D.NewDevice, 'start'), S('SignInNeeded', 'Account · link with QR', () => A.AccountLive(true), 'start'),
  S('SettingsGeneral', 'Settings · General', C.SettingsGeneral, 'settings'), S('SettingsAccounts', 'Settings · Accounts & channels', C.SettingsAccounts, 'settings'), S('SettingsAssistant', 'Settings · Assistant', C.SettingsAssistant, 'settings'),
  S('SettingsWorkspace', 'Settings · Workspace & members', C.SettingsWorkspace, 'settings'), S('SettingsNotifications', 'Settings · Notifications', C.SettingsNotifications, 'settings'), S('SettingsAppearance', 'Settings · Appearance', C.SettingsAppearance, 'settings'),
  S('SettingsPrivacy', 'Settings · Data & privacy', C.SettingsPrivacy, 'settings'), S('SettingsAbout', 'Settings · About & updates', C.SettingsAbout, 'settings'),
  S('OwnerWorkspaces', 'Owner · workspaces', D.OwnerWorkspaces, 'owner'), S('OwnerWorkspace', 'Owner · one workspace', D.OwnerWorkspace, 'owner'), S('SuspendWorkspace', 'Owner · suspend dialog', E.SuspendWorkspace, 'owner'),
  S('AddAccount', 'Add account', E.AddAccount, 'states'), S('EditAccount', 'Edit account', E.EditAccount, 'states'), S('InviteMember', 'Invite member', E.InviteMember, 'states'),
  S('RemoveMember', 'Remove member', E.RemoveMember, 'states'), S('DeleteAccount', 'Delete account', E.DeleteAccount, 'states'), S('Suspended', 'Membership suspended', F.Suspended, 'states'),
  S('Offline', 'Offline · grace period', F.Offline, 'states'), S('EmptyWorkspace', 'No accounts yet', F.EmptyWorkspace, 'states'), S('AllCaughtUp', 'All caught up', F.AllCaughtUp, 'states'),
  S('ModuleUnhealthy', 'Channel reader broken', F.ModuleUnhealthy, 'states'), S('UpdateReady', 'Update ready', F.UpdateReady, 'states'),
  S('TitleBar', 'Title bar states', F.TitleBar, 'system', 1440, 700), S('TrayAndToast', 'Tray, toast, taskbar', F.TrayAndToast, 'system', 1440, 560), S('Tokens', 'Tokens and components', F.Tokens, 'system'),
];

const artboards = [];
for (const [pageId] of PAGES) {
  const list = SCREENS.filter(s => s.page === pageId);
  let y = 0;
  for (let i = 0; i < list.length; i += 3) {
    const row = list.slice(i, i + 3);
    row.forEach((s, k) => artboards.push({ file: `${s.file}.dc.html`, title: s.title, x: k * 1560, y, w: s.w, h: s.h, page: pageId }));
    y += Math.max(...row.map(s => s.h)) + 180;
  }
}
for (const s of SCREENS) writeFileSync(join(out, `${s.file}.dc.html`), artboard(s.render()));

const note = (id, page, text) => ({ id, page, x: 0, y: -260, w: 520, text });
const canvas = {
  pages: PAGES.map(([id, name]) => ({ id, name })),
  artboards,
  annotations: [
    note('oversight-note', 'oversight', 'Unified Messenger v6 · Oversight screens\nAll names, businesses and figures are sample data.\nTitle bar, sidebar and tokens are shared by every screen.'),
    note('start-note', 'start', 'First launch and a new PC. Google sign-in opens the normal browser (Google requires it). Logins never move between PCs.'),
    note('owner-note', 'owner', 'Visible only to the product owner\'s Google account. Enforced by Firebase security rules on the free plan; no server.'),
  ],
  launch: { view: 'canvas', page: 'oversight' },
};
writeFileSync(join(out, 'canvas.json'), JSON.stringify(canvas, null, 2));
console.log(`wrote ${SCREENS.length} artboards on ${PAGES.length} pages`);
console.log(SCREENS.map(s => `--artboard ${s.file}.dc.html`).join(' '));
