// What every channel module must provide, and nothing more. One folder per channel, same four parts, so a
// channel that breaks costs its own figures and not the app's.
//
// A module is deliberately data, not behaviour: strings of page script plus a pure parser. It touches no
// files, no network and no Electron, which is what makes it testable against made-up page output.
import type { ChatEntry } from '../core/chat-entry.ts';

export interface ReadResult {
  entries: ChatEntry[];
  /** Rows that could not be read. Counted, never silently dropped. */
  skipped: number;
  /** Rows with no explicit awaiting flag, which fell back to the unread count. */
  awaitingInferred: number;
  /**
   * The page has not finished bringing its reader up. Only the module can tell this apart from a genuinely
   * empty inbox, and the difference matters: a page still loading must not be reported as a broken reader.
   */
  notReady?: boolean;
  /** What the page said about its own state, for the log. A stage name, never anything a customer wrote. */
  stage?: string;
}

/** What the page said about being signed in. Asked only when a read comes back empty. */
export interface SignInState {
  qr?: boolean;
  login?: boolean;
  unsupported?: boolean;
}

export interface ChannelModule {
  /** Matches the ChannelId in core/config.ts. */
  id: string;
  /** Human name for the health line. */
  name: string;
  /**
   * Page script that installs the reader. `load` hands over a file's contents, so the module names what it
   * needs without reaching for the disk itself.
   */
  inject(load: (file: string) => string): string;
  /** Expression evaluated in the page. Returns a JSON string, or "" when the reader is not installed yet. */
  scan: string;
  /** Expression returning a SignInState, so an empty read can say why rather than reading as a quiet day. */
  signedOutProbe: string;
  /** Never throws. A page that changed shape costs this read, not the app. */
  parse(raw: unknown): ReadResult;
}

export const EMPTY: ReadResult = { entries: [], skipped: 0, awaitingInferred: 0 };
