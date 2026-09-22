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

/** Who to go to. Taken from the snapshot by main, never from the screen. */
export interface FocusTarget { key: string; name: string; phone: string }

/**
 * One step of going to a conversation, as the page reports it. `done` means the page shows what focusing
 * promises for this channel; anything else means call again shortly (the page may still be loading).
 */
export type FocusStep = 'done' | 'working' | 'not-found' | 'no-target';

/** What the owner allows one read to take in (core/config.ts settings.readLimits). */
export interface ReadLimits { whatsappChats: number }

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
  /**
   * Expression evaluated in the page. Returns a JSON string, or "" when the reader is not installed yet.
   * `limits` carries what the owner set in Settings; a channel whose page decides for itself ignores it.
   */
  scan(limits: ReadLimits): string;
  /** Expression returning a SignInState, so an empty read can say why rather than reading as a quiet day. */
  signedOutProbe: string;
  /** Never throws. A page that changed shape costs this read, not the app. */
  parse(raw: unknown): ReadResult;
  /**
   * Expression taking one step toward a conversation and returning a FocusStep. Called repeatedly until `done`.
   * What `done` means is the channel's decision: WhatsApp opens the chat, Instagram only finds it, because
   * opening an Instagram thread tells the customer it was seen and clears the unread that marks it waiting.
   */
  focus?(target: FocusTarget): string;
}

export const EMPTY: ReadResult = { entries: [], skipped: 0, awaitingInferred: 0 };
