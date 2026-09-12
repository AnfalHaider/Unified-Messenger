// Durable files. Small on purpose: the app layer's only job here is not to lose anything.
import { copyFileSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

/**
 * A file that cannot be read is preserved before anything else touches it. v5 destroyed a customer's whole
 * reply-time history exactly once this way: a locked file failed to load, the store reset to empty, and the
 * next flush wrote that empty state over the real bytes.
 */
export function loadJson<T>(file: string, fallback: T, onProblem?: (why: string) => void): T {
  let text: string;
  try {
    text = readFileSync(file, 'utf8');
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code !== 'ENOENT') onProblem?.(`unreadable: ${(e as Error).message}`);
    return fallback;
  }
  try {
    return JSON.parse(text) as T;
  } catch (e) {
    const kept = `${file}.broken-${Date.now()}`;
    try { copyFileSync(file, kept); } catch { /* the copy is best effort; the message below still names it */ }
    onProblem?.(`unparseable: ${(e as Error).message} — kept ${kept}`);
    return fallback;
  }
}

/** Write through a temp file, so a crash mid-write cannot leave a truncated one behind. */
export function saveJson(file: string, value: unknown) {
  mkdirSync(dirname(file), { recursive: true });
  const tmp = `${file}.tmp`;
  writeFileSync(tmp, JSON.stringify(value));
  renameSync(tmp, file);
}
