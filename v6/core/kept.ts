// What this PC is holding, for Settings › Privacy. The screen used to show invented sizes; these are measured.
//
// The point of the screen is to answer "what is on my machine, and how do I get rid of it" honestly. So a size
// that could not be measured says so rather than showing 0: nothing found and nothing measured are different
// answers, and only one of them is reassuring.

export interface KeptSizes {
  /** The signed-in sessions, one folder per account: cookies, local storage, WhatsApp's own database. */
  logins: number | null;
  /** Who was waiting, reply times, day records, missed calls and the reading record. */
  recorded: number | null;
  /** Notes, tags and saved replies. */
  customers: number | null;
  /** The reviews last read from each Google profile. */
  reviews: number | null;
  /** app.log. */
  log: number | null;
  /** The assistant's model, as Ollama holds it. Null when the assistant is off or the model is not downloaded. */
  model: number | null;
}

export interface KeptRow {
  what: string;
  kept: string;
  /** Null when it could not be measured; the screen says so rather than showing nothing. */
  bytes: number | null;
  /** Where it is managed, in the owner's words. Empty when there is nothing to do. */
  where: string;
}

/** 486 MB, 3.3 GB, 0.2 MB. Bytes are never shown: nobody reads a file size in bytes. */
export function sizeText(bytes: number | null): string {
  if (bytes === null) return 'not measured';
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB', 'TB'];
  let n = bytes / 1024;
  let i = 0;
  while (n >= 1024 && i < units.length - 1) { n /= 1024; i++; }
  return `${n < 10 ? n.toFixed(1) : Math.round(n)} ${units[i]}`;
}

/** The rows the screen draws, in the order a person would ask about them: the biggest and most personal first. */
export function keptRows(sizes: KeptSizes): KeptRow[] {
  return [
    { what: 'Account logins', kept: 'Until you sign out or remove the account', bytes: sizes.logins,
      where: 'Accounts › Remove, which wipes the login and everything recorded for it' },
    { what: 'Who was waiting, reply times, missed calls', kept: 'Day records and reply times are kept as they are recorded', bytes: sizes.recorded,
      where: 'Removing an account deletes its own history' },
    { what: 'Notes, tags and saved replies', kept: 'Until deleted', bytes: sizes.customers,
      where: 'The customer panel, beside the chat' },
    { what: 'Google reviews last read', kept: 'Replaced at every read', bytes: sizes.reviews,
      where: 'Removing the Google account deletes them' },
    { what: 'The support log', kept: 'Counts and timings only, never a name or a message', bytes: sizes.log,
      where: 'Channel readers › Save a report for support' },
    { what: 'The assistant’s model', kept: 'Held by Ollama, outside the app', bytes: sizes.model,
      where: sizes.model === null ? 'Nothing downloaded' : 'Settings › Assistant, or Ollama itself' },
  ];
}

/** Everything the app is holding, for the one line that answers "how much is all this?". */
export const keptTotal = (sizes: KeptSizes): number | null => {
  const known = Object.values(sizes).filter((v): v is number => typeof v === 'number');
  return known.length ? known.reduce((a, b) => a + b, 0) : null;
};
