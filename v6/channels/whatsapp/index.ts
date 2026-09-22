// WhatsApp. The reader is v5's store bridge, which reads the decrypted in-memory models rather than the
// IndexedDB store: message bodies are encrypted at rest, so the store is the only place a preview exists.
//
// Two things here are hard-won and must not be "tidied":
//   - the truncate helper, because a slice through an emoji leaves a lone surrogate and JSON then throws on
//     that one property, which silently dropped a whole conversation from every scan;
//   - the user agent fix, which lives in the shell: WhatsApp refuses a browser whose agent carries the
//     Electron token and shows "update your browser" instead of the app.
import { parseConversations } from '../../core/chat-entry.ts';
import { EMPTY, type ChannelModule } from '../types.ts';

/** Same surrogate-safe cut as v5's adapter-core, which the store bridge calls into. */
const TRUNCATE =
  'window.__umTruncate = window.__umTruncate || function (v, max) { var t = String(v || ""); if (!(max > 0) || t.length <= max) return t; var e = max, c = t.charCodeAt(e - 1); if (c >= 0xd800 && c <= 0xdbff) e -= 1; return t.slice(0, e); };';

export const whatsapp: ChannelModule = {
  id: 'whatsapp',
  name: 'WhatsApp',
  inject: (load) => TRUNCATE + load('whatsapp/whatsapp-store-bridge.js') + load('whatsapp/whatsapp-idb.js') + load('whatsapp/whatsapp-focus.js') + load('whatsapp/whatsapp-messages.js'),
  // Opens the chat. On WhatsApp that is safe for the figures: waiting is judged by who wrote last, not by unread.
  focus: (t) => `window.__umFocusWhatsApp ? window.__umFocusWhatsApp(${JSON.stringify(t.key)}, ${JSON.stringify(t.name)}, ${JSON.stringify(t.phone)}) : 'working'`,
  // The store bridge, and WhatsApp's saved chat list when the bridge has found nothing for three minutes on a
  // signed-in page (whatsapp-idb.js). executeJavaScript waits for the promise it returns.
  scan: (limits) => {
    const n = limits.whatsappChats;
    return `window.__umWhatsAppScan ? window.__umWhatsAppScan(${n}) : (window.__umStartStoreScan ? (window.__umStartStoreScan(${n}), window.__umGetStoreScanResult()) : "")`;
  },
  // The sign-in screen, by the selectors v5 measured against the live page. A bare `canvas` is not enough:
  // WhatsApp's loading screen draws one too, which reads as "still starting" when the owner is signed out.
  signedOutProbe: `({
    qr: !!document.querySelector('[data-testid="qrcode"], canvas[aria-label*="QR" i], [data-ref] div[data-testid="intro-text"], [data-testid="link-device-phone-number-code-screen-instructions"]'),
    login: false,
    unsupported: /works with google chrome|browser (isn.t|is not) supported|update (your )?(browser|chrome)/i.test(document.body ? document.body.innerText : '')
  })`,
  parse(raw) {
    try {
      // No result at all means the bridge is not installed on this page yet.
      if (!raw) return { ...EMPTY, notReady: true, stage: 'bridge-absent' };
      const root = typeof raw === 'string' ? JSON.parse(raw) : raw;
      const result = parseConversations(root);
      // The bridge names why it found nothing. WhatsApp Web builds its stores a few seconds after the page
      // loads, so "no store" and "no models" mean too early, not broken.
      const diag = (root as { diag?: { stage?: string; source?: string } })?.diag;
      // A read from the saved list says so, so the log and the reading record show the fallback is in use.
      const stage = diag?.source === 'indexeddb' && diag.stage === 'done' ? 'saved-list' : diag?.stage;
      if (!result.entries.length && (stage === 'no-store' || stage === 'no-models' || stage === 'start')) {
        return { ...result, notReady: true, stage };
      }
      return { ...result, stage };
    } catch {
      // The page returned something that is not the scan's shape. That is one bad read, not a broken app.
      return { entries: [], skipped: 1, awaitingInferred: 0 };
    }
  },
};
