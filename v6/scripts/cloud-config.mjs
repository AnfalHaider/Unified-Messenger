// Writes cloud-config.json beside the app: the Firebase project's public identifiers and the desktop sign-in client,
// taken from the project's own files as Firebase and Google hand them out (firebase-config.json, oauth-client.json).
// Those files, and this output, never enter the repository (.gitignore). A build without them still runs, and says
// sign-in is not available.
//
//   node scripts/cloud-config.mjs              -> reads from D:/Projects/um-v6-proof, or UM_CLOUD_SOURCE
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { cloudConfigFrom } from '../core/cloud-auth.ts';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const SOURCE = process.env.UM_CLOUD_SOURCE || 'D:/Projects/um-v6-proof';

export function writeCloudConfig() {
  const fb = join(SOURCE, 'firebase-config.json'), oauth = join(SOURCE, 'oauth-client.json');
  if (!existsSync(fb) || !existsSync(oauth)) {
    console.warn(`No cloud config in ${SOURCE}: this build will say sign-in is not available.`);
    return false;
  }
  const config = cloudConfigFrom(JSON.parse(readFileSync(fb, 'utf8')), JSON.parse(readFileSync(oauth, 'utf8')));
  writeFileSync(join(ROOT, 'cloud-config.json'), JSON.stringify(config, null, 2));
  console.log(`cloud-config.json written for project ${config.firebase.projectId}`);
  return true;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) writeCloudConfig();
