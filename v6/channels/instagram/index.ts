// Instagram. The reader walks the inbox the owner is already signed in to and reports the same shape every
// other channel reports, so nothing downstream knows which channel a chat came from.
//
// It must never open a conversation to read it: opening marks it read and tells the customer you saw it. The
// reader takes what the inbox list shows and stops there — which is why previews are thinner here than on
// WhatsApp, and why that is stated on screen rather than hidden.
import { parseConversations } from '../../core/chat-entry.ts';
import { EMPTY, type ChannelModule } from '../types.ts';

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
  parse(raw) {
    try {
      // No result at all means the adapter is not installed on this page yet.
      if (!raw) return { ...EMPTY, notReady: true, stage: 'reader-absent' };
      return parseConversations(typeof raw === 'string' ? JSON.parse(raw) : raw);
    } catch {
      return { entries: [], skipped: 1, awaitingInferred: 0 };
    }
  },
};
