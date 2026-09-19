// The WhatsApp fallback reader: WhatsApp Web's own saved chat list, in its 'model-storage' IndexedDB. Ported from
// v5's whatsapp-adapter.js (umDbConversationsPromise), whose rules were measured against the live page.
//
// The store bridge is the reader; this is what keeps figures coming when a WhatsApp update breaks it. It is weaker,
// and says so: message bodies are encrypted at rest, so previews are mostly missing, and the saved list lags a
// reply sent from the phone until WhatsApp syncs it. It reads one bounded getAll of 'chat' and one of 'contact',
// never a cursor over 'message' (a long cursor aborts the read transaction mid-scan).
//
// window.__umWhatsAppScan(max) -> JSON in the store bridge's own shape, with diag.source 'indexeddb' when it answered.
(function () {
  if (window.__umWhatsAppScanReady) return;
  window.__umWhatsAppScanReady = true;

  /** How long a signed-in page may report "no store" before the bridge is taken to be broken. WhatsApp builds its
   *  stores within seconds of loading; three minutes without them is not a slow start. */
  var BRIDGE_GRACE_MS = 3 * 60 * 1000;

  var digitsOf = function (value) {
    var s = typeof value === 'string' ? value : (value && (value._serialized || value.user)) || '';
    var at = s.indexOf('@');
    if (at > 0) s = s.slice(0, at);
    return /^\d{7,15}$/.test(s) ? s : '';
  };
  var keyOf = function (chat) {
    if (!chat || !chat.id) return null;
    if (typeof chat.id === 'string') return chat.id;
    return chat.id._serialized || null;
  };
  var request = function (req) {
    return new Promise(function (resolve, reject) { req.onsuccess = function () { resolve(req.result); }; req.onerror = function () { reject(req.error); }; });
  };

  // Signed in, by WhatsApp's own marker, and no QR code on screen. The saved list survives a sign-out, so without
  // this a signed-out page would go on reporting the last customers it saw as if they were still waiting.
  function signedIn() {
    try {
      if (document.querySelector('[data-testid="qrcode"], canvas[aria-label*="QR" i]')) return false;
      return !!window.localStorage.getItem('last-wid-md');
    } catch (e) { return false; }
  }

  async function readSaved(max) {
    var diag = { stage: 'start', source: 'indexeddb', chats: 0, withLastMessage: 0 };
    var fail = function (stage) { diag.stage = stage; return { ok: false, conversations: [], diag: diag }; };
    if (!window.indexedDB || !indexedDB.databases) return fail('no-indexeddb');
    if (!signedIn()) return fail('signed-out');
    var names = (await indexedDB.databases()).map(function (d) { return d.name; });
    if (names.indexOf('model-storage') < 0) return fail('no-model-storage');
    var db = await request(indexedDB.open('model-storage'));
    try {
      if (!db.objectStoreNames.contains('chat')) return fail('no-chat-store');

      // @lid privacy ids → phone digits and saved names, from the contact store.
      var phoneOf = Object.create(null), nameOf = Object.create(null);
      if (db.objectStoreNames.contains('contact')) {
        var contacts = await request(db.transaction('contact', 'readonly').objectStore('contact').getAll(null, 5000));
        (contacts || []).forEach(function (c) {
          try {
            var key = c && (typeof c.id === 'string' ? c.id : (c.id && c.id._serialized) || '');
            if (!key) return;
            var d = c.phoneNumber ? digitsOf(c.phoneNumber) : '';
            if (d) phoneOf[key] = d;
            var nm = String(c.name || c.verifiedName || c.pushname || c.shortName || '').trim();
            if (nm && /[\p{L}]/u.test(nm)) nameOf[key] = nm;
          } catch (e) { /* one bad contact costs only itself */ }
        });
      }

      var chats = await request(db.transaction('chat', 'readonly').objectStore('chat').getAll(null, 1000));
      diag.chats = (chats || []).length;
      var own = '';
      try { var wid = (window.localStorage.getItem('last-wid-md') || '').replace(/"/g, '').match(/(\d{6,})/); own = wid ? wid[1] : ''; } catch (e) { own = ''; }

      var out = [];
      (chats || []).forEach(function (ch) {
        try {
          if (!ch || ch.archive) return;
          var jid = keyOf(ch);
          if (!jid) return;
          var lower = jid.toLowerCase();
          // Customers only: not groups, broadcasts, status, channels, or WhatsApp's own account (0@c.us).
          if (lower.indexOf('@g.us') >= 0 || lower.indexOf('@broadcast') >= 0 || lower.indexOf('@newsletter') >= 0 ||
            lower.indexOf('status@') >= 0 || lower.indexOf('0@') === 0) return;
          // Not the "message yourself" chat, which is keyed by this account's own number.
          var head = lower.split('@')[0].split(':')[0];
          if (own && head.replace(/\D/g, '') === own) return;

          var last = ch.lastMessage || null;
          var t = (last && last.t) || ch.t || 0;
          if (!t) return;
          var name = ch.name || ch.formattedTitle || (ch.contact && ch.contact.name) || '';
          var phone = phoneOf[jid] || digitsOf(jid);
          if (!name || name === 'New message') name = nameOf[jid] || '';
          // A privacy id with no number and no name cannot be recognised or opened: not an actionable customer.
          if (lower.indexOf('@lid') >= 0 && !phoneOf[jid] && (!name || name.replace(/\D/g, '') === digitsOf(jid))) return;

          // Who wrote last decides waiting. A message deleted for everyone is not a question still pending.
          var fromMe = last ? !!last.fromMe : (ch.unreadCount || 0) === 0;
          var revoked = !!(last && (last.type === 'revoked' || last.subtype === 'revoke'));
          var body = last ? (last.body || last.caption || last.text || '') : '';
          var iso = new Date(t * 1000).toISOString();
          if (last) diag.withLastMessage++;
          out.push({
            conversationKey: jid,
            customerName: name || (phone ? '+' + phone : jid),
            contactPhone: phone,
            lastInboundBody: fromMe ? '' : window.__umTruncate(body, 280),
            lastInboundTimestampUtc: iso,
            lastActivityTimestampUtc: iso,
            lastMessageFromMe: fromMe,
            awaiting: !fromMe && !revoked,
            lastMessagePreview: window.__umTruncate(body, 280),
            unreadCount: ch.unreadCount || 0,
            inboundCount: ch.unreadCount || 0,
            hasLastMessage: !!last,
            lastMessageType: String((last && (last.type || last.mediaType)) || ''),
          });
        } catch (e) { /* one malformed chat costs only itself */ }
      });

      // A cold read says almost every chat has no message; that is "unknown", not "every message was deleted".
      if (!(out.length > 0 && diag.withLastMessage * 2 > out.length)) out.forEach(function (c) { c.hasLastMessage = null; });
      out.sort(function (a, b) { return a.lastActivityTimestampUtc < b.lastActivityTimestampUtc ? 1 : -1; });
      diag.stage = 'done';
      return { ok: out.length > 0, conversations: out.slice(0, max), diag: diag };
    } finally {
      try { db.close(); } catch (e) { /* already closed */ }
    }
  }

  window.__umWhatsAppScan = async function (max) {
    var bridge = null;
    try {
      if (window.__umStartStoreScan) { window.__umStartStoreScan(max); bridge = JSON.parse(window.__umGetStoreScanResult() || 'null'); }
    } catch (e) { bridge = null; }
    if (bridge && bridge.conversations && bridge.conversations.length) return JSON.stringify(bridge);
    // Nothing from the bridge. Early on that is WhatsApp still building its stores; once the page has been up past
    // the grace period, the bridge has stopped working and WhatsApp's saved list is read instead.
    // (A test page sets __umSavedListNow to skip the wait.)
    if (performance.now() < BRIDGE_GRACE_MS && !window.__umSavedListNow) return bridge ? JSON.stringify(bridge) : '';
    try {
      var saved = await Promise.race([readSaved(max), new Promise(function (r) { setTimeout(function () { r(null); }, 20000); })]);
      if (saved && saved.conversations.length) return JSON.stringify(saved);
      if (!bridge && saved) return JSON.stringify(saved);
    } catch (e) { /* the saved list could not be read either; report what the bridge said */ }
    return bridge ? JSON.stringify(bridge) : '';
  };
})();
