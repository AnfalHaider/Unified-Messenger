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
// Discovery tries the KNOWN module names first (WAWebChatCollection / WAWebContactCollection — two
// lookups instead of walking 16k modules), then falls back to a CAPABILITY scan if those are renamed.
// Either way the result is validated by the same shape probe, so a rename degrades to the scan rather
// than silently handing back a look-alike collection.
//
// Two facts, both established against a live logged-in account (Aug 2026) and both easy to get wrong:
//   * `require('__debug').modulesMap` values are module DESCRIPTORS, not exports. You must pull each
//     module through `require(name)`. Reading `entry.exports` directly yields an empty object, which is
//     how the first version of this file matched a 1-element decoy instead of the real 851-chat store.
//   * Model fields are prototype getters over mangled backing storage: `__x_unreadCount` is an object,
//     `unreadCount` is the number. Always read the clean name. Several obvious names simply don't
//     exist — `chat.name`, `chat.isGroup` and `message.fromMe` are all undefined; the working accessors
//     are `chat.formattedTitle`, the JID suffix, and `message.id.fromMe`. `chat.lastMessage` is also
//     absent — `chat.msgs.last()` is the path that works.
//
// Preview coverage WARMS UP. `chat.msgs` is populated lazily as WhatsApp syncs, so a scan run seconds
// after load reports very few previews (measured: 2% at load, 82% a minute later on the same account).
// Phone/name/awaiting are correct immediately — only the preview text lags. Nothing to fix; just don't
// read a low withPreview on a freshly-loaded account as a discovery failure.
(function () {
  'use strict';

  if (window.__umStoreBridge) {
    return;
  }

  // WhatsApp Web ships ~16,400 modules (measured live, Aug 2026). The cap only bounds the fallback
  // capability scan; keep it comfortably above the real count or discovery silently truncates before
  // reaching the collections — which is exactly how the first version of this file failed.
  var MAX_DISCOVERY_MODULES = 40000;

  // Verified against a live logged-in account (Aug 2026). These are checked with the same capability
  // probe as everything else, so a rename degrades to the scan instead of silently yielding a decoy.
  var KNOWN_CHAT_COLLECTIONS = [['WAWebChatCollection', 'ChatCollection']];
  var KNOWN_CONTACT_COLLECTIONS = [['WAWebContactCollection', 'ContactCollection']];

  // Diagnostics are cheap and the only way to tune this against a live account without guessing.
  // Read them from DevTools with: window.__umStore.diagnostics()
  var diag = {
    strategy: null,
    strategiesTried: [],
    moduleCount: 0,
    moduleTotal: 0,
    chatCollection: false,
    contactCollection: false,
    discoveredAtMs: 0,
    errors: []
  };

  var store = {
    chat: null,      // collection of chat models
    contact: null    // collection of contact models
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
  // Module discovery — known names first, then three capability-scan fallbacks.
  // ---------------------------------------------------------------------------------------------

  function requireFn() {
    var req = window.require || (window.self && window.self.require);
    return typeof req === 'function' ? req : null;
  }

  /// Returns the module-name map from the debug hook, or null.
  //
  // IMPORTANT: the values in this map are module DESCRIPTORS
  // ({ id, refcount, exports, defaultExport, factory, factoryFinished, … }) — NOT the loaded exports.
  // Reading `entry.exports` directly gives you an empty object for anything not yet materialized, so
  // the module must be pulled through require(name) to get the real thing. Getting this wrong is what
  // made the first version of this file match a 1-element decoy instead of the 851-chat collection.
  function debugModuleMap() {
    try {
      var req = requireFn();
      if (!req) {
        return null;
      }
      var debugModule = req('__debug');
      if (!debugModule) {
        return null;
      }
      return debugModule.modulesMap || debugModule.modules || null;
    } catch (error) {
      note(error, 'debug-map');
      return null;
    }
  }

  // Strategy 1: pull the known collection modules straight through require(). Cheapest by far — two
  // lookups instead of walking 16k modules — and it does not force any lazy module to load.
  function discoverByKnownName() {
    var req = requireFn();
    var map = debugModuleMap();
    if (!req || !map) {
      return false;
    }

    function tryPairs(pairs, validate) {
      for (var i = 0; i < pairs.length; i++) {
        var moduleName = pairs[i][0];
        var propertyName = pairs[i][1];
        try {
          if (!(moduleName in map)) {
            continue;
          }
          var exports = req(moduleName);
          if (!exports) {
            continue;
          }
          var candidate = exports[propertyName] || exports;
          if (validate(candidate)) {
            return candidate;
          }
        } catch (perModule) { /* fall through to the next candidate */ }
      }
      return null;
    }

    try {
      diag.moduleTotal = Object.keys(map).length;
    } catch (ignored) { /* count is diagnostics only */ }

    store.chat = store.chat || tryPairs(KNOWN_CHAT_COLLECTIONS, looksLikeChatCollection);
    store.contact = store.contact || tryPairs(KNOWN_CONTACT_COLLECTIONS, looksLikeContactCollection);
    return !!store.chat;
  }

  // Strategy 2: capability scan over the named module map, materializing each candidate via require().
  // Restricted to modules WhatsApp has already finished loading (`factoryFinished`) so we never execute
  // an unrelated lazy module's factory inside the owner's live session just to look at it.
  function collectViaDebugRequire() {
    var out = [];
    try {
      var req = requireFn();
      var map = debugModuleMap();
      if (!req || !map) {
        return out;
      }

      var keys = Object.keys(map);
      diag.moduleTotal = keys.length;
      for (var i = 0; i < keys.length && out.length < MAX_DISCOVERY_MODULES; i++) {
        var key = keys[i];
        // Only collections can hold the models we want; skip the rest to keep this cheap.
        if (!/collection|store/i.test(key)) {
          continue;
        }
        try {
          var descriptor = map[key];
          if (descriptor && descriptor.factoryFinished === false) {
            continue; // not loaded yet — requiring it would run its factory
          }
          var exports = req(key);
          if (exports) {
            out.push(exports);
          }
        } catch (perModule) { /* a module that throws when required is not one we want */ }
      }
    } catch (error) {
      note(error, 'debug-require');
    }
    return out;
  }

  // Strategy 3 (moduleRaid technique): push a synthetic chunk onto the webpack chunk array and use the
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

  // Strategy 4 (last resort): some builds keep a populated module cache we can read directly.
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

  // A chat model carries a serialized id, a numeric unread count, a numeric last-activity stamp, and a
  // `msgs` sub-collection. Requiring all four together is what separates the real 851-chat collection
  // from the small look-alikes that also expose an id and an unreadCount.
  //
  // Note the accessors are prototype getters over mangled backing fields (`__x_unreadCount` is an
  // object, `unreadCount` is the number) — always read the clean name, never the `__x_` one.
  function looksLikeChatCollection(candidate) {
    var models = modelsOf(candidate);
    if (!models || models.length === 0) {
      return false;
    }
    var sampled = 0;
    for (var i = 0; i < models.length && sampled < 8; i++) {
      var m = models[i];
      if (!m || !m.id || !serializedId(m.id)) {
        continue;
      }
      sampled++;
      if (typeof m.unreadCount === 'number' && typeof m.t === 'number' && m.msgs) {
        return true;
      }
    }
    return false;
  }

  // A contact model carries a serialized id plus a push name / display name, and — for the @lid privacy
  // contacts this app depends on — a phoneNumber. It has no `msgs`, which is what separates it from a
  // chat collection. (Only ~44% of contacts carry a phoneNumber, so presence of the accessor is checked
  // rather than a value on the first model.)
  function looksLikeContactCollection(candidate) {
    var models = modelsOf(candidate);
    if (!models || models.length === 0) {
      return false;
    }
    var sampled = 0;
    for (var i = 0; i < models.length && sampled < 8; i++) {
      var m = models[i];
      if (!m || !m.id || !serializedId(m.id) || m.msgs) {
        continue;
      }
      sampled++;
      if ('phoneNumber' in m || 'pushname' in m || typeof m.name === 'string') {
        return true;
      }
    }
    return false;
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
    }
  }

  function discover() {
    if (store.chat) {
      return true;
    }

    // Fast path first: the known module names, verified by the same capability probe as everything
    // else so a WhatsApp rename falls through to the scan rather than yielding a decoy collection.
    try {
      diag.strategiesTried.push('known-name');
      if (discoverByKnownName()) {
        diag.strategy = 'known-name';
        diag.chatCollection = !!store.chat;
        diag.contactCollection = !!store.contact;
        diag.discoveredAtMs = Date.now();
        return true;
      }
    } catch (error) {
      note(error, 'known-name');
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
        if (store.chat && store.contact) {
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

  // WhatsApp's message `type` — 'chat' for text, 'image'/'video'/'ptt'/'audio'/'document'/'sticker' for
  // media, 'revoked' for deleted-for-everyone. This matters because bodyOf() returns '' for BOTH an
  // uncaptioned photo and a message that does not exist, and those two need opposite treatment: the photo
  // is very often "can you do this?" and needs a reply, while a vanished message has nothing to answer.
  // Without the type the app could only guess, and guessing wrong drops a real customer.
  function typeOf(message) {
    if (!message) {
      return '';
    }
    try {
      return String(message.type || message.mediaType || '');
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
      scanDiag.strategy = diag.strategy;
      return { ok: false, conversations: [], diag: scanDiag };
    }

    // Read the strategy AFTER discovery — it isn't known before, and reporting null here is what makes
    // the Settings health line unable to say how the bridge got in.
    scanDiag.strategy = diag.strategy;
    scanDiag.moduleTotal = diag.moduleTotal;

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
        // Keep this list in step with whatsapp-adapter.js — they diverged once, and because the store
        // bridge is the PREFERRED path the divergence was what users actually saw.
        if (jidLower.indexOf('@g.us') >= 0 ||
          jidLower.indexOf('@broadcast') >= 0 ||   // also covers status@broadcast
          jidLower.indexOf('@newsletter') >= 0 ||
          // WhatsApp's own official account: one-way notices you cannot reply to, so once unanswered it
          // sat in the awaiting count forever. '0@' only ever prefixes this account — real E.164 numbers
          // never have a leading-zero local part. This line was missing here but present in the adapter.
          jidLower.indexOf('0@') === 0) {
          continue;
        }
        // NOTE: do not add `chat.isGroup === true` here. That property does not exist on the model (see
        // AGENTS.md); the check was always undefined === true, i.e. dead, and implied a safety net that
        // was not there. JID suffix is the authoritative signal.

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
          inboundCount: unread,
          // Whether a last message exists AT ALL, and what kind it is. `hasLastMessage: false` on a chat
          // whose last activity was weeks ago is the signal that the message is gone — deleted for
          // everyone, or expired under disappearing messages. Those chats were being counted as customers
          // waiting when there is nothing left to reply to (observed live: a chat 57 days old, no body,
          // nothing in the thread when opened).
          hasLastMessage: !!last,
          lastMessageType: typeOf(last)
        });
      } catch (perChat) {
        // Skip a malformed chat rather than failing the whole scan.
      }
    }

    // `hasLastMessage: false` is only meaningful if THIS scan was warm. chat.msgs fills in lazily —
    // measured 2% coverage at load and 82% a minute later — so a scan taken seconds after a reload finds
    // no last message for almost every chat. Reporting that as fact told the host that nearly every
    // conversation's message had been deleted, and the host duly closed the entire queue: 354 real
    // messages collapsed to 5 on screen.
    //
    // So the claim is retracted wholesale unless most chats produced a message. Coverage is a property of
    // the scan, not of any one chat, which is why it cannot be decided in the loop above.
    var withMessage = 0;
    for (var m = 0; m < conversations.length; m++) {
      if (conversations[m].hasLastMessage) {
        withMessage++;
      }
    }
    scanDiag.withLastMessage = withMessage;
    var warm = conversations.length > 0 && withMessage * 2 > conversations.length;
    scanDiag.storeWarm = warm;
    if (!warm) {
      for (var n = 0; n < conversations.length; n++) {
        // null, not false: "we do not know" rather than "there is no message".
        conversations[n].hasLastMessage = null;
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
      moduleTotal: diag.moduleTotal,
      chat: diag.chatCollection,
      contact: diag.contactCollection,
      contact: diag.contactCollection
    });
  };

  window.__umStoreBridge = true;

  // Discovery is attempted lazily on first use rather than at document-create: the module registry does
  // not exist until WhatsApp Web's bundle has booted, and an eager attempt here would always miss.
})();
