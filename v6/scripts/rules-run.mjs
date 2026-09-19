// Runs the Firestore rules tests (cloud/rules.spec.ts) against the emulator: `npm run rules:test`.
// Named -run, not -test, so `node --test` never picks it up (lesson v6-node-test-runs-dash-test-files).
//
// The emulator needs Java 21 or newer. CI installs it; on a PC whose `java` is older, this looks for a JDK 21+ under
// JAVA_HOME or in %USERPROFILE%\.jdks (a portable Temurin unpacked there, which changes no system setting).
import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync, readdirSync } from 'node:fs';
import { homedir } from 'node:os';
import { delimiter, join } from 'node:path';

const javaIn = (home) => join(home, 'bin', process.platform === 'win32' ? 'java.exe' : 'java');
const versionOf = (java) => { const r = spawnSync(java, ['-version'], { encoding: 'utf8' }); return Number(/version "(\d+)/.exec(`${r.stderr}${r.stdout}`)?.[1] ?? 0); };

const env = { ...process.env };
if (versionOf('java') < 21) {
  const jdks = join(homedir(), '.jdks');
  const homes = [env.JAVA_HOME, ...(existsSync(jdks) ? readdirSync(jdks).map((d) => join(jdks, d)) : [])].filter(Boolean);
  const home = homes.find((h) => existsSync(javaIn(h)) && versionOf(javaIn(h)) >= 21);
  if (!home) {
    console.error('The Firestore emulator needs Java 21 or newer. Install a JDK 21 (for example Temurin) and set JAVA_HOME, or unpack one into %USERPROFILE%\.jdks.');
    process.exit(1);
  }
  env.JAVA_HOME = home;
  env.PATH = `${join(home, 'bin')}${delimiter}${env.PATH}`;
  console.log(`Using Java from ${home}`);
}
const run = spawnSync('npx firebase emulators:exec --only firestore --project demo-unified-messenger "node --test cloud/rules.spec.ts"', { stdio: 'inherit', env, shell: true });
process.exit(run.status ?? 1);
