// Refreshes the pictures in the help pages (help/shots/*.jpg) from invented data. Run after a screen changes:
//   npm run help:shots
import { execSync } from 'node:child_process';

execSync('npx vite build', { stdio: 'inherit' });
execSync('npx playwright test -g "help screenshots"', { stdio: 'inherit', env: { ...process.env, UM_HELP_SHOTS: '1' } });
