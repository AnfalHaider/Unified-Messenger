// Instagram. The reader is v5's: it reads the DM mailbox Instagram already prefetched into its Relay store on
// the home feed, to draw its own Messages badge. No navigation, no query of our own, and it never opens a
// conversation — opening one marks it read and tells the customer you saw it.
//
// Its output is its own shape, not the WhatsApp scan's: { conversations: [{ key, name, username, unread,
// awaiting, lastActivityMs }], unreadBadge, unreadBadgeCapped, diag: { stage } }. Ported from v5's
// InstagramSnapshotReader, including its guard against reads taken before Instagram has synced read state.
import type { ChatEntry } from '../../core/chat-entry.ts';
import { EMPTY, type ChannelModule, type ReadResult } from '../types.ts';

type Row = { key?: unknown; name?: unknown; username?: unknown; unread?: unknown; awaiting?: unknown; lastActivityMs?: unknown };
const text = (v: unknown) => (typeof v === 'string' ? v : '');
const count = (v: unknown) => (typeof v === 'number' && Number.isFinite(v) ? Math.max(0, Math.trunc(v)) : 0);

/**
 * Seconds after an account loads, Instagram reports every thread unread while its own badge says otherwise,
 * and nothing in the record says it is unsure. A read claiming more unread threads than the client's badge is
 * that window; it is dropped until the next pass. "More than", not "different": the badge counts every unread
 * thread and the reader sees only the top of Primary. A capped badge ("9+") cannot be compared.
 */
export const looksUnsynced = (awaiting: number, badge: number, capped: boolean) => !capped && awaiting > badge;

export const instagram: ChannelModule = {
  id: 'instagram',
  name: 'Instagram',
  inject: (load) => load('instagram-adapter.js'),
  scan: 'window.__umReadInstagramThreads ? window.__umReadInstagramThreads() : ""',
  signedOutProbe: `({
    qr: false,
    login: !!document.querySelector('input[name="username"], input[name="password"], input[type="password"], form#loginForm, a[href^="/accounts/signup"]'),
    unsupported: false
  })`,
  parse(raw): ReadResult {
    try {
      // No result at all means the adapter is not installed on this page yet.
      if (!raw) return { ...EMPTY, notReady: true, stage: 'reader-absent' };
      const root = (typeof raw === 'string' ? JSON.parse(raw) : raw) as {
        conversations?: unknown; unreadBadge?: unknown; unreadBadgeCapped?: unknown; diag?: { stage?: unknown };
      };
      const stage = text(root?.diag?.stage);
      // The reader names where it stopped. Anything but done or empty is a read that did not complete.
      if (stage !== 'done' && stage !== 'empty') return { entries: [], skipped: 1, awaitingInferred: 0, stage: stage || 'unknown' };

      const rows = Array.isArray(root.conversations) ? (root.conversations as Row[]) : [];
      const entries: ChatEntry[] = [];
      let skipped = 0;
      for (const row of rows) {
        const key = text(row?.key);
        if (!key) { skipped++; continue; }
        const name = text(row.name), username = text(row.username);
        const at = count(row.lastActivityMs);
        entries.push({
          conversationKey: key,
          // A brand-new request can have no title; an empty name is a row nobody can act on.
          customerName: name || (username ? `@${username}` : key),
          unread: count(row.unread),
          lastActivity: at > 0 ? at : Date.now(),
          // Empty on purpose: the feed's prefetch carries thread metadata only, never message text.
          preview: '',
          awaiting: row.awaiting === true,
          lastMessageFromMe: false,
          contactPhone: '',
          hasLastMessage: null,
          lastMessageType: '',
          lastCallOutcome: '',
        });
      }

      const awaiting = entries.filter((e) => e.awaiting).length;
      if (looksUnsynced(awaiting, count(root.unreadBadge), root.unreadBadgeCapped === true)) {
        return { entries: [], skipped: 0, awaitingInferred: 0, notReady: true, stage: 'unsynced' };
      }
      return { entries, skipped, awaitingInferred: 0, stage };
    } catch {
      return { entries: [], skipped: 1, awaitingInferred: 0 };
    }
  },
};
