// whatsapp-store-bridge.js — READ-ONLY access to WhatsApp Web's in-memory model collections.
//
// Why this exists
// ---------------
// The IndexedDB path (`__umStartDbConversationScan` in whatsapp-adapter.js) reads WhatsApp's PERSISTED
// chat store. That store has two hard limits we documented the expensive way:
//   1. Message bodies are ENCRYPTED at rest (msgRowOpaqueData), so it carries no readable preview. The
//      only plaintext fallback was harvesting the ~60 rendered sidebar rows.
//   2. It LAGS in a throttled background webview — a reply sent from the phone can take a while to land.
// WhatsApp Web's own runtime keeps the same data in memory, already decrypted, always current. That is
// what this bridge reads. Technique adopted from wppconnect/wa-js and whatsapp-web.js (both Apache-2.0):
// reach the app's webpack module registry from inside the page and pull out the model collections.
//
// Rules this file obeys
// ---------------------
// * READ-ONLY. It never resolves, imports, or calls a send/mutate/mark-read surface. Collections are
//   read; models are never written to. This is an oversight tool — it must never touch the account.
// * FAIL SOFT. Every discovery step is probed and verified. If WhatsApp Web changes shape, this file
//   reports `ok:false` with diagnostics and the host silently falls back to the IndexedDB scan. It must
//   never throw into the page, and never break WhatsApp Web itself.
// * SAME ENVELOPE. The scan result is byte-compatible with the IndexedDB scan's shape, so the C# side
//   (ChatEntryParser) parses either source with no changes.
//
// Discovery is by CAPABILITY, not by module name. Module names (WAWebChatCollection, …) churn between
// releases and several look-alike modules export a `Chat` property; matching on the actual shape of the
// data ("has getModelsArray(), models have an id and a numeric unreadCount") survives renames.
(function () {
  'use strict';

  if (window.__umStoreBridge) {
    return;
  }

  var MAX_DISCOVERY_MODULES = 12000;

  // Diagnostics are cheap and the only way to tune this against a live account without guessing.
  // Read them from DevTools with: window.__umStore.diagnostics()
  var diag = {
    strategy: null,
    strategiesTried: [],
    moduleCount: 0,
    chatCollection: false,
    contactCollection: false,
    connState: false,
    discoveredAtMs: 0,
    errors: []
  };

  var store = {
    chat: null,      // collection of chat models
    contact: null,   // collection of contact models
    conn: null       // connection/socket state model
  };

  function note(error, where) {
    try {
      var message = (error && error.message) || String(error);
      if (diag.errors.length < 25) {
        diag.errors.push(where + ': ' + message);
      }
    } catch (ignored) { /* diagnostics must never throw */ }
  }

  // ---------------------------------------------------------------------------------------------
  // Module discovery — three strategies, most-modern first.
  // ---------------------------------------------------------------------------------------------

  // Strategy 1 (current WhatsApp Web): the app ships a debug hook exposing its module map keyed by
  // readable names. Cheapest and most stable when present.
  function collectViaDebugRequire() {
    var out = [];
    try {
      var req = window.require || (window.self && window.self.require);
      if (typeof req !== 'function') {
        return out;
      }
      var debugModule = req('__debug');
      if (!debugModule) {
        return out;
      }
      var map = debugModule.modulesMap || debugModule.modules || null;
      if (!map) {
        return out;
      }
      var keys = Object.keys(map);
      for (var i = 0; i < keys.length && out.length < MAX_DISCOVERY_MODULES; i++) {
        try {
          var entry = map[keys[i]];
          // Entries are either the loaded exports or a wrapper carrying them.
          var exports = entry && (entry.defaultExport || entry.exports || entry);
          if (exports) {
            out.push(exports);
          }
        } catch (perModule) { /* a module that throws on access is not one we want */ }
      }
    } catch (error) {
      note(error, 'debug-require');
    }
    return out;
  }

  // Strategy 2 (moduleRaid technique): push a synthetic chunk onto the webpack chunk array and use the
  // require function handed to our callback to materialize every registered module.
  function collectViaWebpackChunk() {
    var out = [];
    try {
      var chunkNames = Object.keys(window).filter(function (k) {
        return k.indexOf('webpackChunk') === 0 && Array.isArray(window[k]);
      });
      for (var c = 0; c < chunkNames.length; c++) {
        var chunk = window[chunkNames[c]];
        try {
          chunk.push([
            ['__um_store_bridge_' + Date.now()],
            {},
            function (req) {
              try {
                var registry = req && req.m;
                if (!registry) {
                  return;
                }
                var ids = Object.keys(registry);
                for (var i = 0; i < ids.length && out.length < MAX_DISCOVERY_MODULES; i++) {
                  try {
                    var exports = req(ids[i]);
                    if (exports) {
                      out.push(exports);
                    }
                  } catch (perModule) { /* skip modules that throw when required */ }
                }
              } catch (inner) {
                note(inner, 'webpack-chunk-inner');
              }
            }
          ]);
        } catch (perChunk) {
          note(perChunk, 'webpack-chunk-push');
        }
      }
    } catch (error) {
      note(error, 'webpack-chunk');
    }
    return out;
  }

  // Strategy 3 (last resort): some builds keep a populated module cache we can read directly.
  function collectViaModuleCache() {
    var out = [];
    try {
      var req = window.require || (window.self && window.self.require);
      var cache = req && (req.c || req.cache);
      if (!cache) {
        return out;
      }
      var ids = Object.keys(cache);
      for (var i = 0; i < ids.length && out.length < MAX_DISCOVERY_MODULES; i++) {
        try {
          var exports = cache[ids[i]] && cache[ids[i]].exports;
          if (exports) {
            out.push(exports);
          }
        } catch (perModule) { /* skip */ }
      }
    } catch (error) {
      note(error, 'module-cache');
    }
    return out;
  }

  // ---------------------------------------------------------------------------------------------
  // Capability probes — identify a collection by the shape of the models it holds.
  // ---------------------------------------------------------------------------------------------

  function modelsOf(candidate) {
    try {
      if (!candidate) {
        return null;
      }
      if (typeof candidate.getModelsArray === 'function') {
        return candidate.getModelsArray();
      }
      // Some collections expose the backing array directly.
      if (Array.isArray(candidate.models)) {
        return candidate.models;
      }
      if (Array.isArray(candidate._models)) {
        return candidate._models;
      }
    } catch (error) { /* a getter that throws is not a usable collection */ }
    return null;
  }

  function looksLikeChatCollection(candidate) {
    var models = modelsOf(candidate);
    if (!models || models.length === 0) {
      return false;
    }
    // Sample the front of the collection: a chat model has an id and a numeric unread count, and
    // virtually always a numeric last-activity stamp (`t`).
    var sampled = 0;
    for (var i = 0; i < models.length && sampled < 5; i++) {
      var m = models[i];
      if (!m || !m.id) {
        continue;
      }
      sampled++;
      if (typeof m.unreadCount === 'number' && (typeof m.t === 'number' || typeof m.t === 'undefined')) {
        return true;
      }
    }
    return false;
  }

  function looksLikeContactCollection(candidate) {
    var models = modelsOf(candidate);
    if (!models || models.length === 0) {
      return false;
    }
    var sampled = 0;
    for (var i = 0; i < models.length && sampled < 8; i++) {
      var m = models[i];
      if (!m || !m.id) {
        continue;
      }
      sampled++;
      // Contact models carry a phone number and/or a push name; chat models carry neither.
      if ('phoneNumber' in m || 'pushname' in m || 'isMyContact' in m) {
        return true;
      }
    }
    return false;
  }

  function looksLikeConnState(candidate) {
    try {
      if (!candidate || typeof candidate !== 'object') {
        return false;
      }
      return ('state' in candidate && 'stream' in candidate) ||
        typeof candidate.canSend === 'boolean' ||
        ('state' in candidate && 'displayInfo' in candidate);
    } catch (error) {
      return false;
    }
  }

  // Walk a module's exports and its shallow properties looking for our collections.
  function inspectModule(exports) {
    var candidates = [exports];
    try {
      // Collections are usually exported as a named property (e.g. `.Chat`, `.ChatCollection`,
      // `.default`), so look one level deep too.
      var keys = Object.keys(exports);
      for (var i = 0; i < keys.length && i < 40; i++) {
        try {
          var value = exports[keys[i]];
          if (value && typeof value === 'object') {
            candidates.push(value);
          }
        } catch (perKey) { /* skip throwing getters */ }
      }
    } catch (error) { /* non-enumerable exports */ }

    for (var c = 0; c < candidates.length; c++) {
      var candidate = candidates[c];
      if (!store.chat && looksLikeChatCollection(candidate)) {
        store.chat = candidate;
      }
      if (!store.contact && looksLikeContactCollection(candidate)) {
        store.contact = candidate;
      }
      if (!store.conn && looksLikeConnState(candidate)) {
        store.conn = candidate;
      }
    }
  }

  function discover() {
    if (store.chat) {
      return true;
    }

    var strategies = [
      ['debug-require', collectViaDebugRequire],
      ['webpack-chunk', collectViaWebpackChunk],
      ['module-cache', collectViaModuleCache]
    ];

    for (var s = 0; s < strategies.length; s++) {
      var name = strategies[s][0];
      diag.strategiesTried.push(name);
      var modules = strategies[s][1]();
      if (!modules.length) {
        continue;
      }

      diag.moduleCount = modules.length;
      for (var i = 0; i < modules.length; i++) {
        try {
          inspectModule(modules[i]);
        } catch (perModule) { /* keep scanning */ }
        if (store.chat && store.contact && store.conn) {
          break;
        }
      }

      if (store.chat) {
        diag.strategy = name;
        break;
      }
    }

    diag.chatCollection = !!store.chat;
    diag.contactCollection = !!store.contact;
    diag.connState = !!store.conn;
    diag.discoveredAtMs = Date.now();
    return !!store.chat;
  }

  // ---------------------------------------------------------------------------------------------
  // Field readers — every WhatsApp model field is probed defensively.
  // ---------------------------------------------------------------------------------------------

  function serializedId(value) {
    try {
      if (!value) {
        return '';
      }
      if (typeof value === 'string') {
        return value;
      }
      if (typeof value._serialized === 'string') {
        return value._serialized;
      }
      if (typeof value.user === 'string' && typeof value.server === 'string') {
        return value.user + '@' + value.server;
      }
    } catch (error) { /* fall through */ }
    return '';
  }

  function digitsOf(value) {
    var s = serializedId(value);
    var at = s.indexOf('@');
    if (at > 0) {
      s = s.slice(0, at);
    }
    return /^\d{7,15}$/.test(s) ? s : '';
  }

  function cleanText(value) {
    if (typeof value !== 'string') {
      return '';
    }
    return value.replace(/\s+/g, ' ').trim();
  }

  // The last message on a chat lives in different places across builds; probe in order of reliability.
  function lastMessageOf(chat) {
    try {
      if (chat.lastMessage) {
        return chat.lastMessage;
      }
      var msgs = chat.msgs;
      if (msgs) {
        if (typeof msgs.last === 'function') {
          var viaLast = msgs.last();
          if (viaLast) {
            return viaLast;
          }
        }
        var array = modelsOf(msgs);
        if (array && array.length) {
          return array[array.length - 1];
        }
      }
    } catch (error) { /* no last message available */ }
    return null;
  }

  function bodyOf(message) {
    if (!message) {
      return '';
    }
    try {
      return cleanText(message.body || message.caption || message.text || '');
    } catch (error) {
      return '';
    }
  }

  function fromMeOf(message) {
    if (!message) {
      return null;
    }
    try {
      if (typeof message.fromMe === 'boolean') {
        return message.fromMe;
      }
      if (message.id && typeof message.id.fromMe === 'boolean') {
        return message.id.fromMe;
      }
    } catch (error) { /* unknown direction */ }
    return null;
  }

  function isRevoked(message) {
    if (!message) {
      return false;
    }
    try {
      return message.type === 'revoked' || message.subtype === 'revoke';
    } catch (error) {
      return false;
    }
  }

  function chatTitle(chat) {
    try {
      var direct = cleanText(chat.formattedTitle || chat.name || '');
      if (direct) {
        return direct;
      }
      var contact = chat.contact;
      if (contact) {
        return cleanText(
          contact.name || contact.pushname || contact.verifiedName || contact.notifyName || ''
        );
      }
    } catch (error) { /* fall through to empty */ }
    return '';
  }

  // Build @lid → phone digits and @lid → display name from the contact collection. Unsaved contacts key
  // their chat by an @lid privacy JID; the contact model is where the real number lives. (Same fact the
  // IndexedDB path relies on — see the P2-A section in AGENTS.md.)
  function buildContactMaps() {
    var phones = Object.create(null);
    var names = Object.create(null);
    var models = modelsOf(store.contact);
    if (!models) {
      return { phones: phones, names: names };
    }
    for (var i = 0; i < models.length; i++) {
      try {
        var contact = models[i];
        var key = serializedId(contact && contact.id);
        if (!key) {
          continue;
        }
        var phone = digitsOf(contact.phoneNumber);
        if (phone) {
          phones[key] = phone;
        }
        var name = cleanText(
          contact.name || contact.pushname || contact.verifiedName || contact.notifyName || ''
        );
        if (name) {
          names[key] = name;
        }
      } catch (perContact) { /* skip malformed contact */ }
    }
    return { phones: phones, names: names };
  }

  // ---------------------------------------------------------------------------------------------
  // Scan — emits the SAME envelope as the IndexedDB scan so the C# parser is source-agnostic.
  // ---------------------------------------------------------------------------------------------

  function scan(maxChats) {
    maxChats = maxChats || 2000;
    var scanDiag = {
      stage: 'start',
      source: 'store-bridge',
      strategy: diag.strategy,
      chats: 0,
      withTs: 0,
      withPreview: 0,
      active: 0,
      caughtUp: 0,
      awaiting: 0
    };

    if (!discover()) {
      scanDiag.stage = 'no-store';
      return { ok: false, conversations: [], diag: scanDiag };
    }

    var models = modelsOf(store.chat);
    if (!models) {
      scanDiag.stage = 'no-models';
      return { ok: false, conversations: [], diag: scanDiag };
    }

    scanDiag.chats = models.length;
    var maps = buildContactMaps();
    var conversations = [];

    for (var i = 0; i < models.length; i++) {
      try {
        var chat = models[i];
        if (!chat) {
          continue;
        }

        var jid = serializedId(chat.id);
        if (!jid) {
          continue;
        }
        var jidLower = jid.toLowerCase();

        // Groups, broadcasts, status updates and newsletters are not customer conversations.
        if (jidLower.indexOf('@g.us') >= 0 ||
          jidLower.indexOf('@broadcast') >= 0 ||
          jidLower.indexOf('@newsletter') >= 0 ||
          chat.isGroup === true) {
          continue;
        }

        var seconds = typeof chat.t === 'number' ? chat.t : 0;
        var last = lastMessageOf(chat);
        if (!seconds && last && typeof last.t === 'number') {
          seconds = last.t;
        }
        if (!seconds) {
          continue;
        }
        scanDiag.withTs++;

        var unread = typeof chat.unreadCount === 'number' && chat.unreadCount > 0 ? chat.unreadCount : 0;
        var name = chatTitle(chat);
        var contactPhone = maps.phones[jid] || digitsOf(jid);

        if (!name || name === 'New message') {
          if (maps.names[jid]) {
            name = maps.names[jid];
          }
        }

        // Drop fully-anonymous @lid privacy contacts — no resolved phone AND no real name means there is
        // no way to identify or open them, so they are non-actionable noise. (Mirrors the IndexedDB path.)
        if (jidLower.indexOf('@lid') >= 0 && !maps.phones[jid]) {
          var trimmed = (name || '').trim();
          if (!trimmed || trimmed === 'New message' || trimmed.replace(/\D/g, '') === digitsOf(jid)) {
            continue;
          }
        }

        // Direction drives "awaiting": the customer had the last word and we have not replied. Unlike the
        // IndexedDB path we do not need a DOM hint or the unread badge as a fallback — the in-memory model
        // carries the real direction. When direction is genuinely unknown, fall back to the unread marker.
        var direction = fromMeOf(last);
        var fromMe = direction === null ? unread === 0 : direction;
        var awaiting = !fromMe && !isRevoked(last);

        // The in-memory message is decrypted, so this is a real preview for EVERY chat — not just the
        // ~60 rendered sidebar rows the DOM harvest could reach.
        var preview = bodyOf(last).slice(0, 120);
        if (preview) {
          scanDiag.withPreview++;
        }

        var iso = new Date(seconds * 1000).toISOString();
        scanDiag.active++;
        if (awaiting) {
          scanDiag.awaiting++;
        } else {
          scanDiag.caughtUp++;
        }

        conversations.push({
          conversationKey: jid,
          customerName: name,
          contactPhone: contactPhone,
          lastInboundBody: fromMe ? '' : preview,
          lastInboundTimestampUtc: iso,
          lastActivityTimestampUtc: iso,
          lastMessageFromMe: fromMe,
          awaiting: awaiting,
          lastMessagePreview: preview,
          unreadCount: unread,
          inboundCount: unread
        });
      } catch (perChat) {
        // Skip a malformed chat rather than failing the whole scan.
      }
    }

    conversations.sort(function (a, b) {
      return new Date(b.lastActivityTimestampUtc) - new Date(a.lastActivityTimestampUtc);
    });
    if (conversations.length > maxChats) {
      conversations = conversations.slice(0, maxChats);
    }

    scanDiag.stage = conversations.length > 0 ? 'done' : 'empty';
    return { ok: conversations.length > 0, conversations: conversations, diag: scanDiag };
  }

  // ---------------------------------------------------------------------------------------------
  // Connection state — mapped onto the session status machine the host understands.
  // ---------------------------------------------------------------------------------------------

  function connectionState() {
    try {
      if (!discover() || !store.conn) {
        return 'unknown';
      }
      var raw = String(store.conn.state || store.conn.stream || '').toUpperCase();
      if (raw.indexOf('CONNECTED') >= 0 || raw === 'MAIN') {
        return 'working';
      }
      if (raw.indexOf('SYNCING') >= 0 || raw.indexOf('RESUMING') >= 0 || raw.indexOf('OPENING') >= 0) {
        return 'syncing';
      }
      if (raw.indexOf('UNPAIRED') >= 0 || raw.indexOf('QR') >= 0 || raw.indexOf('LOGOUT') >= 0) {
        return 'scan-qr';
      }
      if (raw.indexOf('CONFLICT') >= 0 || raw.indexOf('DEPRECATED') >= 0 || raw.indexOf('PROXYBLOCK') >= 0) {
        return 'failed';
      }
      if (raw.indexOf('OFFLINE') >= 0 || raw.indexOf('DISCONNECTED') >= 0) {
        return 'degraded';
      }
    } catch (error) {
      note(error, 'conn-state');
    }
    return 'unknown';
  }

  // ---------------------------------------------------------------------------------------------
  // Host-facing API. Start/poll shape mirrors the IndexedDB scan even though the read is synchronous:
  // discovery may need a retry window while the page boots, and the host already speaks this protocol.
  // ---------------------------------------------------------------------------------------------

  window.__umStore = {
    isReady: function () {
      try {
        return discover();
      } catch (error) {
        note(error, 'is-ready');
        return false;
      }
    },
    connectionState: connectionState,
    diagnostics: function () {
      return JSON.stringify(diag);
    }
  };

  window.__umStartStoreScan = function (maxChats) {
    window.__umStoreScanResult = null;
    try {
      window.__umStoreScanResult = scan(maxChats);
    } catch (error) {
      note(error, 'scan');
      window.__umStoreScanResult = {
        ok: false,
        conversations: [],
        diag: { stage: 'scan-exception', source: 'store-bridge' }
      };
    }
    return true;
  };

  window.__umGetStoreScanResult = function () {
    return window.__umStoreScanResult ? JSON.stringify(window.__umStoreScanResult) : '';
  };

  // Probe-only entry point for the host's health line: does the bridge work on this page right now?
  window.__umStoreBridgeProbe = function () {
    var ready = false;
    try {
      ready = discover();
    } catch (error) {
      note(error, 'probe');
    }
    return JSON.stringify({
      ready: ready,
      strategy: diag.strategy,
      moduleCount: diag.moduleCount,
      chat: diag.chatCollection,
      contact: diag.contactCollection,
      conn: diag.connState,
      connectionState: ready ? connectionState() : 'unknown'
    });
  };

  window.__umStoreBridge = true;

  // Discovery is attempted lazily on first use rather than at document-create: the module registry does
  // not exist until WhatsApp Web's bundle has booted, and an eager attempt here would always miss.
})();
