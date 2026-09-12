// Builds the Windows installer: screens, then the packaged app, then Inno Setup.
//
//   npm run dist            -> dist\UnifiedMessenger6Setup.exe
//   npm run install-local   -> the same, then installs it on this PC and opens the app
//
// Run install-local from your own terminal. An agent's shell sits in a sandbox that redirects installs to a
// private copy, so an install started from there lands where the Start Menu shortcut never looks.
import { packager } from '@electron/packager';
import { execSync, spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const ISCC = 'C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe';
const { version } = JSON.parse(readFileSync(join(ROOT, 'package.json'), 'utf8'));
const step = (name) => console.log(`\n== ${name}`);

step('screens');
execSync('npx vite build', { cwd: ROOT, stdio: 'inherit' });

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
  // ponytail: no asar, so Electron runs the TypeScript exactly as it does from source. Pack it once a build
  // step exists for other reasons.
  asar: false,
  prune: false,
  // Only what runs: app/, core/, channels/, assets/, the built screens and package.json. Nothing in
  // node_modules is needed at run time, because the screens are bundled and main uses only Node and Electron.
  ignore: [
    /^\/(ui|out|dist|scripts|node_modules|\.vite)(\/|$)/,
    /^\/(tsconfig\.json|vite\.config\.ts|installer\.iss|README\.md|package-lock\.json)$/,
    /\.test\.ts$/,
  ],
});

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
