// Builds the Windows installer: screens, then the packaged app, then Inno Setup.
//
//   npm run dist            -> dist\UnifiedMessenger6Setup.exe
//   npm run install-local   -> the same, then installs it on this PC and opens the app
//
// Run install-local from your own terminal. An agent's shell sits in a sandbox that redirects installs to a
// private copy, so an install started from there lands where the Start Menu shortcut never looks.
import { packager } from '@electron/packager';
import { flipFuses, FuseV1Options, FuseVersion } from '@electron/fuses';
import { execSync, spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { writeCloudConfig } from './cloud-config.mjs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const ISCC = 'C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe';
const { version } = JSON.parse(readFileSync(join(ROOT, 'package.json'), 'utf8'));
const step = (name) => console.log(`\n== ${name}`);

step('screens');
execSync('npx vite build', { cwd: ROOT, stdio: 'inherit' });

step('cloud config');
writeCloudConfig();

step('app');
await packager({
  dir: ROOT,
  out: join(ROOT, 'out'),
  overwrite: true,
  platform: 'win32',
  arch: 'x64',
  name: 'Unified Messenger',
  executableName: 'UnifiedMessenger6',
  appVersion: version,
  icon: join(ROOT, 'assets', 'icon.ico'),
  win32metadata: { ProductName: 'Unified Messenger', FileDescription: 'Unified Messenger', CompanyName: 'Unified Messenger' },
  asar: true,
  prune: false,
  // Only what runs: app/, core/, channels/, assets/, the built screens and package.json. Nothing in
  // node_modules is needed at run time, because the screens are bundled and main uses only Node and Electron.
  ignore: [
    // cloud/ holds the Firestore rules and their tests, site/ the two public pages: both go to Firebase, not the app.
    /^\/(ui|out|dist|scripts|tests|test-results|cloud|site|node_modules|\.vite)(\/|$)/,
    /^\/(tsconfig\.json|vite\.config\.ts|playwright\.config\.ts|installer\.iss|README\.md|package-lock\.json|firebase\.json)$/,
    /^\/[^/]*\.log$/,
    /\.test\.ts$/,
  ],
});

step('fuses');
// Electron leaves cookie encryption off, so every login cookie sits in plaintext on disk (lesson
// v6-electron-cookies-are-plaintext). This fuse turns Chromium's own encryption on for the packaged app; cookies
// already written in plaintext keep working, because Chromium reads the plain `value` when there is no encrypted one.
await flipFuses(join(ROOT, 'out', 'Unified Messenger-win32-x64', 'UnifiedMessenger6.exe'), {
  version: FuseVersion.V1,
  resetAdHocDarwinSignature: false,
  [FuseV1Options.EnableCookieEncryption]: true,
  // The app is packed into app.asar (below), so Electron should refuse a loose app folder dropped beside it.
  [FuseV1Options.OnlyLoadAppFromAsar]: true,
});
console.log('EnableCookieEncryption and OnlyLoadAppFromAsar on');

step('installer');
if (!existsSync(ISCC)) throw new Error(`Inno Setup not found at ${ISCC}`);
const iscc = spawnSync(ISCC, ['/Q', `/DAppVersion=${version}`, join(ROOT, 'installer.iss')], { cwd: ROOT, stdio: 'inherit' });
if (iscc.status !== 0) process.exit(iscc.status ?? 1);
const setup = join(ROOT, 'dist', 'UnifiedMessenger6Setup.exe');
console.log(`\nBuilt ${setup}`);

if (process.argv.includes('--install')) {
  step('install');
  const run = spawnSync(setup, ['/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'], { stdio: 'inherit' });
  if (run.status !== 0) process.exit(run.status ?? 1);
  console.log('Installed. The app opens by itself; next time, use the Unified Messenger shortcut.');
}
