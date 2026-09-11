// Port of Services/Oversight/ChatEntryParser.cs: the one place a channel reader's scan JSON becomes chat entries.
// The JSON comes from a web page that changes without notice, so: a bad row never costs a good row, and a
// field of the wrong type is an empty value or a skipped row, never an exception.

export interface ChatEntry {
  conversationKey: string;
  customerName: string;
  unread: number;
  lastActivity: Date;
  preview: string;
  awaiting: boolean;
  lastMessageFromMe: boolean;
  contactPhone: string;
  hasLastMessage: boolean | null; // null = unknown (older snapshot), NOT "no message"
  lastMessageType: string;
  lastCallOutcome: string; // empty = unknown, which stays counted
}

export interface ParseResult {
  entries: ChatEntry[];
  skipped: number;
  // Rows with no explicit boolean `awaiting`, which fell back to unread > 0: per-device and less accurate.
  // Non-zero means the reader's output shape changed; the caller must log it.
  awaitingInferred: number;
}

type Row = Record<string, unknown>;
const isObject = (v: unknown): v is Row => typeof v === 'object' && v !== null && !Array.isArray(v);
const str = (row: Row, key: string) => (typeof row[key] === 'string' ? (row[key] as string) : '');
const int = (row: Row, key: string) => {
  const v = row[key];
  return typeof v === 'number' && Number.isInteger(v) && v >= -2147483648 && v <= 2147483647 ? v : 0;
};

export function parseConversations(root: unknown): ParseResult {
  const result: ParseResult = { entries: [], skipped: 0, awaitingInferred: 0 };
  const rows = isObject(root) ? root.conversations : undefined;
  if (!Array.isArray(rows)) return result;

  for (const row of rows) {
    try {
      const entry = parseConversation(row);
      if (!entry) { result.skipped++; continue; }
      // Filtered here, where every producer funnels through: v5's store bridge once let WhatsApp's own
      // one-way notice account into the waiting count, where no reply could ever clear it.
      if (isNonCustomerConversation(entry.conversationKey)) continue;
      if (typeof (row as Row).awaiting !== 'boolean') result.awaitingInferred++;
      result.entries.push(entry);
    } catch {
      result.skipped++;
    }
  }
  return result;
}

/** Null when the row has no parseable timestamp: better dropped than stamped "now" into today's counts. */
export function parseConversation(row: unknown): ChatEntry | null {
  if (!isObject(row)) return null;
  const when = Date.parse(str(row, 'lastActivityTimestampUtc'));
  if (Number.isNaN(when)) return null;

  const unread = int(row, 'unreadCount');
  return {
    conversationKey: str(row, 'conversationKey'),
    customerName: str(row, 'customerName'),
    unread,
    lastActivity: new Date(when),
    preview: sanitizePreview(str(row, 'lastMessagePreview')),
    awaiting: row.awaiting !== undefined && row.awaiting !== null ? row.awaiting === true : unread > 0,
    lastMessageFromMe: row.lastMessageFromMe === true,
    contactPhone: str(row, 'contactPhone'),
    hasLastMessage: typeof row.hasLastMessage === 'boolean' ? row.hasLastMessage : null,
    lastMessageType: str(row, 'lastMessageType'),
    lastCallOutcome: str(row, 'lastCallOutcome'),
  };
}

/** Groups, broadcasts and Status, channels, and WhatsApp's own `0@` account are never customer chats. */
export function isNonCustomerConversation(key?: string | null): boolean {
  const k = (key ?? '').trim().toLowerCase();
  if (!k) return false;
  return k.includes('@g.us') || k.includes('@broadcast') || k.includes('@newsletter') || k.startsWith('status@') || k.startsWith('0@');
}

/** Raw encoded media becomes "Photo"; a bare JID from a shared contact card becomes "Shared a contact". */
export function sanitizePreview(preview: string): string {
  if (!preview.trim()) return '';
  const t = preview.trimStart();
  if (t.startsWith('/9j/') || t.startsWith('iVBORw0') || t.startsWith('R0lGOD') || t.toLowerCase().startsWith('data:image')) return 'Photo';
  return isBareJid(t) ? 'Shared a contact' : preview;
}

/** The whole text is ASCII digits, "@", then a known JID suffix. Deliberately narrow. */
export const isBareJid = (text: string) => /^[0-9]+@(lid|c\.us|s\.whatsapp\.net|g\.us)$/i.test(text.trimEnd());
