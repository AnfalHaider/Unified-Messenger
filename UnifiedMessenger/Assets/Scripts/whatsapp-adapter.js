(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = (window.__umConfig && window.__umConfig.instanceId) || '';
  var PLATFORM = (window.__umConfig && window.__umConfig.platform) || 'whatsapp';
  var ADAPTER_ID = 'whatsapp';
  var lastPostedCount = -1;
  var dbCache = null;
  var pollTimer = null;
  var publishScheduled = false;
  var chatSnapshots = Object.create(null);
  var snapshotsInitialized = false;
  var backfillOptions = {
    mode: 'unread',
    recentDays: 7,
    maxChats: 20
  };
  var lastUrl = location.href;
  var spaNotify = null;
  var historyHooked = false;
  var originalPushState = null;
  var originalReplaceState = null;
  var sidebarDomObserver = null;
  var mainDomObserver = null;
  var domWorkRafScheduled = false;
  var domWorkPending = false;
  var domWorkMaxWaitTimer = null;
  var domWorkDepth = 0;
  var tickCacheStore = null;
  var DOM_WORK_MAX_WAIT_MS = 300;
  var MAX_DOM_WORK_DEPTH = 3;
  var UM_DEV = !!(window.__umDevMode || window.__umStressTestEnabled);
  var labelDedupScratch = Object.create(null);
  var telemetryPayloadScratch = {
    type: 'whatsapp-telemetry',
    instanceId: INSTANCE_ID,
    platform: PLATFORM,
    conversationKey: '',
    customerName: '',
    contactPhoneNumber: '',
    profilePhoneNumber: '',
    verifiedBusinessName: '',
    businessLabels: [],
    lastReceivedAtUtc: null,
    lastSentAtUtc: null,
    lastReceivedKind: 'text',
    lastSentKind: 'text',
    activeMessagePreview: '',
    timestampUtc: ''
  };
  var outgoingPayloadScratch = {
    type: 'whatsapp-outgoing-status',
    instanceId: INSTANCE_ID,
    platform: PLATFORM,
    conversationKey: '',
    deliveryStatus: 'pending',
    messagePreview: '',
    timestampUtc: ''
  };
  var signaturePartsScratch = ['', '', '', '', '', ''];

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function normalizeText(value) {
    if (value == null) {
      return '';
    }

    var text = typeof value === 'string' ? value : String(value);
    var start = 0;
    var end = text.length;

    while (start < end && /\s/.test(text.charAt(start))) {
      start++;
    }

    while (end > start && /\s/.test(text.charAt(end - 1))) {
      end--;
    }

    if (start >= end) {
      return '';
    }

    var collapsed = '';
    var lastWasSpace = false;
    for (var i = start; i < end; i++) {
      var ch = text.charAt(i);
      if (/\s/.test(ch)) {
        if (!lastWasSpace) {
          collapsed += ' ';
          lastWasSpace = true;
        }
      } else {
        collapsed += ch;
        lastWasSpace = false;
      }
    }

    return collapsed;
  }

  function beginDomWorkTick() {
    tickCacheStore = Object.create(null);
  }

  function endDomWorkTick() {
    tickCacheStore = null;
  }

  function getHeaderForTick() {
    if (!tickCacheStore) {
      beginDomWorkTick();
    }

    if (!tickCacheStore.header) {
      tickCacheStore.header = extractChatHeader();
    }

    return tickCacheStore.header;
  }

  function getLabelsForTick(title) {
    if (!title) {
      return [];
    }

    if (!tickCacheStore) {
      beginDomWorkTick();
    }

    if (!tickCacheStore.labelsByTitle) {
      tickCacheStore.labelsByTitle = Object.create(null);
    }

    if (!tickCacheStore.labelsByTitle[title]) {
      tickCacheStore.labelsByTitle[title] = scrapeSidebarLabelsForTitle(title);
    }

    return tickCacheStore.labelsByTitle[title];
  }

  function getConversationKeyForTick(header) {
    if (!tickCacheStore) {
      beginDomWorkTick();
    }

    if (!tickCacheStore.conversationKey) {
      tickCacheStore.conversationKey = resolveActiveConversationKey(header);
    }

    return tickCacheStore.conversationKey;
  }

  function scheduleDomWork(immediate) {
    if (document.hidden && !immediate) {
      return;
    }

    if (immediate) {
      flushDomWork(true);
      return;
    }

    if (domWorkPending) {
      return;
    }

    domWorkPending = true;
    domWorkRafScheduled = true;

    if (!domWorkMaxWaitTimer) {
      domWorkMaxWaitTimer = window.setTimeout(function () {
        flushDomWork(false);
      }, DOM_WORK_MAX_WAIT_MS);
    }

    if (typeof requestAnimationFrame === 'function') {
      requestAnimationFrame(function () {
        flushDomWork(false);
      });
    } else {
      window.setTimeout(function () {
        flushDomWork(false);
      }, 16);
    }
  }

  function flushDomWork(fromImmediate) {
    if (domWorkDepth >= MAX_DOM_WORK_DEPTH) {
      return;
    }

    if (!fromImmediate && !domWorkPending) {
      return;
    }

    domWorkPending = false;
    domWorkRafScheduled = false;
    if (domWorkMaxWaitTimer) {
      window.clearTimeout(domWorkMaxWaitTimer);
      domWorkMaxWaitTimer = null;
    }

    domWorkDepth++;
    beginDomWorkTick();

    try {
      publishTelemetryImmediate();
      publishOutgoingStatusFromDom();
    } finally {
      endDomWorkTick();
      domWorkDepth--;
    }
  }

  function disconnectDomObservers() {
    if (sidebarDomObserver) {
      sidebarDomObserver.disconnect();
    }

    if (mainDomObserver) {
      mainDomObserver.disconnect();
    }
  }

  function reconnectDomObservers() {
    if (sidebarDomObserver) {
      attachSidebarObserver();
    }

    if (mainDomObserver) {
      attachMainObserver();
    }
  }

  function queryFirst(selectors) {
    for (var i = 0; i < selectors.length; i++) {
      try {
        var node = document.querySelector(selectors[i]);
        if (node) {
          return node;
        }
      } catch (error) {
        // ignore invalid selector
      }
    }

    return null;
  }

  function findSidebarRowForTitle(chatTitle) {
    if (!chatTitle) {
      return null;
    }

    var rows = document.querySelectorAll(
      '#pane-side [role="row"], #side [role="row"], [data-testid="chat-list"] [role="row"]'
    );

    for (var i = 0; i < rows.length; i++) {
      var row = rows[i];
      if ((row.textContent || '').indexOf(chatTitle) >= 0) {
        return row;
      }
    }

    return null;
  }

  function pushUniqueLabel(labels, label) {
    if (!label || labelDedupScratch[label]) {
      return;
    }

    labelDedupScratch[label] = true;
    labels.push(label);
  }

  function scrapeSidebarLabelsForTitle(chatTitle) {
    var labels = [];
    if (!chatTitle) {
      return labels;
    }

    var row = findSidebarRowForTitle(chatTitle);
    if (!row) {
      return labels;
    }

    for (var key in labelDedupScratch) {
      if (Object.prototype.hasOwnProperty.call(labelDedupScratch, key)) {
        delete labelDedupScratch[key];
      }
    }

    row.querySelectorAll(
      'span[aria-label], span[data-testid="label"], div[title], span[title]'
    ).forEach(function (node) {
      var label = normalizeText(node.getAttribute('aria-label') || node.getAttribute('title') || '');
      if (!label || label.length > 48) {
        return;
      }

      if (/unread|muted|pinned|archived|message/i.test(label)) {
        return;
      }

      pushUniqueLabel(labels, label);
    });

    row.querySelectorAll('span[dir="auto"]').forEach(function (span) {
      var text = normalizeText(span.textContent || '');
      if (!text || text === chatTitle || text.length > 32) {
        return;
      }

      if (/^\d{1,2}:\d{2}/.test(text)) {
        return;
      }

      if (/new customer|booking|vip|pending|follow up|lead/i.test(text)) {
        pushUniqueLabel(labels, text);
      }
    });

    return labels;
  }

  function extractChatHeader() {
    var titleNode = queryFirst([
      'header [data-testid="conversation-info-header-chat-title"]',
      'span[data-testid="conversation-info-header-chat-title"]',
      '#main header span[title]',
      'header[data-testid="conversation-header"] span[title]'
    ]);

    var title = titleNode
      ? normalizeText(titleNode.getAttribute('title') || titleNode.textContent || '')
      : '';

    var subtitleNode = queryFirst([
      'header [data-testid="conversation-info-header-chat-subtitle"]',
      'header span[data-testid="conversation-info-header-chat-subtitle"]',
      '#main header span[dir="auto"]:not([title])'
    ]);

    var subtitle = subtitleNode ? normalizeText(subtitleNode.textContent || '') : '';

    var verifiedBusinessName = '';
    if (/business account|verified business/i.test(subtitle)) {
      verifiedBusinessName = title;
    }

    var phoneMatch = subtitle.match(/\+?\d[\d\s\-()]{7,}/);
    var contactPhoneNumber = phoneMatch ? phoneMatch[0].replace(/\s+/g, '') : '';

    var profilePhoneNode = queryFirst([
      'header a[href^="tel:"]',
      'header [data-testid="phone-number"]'
    ]);
    var profilePhoneNumber = '';
    if (profilePhoneNode) {
      var href = profilePhoneNode.getAttribute('href') || '';
      profilePhoneNumber = href.indexOf('tel:') === 0
        ? href.replace('tel:', '').trim()
        : normalizeText(profilePhoneNode.textContent || '');
    }

    return {
      title: title,
      subtitle: subtitle,
      verifiedBusinessName: verifiedBusinessName,
      contactPhoneNumber: contactPhoneNumber,
      profilePhoneNumber: profilePhoneNumber
    };
  }

  function detectMessageKind(container) {
    if (!container) {
      return 'text';
    }

    if (container.querySelector('[data-testid="audio-play"], [data-icon="audio"], [data-icon="ptt"]')) {
      return 'audio';
    }

    if (container.querySelector('[data-testid="image-thumb"], img[src*="blob:"], [data-testid="media-url-provider"] img')) {
      return 'image';
    }

    var text = normalizeText(container.textContent || '');
    if (/view catalog|catalog item|product list/i.test(text)) {
      return 'catalog';
    }

    if (/booking|appointment|reserve|schedule/i.test(text)) {
      return 'booking';
    }

    return 'text';
  }

  function parseMessageTimestamp(container) {
    if (!container) {
      return null;
    }

    var prePlain = container.getAttribute('data-pre-plain-text');
    if (prePlain) {
      var parsed = Date.parse(prePlain);
      if (!isNaN(parsed)) {
        return new Date(parsed).toISOString();
      }
    }

    var timeNode = container.querySelector('span[data-testid="msg-meta"] span, span[dir="auto"][title]');
    if (timeNode) {
      var title = timeNode.getAttribute('title') || timeNode.textContent || '';
      var titleParsed = Date.parse(title);
      if (!isNaN(titleParsed)) {
        return new Date(titleParsed).toISOString();
      }
    }

    return new Date().toISOString();
  }

  function isOutgoingContainer(node) {
    return node.classList.contains('message-out') ||
      !!node.querySelector('.message-out') ||
      !!node.closest('.message-out');
  }

  function scanConversationTelemetry() {
    var header = getHeaderForTick();
    if (!header.title) {
      return null;
    }

    var conversationKey = getConversationKeyForTick(header);
    if (!conversationKey) {
      return null;
    }

    var containers = document.querySelectorAll('div[data-testid="msg-container"]');
    var lastReceivedAtUtc = null;
    var lastSentAtUtc = null;
    var lastReceivedKind = 'text';
    var lastSentKind = 'text';
    var activePreview = '';

    for (var i = containers.length - 1; i >= 0 && (!lastReceivedAtUtc || !lastSentAtUtc); i--) {
      var node = containers[i];
      var isOutgoing = isOutgoingContainer(node);
      var timestamp = parseMessageTimestamp(node);
      var kind = detectMessageKind(node);

      if (isOutgoing) {
        if (!lastSentAtUtc) {
          lastSentAtUtc = timestamp;
          lastSentKind = kind;
        }
      } else if (!lastReceivedAtUtc) {
        lastReceivedAtUtc = timestamp;
        lastReceivedKind = kind;
        var previewNode = node.querySelector('span.selectable-text, span.copyable-text');
        activePreview = previewNode ? normalizeText(previewNode.textContent || '') : '';
      }
    }

    return {
      conversationKey: conversationKey,
      customerName: header.title,
      contactPhoneNumber: header.contactPhoneNumber || '',
      profilePhoneNumber: header.profilePhoneNumber || '',
      verifiedBusinessName: header.verifiedBusinessName || '',
      businessLabels: getLabelsForTick(header.title),
      lastReceivedAtUtc: lastReceivedAtUtc,
      lastSentAtUtc: lastSentAtUtc,
      lastReceivedKind: lastReceivedKind,
      lastSentKind: lastSentKind,
      activeMessagePreview: activePreview
    };
  }

  var lastTelemetrySignature = '';

  function publishTelemetryImmediate() {
    var telemetry = scanConversationTelemetry();
    if (!telemetry) {
      return;
    }

    signaturePartsScratch[0] = telemetry.conversationKey;
    signaturePartsScratch[1] = telemetry.lastReceivedAtUtc || '';
    signaturePartsScratch[2] = telemetry.lastSentAtUtc || '';
    signaturePartsScratch[3] = telemetry.lastReceivedKind;
    signaturePartsScratch[4] = telemetry.lastSentKind;
    signaturePartsScratch[5] = telemetry.activeMessagePreview;
    var signature = signaturePartsScratch.join('|');

    if (signature === lastTelemetrySignature) {
      return;
    }

    lastTelemetrySignature = signature;

    telemetryPayloadScratch.conversationKey = telemetry.conversationKey;
    telemetryPayloadScratch.customerName = telemetry.customerName;
    telemetryPayloadScratch.contactPhoneNumber = telemetry.contactPhoneNumber;
    telemetryPayloadScratch.profilePhoneNumber = telemetry.profilePhoneNumber;
    telemetryPayloadScratch.verifiedBusinessName = telemetry.verifiedBusinessName;
    telemetryPayloadScratch.businessLabels = telemetry.businessLabels;
    telemetryPayloadScratch.lastReceivedAtUtc = telemetry.lastReceivedAtUtc;
    telemetryPayloadScratch.lastSentAtUtc = telemetry.lastSentAtUtc;
    telemetryPayloadScratch.lastReceivedKind = telemetry.lastReceivedKind;
    telemetryPayloadScratch.lastSentKind = telemetry.lastSentKind;
    telemetryPayloadScratch.activeMessagePreview = telemetry.activeMessagePreview;
    telemetryPayloadScratch.timestampUtc = new Date().toISOString();

    postMessage(telemetryPayloadScratch);
  }

  function schedulePublishTelemetry() {
    scheduleDomWork(false);
  }

  function detectOutgoingDeliveryStatus(container) {
    if (!container) {
      return 'pending';
    }

    var statusRoot = container.querySelector
      ? (container.querySelector('[data-testid="msg-meta"]') || container)
      : container;

    var icon = statusRoot.querySelector
      ? statusRoot.querySelector('span[data-icon], span[data-testid*="status"]')
      : null;

    var dataIcon = icon ? (icon.getAttribute('data-icon') || '') : '';
    var aria = icon ? (icon.getAttribute('aria-label') || '') : '';

    if (/msg-dblcheck-ack|blue/i.test(dataIcon) || /read/i.test(aria)) {
      return 'read';
    }

    if (/msg-dblcheck|dblcheck/i.test(dataIcon) || /delivered/i.test(aria)) {
      return 'delivered';
    }

    if (/msg-check|check/i.test(dataIcon) || /sent/i.test(aria)) {
      return 'sent';
    }

    return 'pending';
  }

  function resolveActiveChatJid() {
    return typeof window.__umResolveActiveChatJid === 'function'
      ? window.__umResolveActiveChatJid()
      : '';
  }

  function resolveActiveConversationKey(header) {
    var chatJid = resolveActiveChatJid();
    if (typeof window.__umResolvePlatformConversationIdentity === 'function') {
      var identity = window.__umResolvePlatformConversationIdentity(PLATFORM, {
        headerTitle: header.title,
        messagePreview: '',
        chatJid: chatJid
      });
      if (identity && identity.conversationKey) {
        return identity.conversationKey;
      }
    }

    return header.title || '';
  }

  var lastOutgoingStatusKey = '';
  var activeContextTimer = null;

  function findNewestOutgoingContainer(root) {
    var scope = root || document;
    var containers = scope.querySelectorAll('div[data-testid="msg-container"]');
    if (!containers.length) {
      return null;
    }

    for (var i = containers.length - 1; i >= 0; i--) {
      if (isOutgoingContainer(containers[i])) {
        return containers[i];
      }
    }

    return null;
  }

  function publishOutgoingStatusFromDom() {
    var mainRoot = document.querySelector('#main') ||
      document.querySelector('[data-testid="conversation-panel-messages"]');
    var newest = findNewestOutgoingContainer(mainRoot);
    if (!newest) {
      return;
    }

    var header = getHeaderForTick();
    var conversationKey = getConversationKeyForTick(header);
    if (!conversationKey) {
      return;
    }

    var deliveryStatus = detectOutgoingDeliveryStatus(newest);
    var previewNode = newest.querySelector('span.selectable-text, span.copyable-text');
    var preview = previewNode ? normalizeText(previewNode.textContent || '') : '';
    var signature = conversationKey + '|' + deliveryStatus + '|' + preview.slice(0, 40);
    if (signature === lastOutgoingStatusKey) {
      return;
    }

    lastOutgoingStatusKey = signature;

    outgoingPayloadScratch.conversationKey = conversationKey;
    outgoingPayloadScratch.deliveryStatus = deliveryStatus;
    outgoingPayloadScratch.messagePreview = preview;
    outgoingPayloadScratch.timestampUtc = new Date().toISOString();

    postMessage(outgoingPayloadScratch);
  }

  window.__umWhatsAppDetectDeliveryStatus = detectOutgoingDeliveryStatus;

  if (UM_DEV) {
    window.__umWhatsAppExtractChatHeader = extractChatHeader;
    window.__umWhatsAppScrapeSidebarLabels = scrapeSidebarLabelsForTitle;
  }

  function startActiveContextMonitor() {
    scheduleDomWork(true);

    if (activeContextTimer) {
      return;
    }

    activeContextTimer = window.setInterval(function () {
      if (!document.hidden) {
        scheduleDomWork(false);
      }
    }, 4000);
  }

  function isEligibleChatForBackfill(chat) {
    if (!chat || chat.archive) {
      return false;
    }

    var mode = backfillOptions.mode || 'unread';
    if (mode === 'unread') {
      return isEligibleChat(chat);
    }

    if (mode === 'recent') {
      var recentTs = resolveLastMessageTimestamp(chat);
      if (!recentTs) {
        return false;
      }

      var recentDays = backfillOptions.recentDays || 7;
      var ageMs = Date.now() - Date.parse(recentTs);
      return ageMs <= recentDays * 86400000;
    }

    if (mode === 'all') {
      return !!getChatKey(chat);
    }

    return false;
  }

  window.__umSetBackfillOptions = function (options) {
    options = options || {};
    if (options.mode) {
      backfillOptions.mode = String(options.mode);
    }
    if (options.recentDays) {
      backfillOptions.recentDays = Math.max(1, parseInt(options.recentDays, 10) || 7);
    }
    if (options.maxChats) {
      backfillOptions.maxChats = Math.max(5, parseInt(options.maxChats, 10) || 20);
    }
    return backfillOptions;
  };

  function getChatRowTitle(row) {
    var titleNode = row.querySelector(
      'span[dir="auto"][title], span[title][dir="auto"], div[role="gridcell"] span[dir="auto"]'
    );
    return titleNode ? normalizeText(titleNode.textContent || titleNode.getAttribute('title') || '') : '';
  }

  function getChatRowRelativeTime(row) {
    var timeNode = row.querySelector(
      'div[data-testid="cell-frame-secondary"] span, span[data-testid="msg-time"], aside span[dir="auto"]'
    );
    return timeNode ? normalizeText(timeNode.textContent || '') : '';
  }

  function getChatRowJid(row) {
    if (!row) {
      return '';
    }

    var rowId = row.getAttribute && row.getAttribute('data-id');
    if (rowId && rowId.indexOf('@') >= 0) {
      return rowId;
    }

    var child = row.querySelector('[data-id]');
    if (child) {
      var childId = child.getAttribute('data-id') || '';
      if (childId.indexOf('@') >= 0) {
        return childId;
      }
    }

    return '';
  }

  window.__umCollectSidebarSnapshot = function () {
    var rows = document.querySelectorAll(
      '#pane-side [role="row"], #side [role="row"], [data-testid="chat-list"] [role="row"]'
    );
    var payloadRows = [];

    for (var i = 0; i < rows.length && payloadRows.length < (backfillOptions.maxChats || 20); i++) {
      var row = rows[i];
      var title = getChatRowTitle(row);
      var chatJid = getChatRowJid(row);
      if (!title && !chatJid) {
        continue;
      }

      var previewNode = row.querySelector(
        'span[data-testid="last-msg-text"], span[data-testid="last-msg-status"], div[data-testid="cell-frame-secondary"] span'
      );
      payloadRows.push({
        title: title || chatJid,
        conversationKey: chatJid || title,
        preview: previewNode ? normalizeText(previewNode.textContent || '') : '',
        relativeTime: getChatRowRelativeTime(row),
        timestampUtc: new Date().toISOString()
      });
    }

    postMessage({
      type: 'whatsapp-sidebar-snapshot',
      instanceId: INSTANCE_ID,
      platform: PLATFORM,
      rows: payloadRows
    });

    return { ok: payloadRows.length > 0, rows: payloadRows };
  };

  function readMessageDailyAggregatesFromDb(db, callback) {
    try {
      if (!db.objectStoreNames.contains('message')) {
        callback({ sent: {}, received: {} });
        return;
      }

      var sent = Object.create(null);
      var received = Object.create(null);
      var txn = db.transaction('message', 'readonly');
      var store = txn.objectStore('message');
      var cursorReq = store.openCursor();

      cursorReq.onsuccess = function (event) {
        var cursor = event.target.result;
        if (!cursor) {
          callback({ sent: sent, received: received });
          return;
        }

        var msg = cursor.value;
        if (msg && msg.t) {
          var day = new Date(msg.t * 1000).toISOString().slice(0, 10);
          if (msg.fromMe) {
            sent[day] = (sent[day] || 0) + 1;
          } else {
            received[day] = (received[day] || 0) + 1;
          }
        }

        cursor.continue();
      };

      cursorReq.onerror = function () {
        callback(null);
      };
    } catch (error) {
      callback(null);
    }
  }

  window.__umCollectMessageDailyAggregates = function () {
    return new Promise(function (resolve) {
      if (!window.indexedDB) {
        resolve({ sent: {}, received: {} });
        return;
      }

      function readFromDb(db) {
        readMessageDailyAggregatesFromDb(db, function (result) {
          resolve(result || { sent: {}, received: {} });
        });
      }

      if (dbCache) {
        readFromDb(dbCache);
        return;
      }

      if (!indexedDB.databases) {
        resolve({ sent: {}, received: {} });
        return;
      }

      indexedDB.databases().then(function (databases) {
        var hasModelStorage = databases.some(function (db) {
          return db.name === 'model-storage';
        });

        if (!hasModelStorage) {
          resolve({ sent: {}, received: {} });
          return;
        }

        var request = indexedDB.open('model-storage');
        request.onsuccess = function () {
          dbCache = request.result;
          readFromDb(dbCache);
        };
        request.onerror = function () {
          resolve({ sent: {}, received: {} });
        };
      }).catch(function () {
        resolve({ sent: {}, received: {} });
      });
    });
  };

  // --- IndexedDB-direct history (robust backfill) -------------------------------------------------
  // Reads conversation history straight from WhatsApp Web's local 'model-storage' DB instead of
  // scrolling/clicking the DOM. Gives stable chat JIDs (conversation keys) for ALL chats, with last
  // inbound body/timestamp and direction — no UI automation, lower ban risk, far more complete.

  function umSerializeJidLike(value) {
    if (!value) {
      return '';
    }
    if (typeof value === 'string') {
      return value;
    }
    if (value._serialized) {
      return value._serialized;
    }
    if (typeof value.id === 'string') {
      return value.id;
    }
    return '';
  }

  function umChatJidFromMessage(value, primaryKey) {
    // Serialized message id format: "<fromMe>_<chatJid>_<msgId>" — the JID is the stable chat key.
    var key = typeof primaryKey === 'string' ? primaryKey : umSerializeJidLike(value && value.id);
    if (key && key.indexOf('_') >= 0) {
      var parts = key.split('_');
      if (parts.length >= 2 && parts[1].indexOf('@') >= 0) {
        return parts[1];
      }
    }

    // Fallback: derive from from/to by direction (1:1 chats).
    var fromMe = value && value.fromMe;
    var jid = fromMe ? umSerializeJidLike(value && value.to) : umSerializeJidLike(value && value.from);
    return jid && jid.indexOf('@') >= 0 ? jid : '';
  }

  function umMessageBody(value) {
    if (!value) {
      return '';
    }
    if (typeof value.body === 'string' && value.body) {
      return value.body;
    }
    if (typeof value.caption === 'string' && value.caption) {
      return value.caption;
    }
    if (typeof value.text === 'string' && value.text) {
      return value.text;
    }
    return '';
  }

  window.__umCollectConversationHistoryFromDb = function (maxChats) {
    maxChats = maxChats || 50;
    return new Promise(function (resolve) {
      function fail() {
        resolve({ ok: false, conversations: [] });
      }

      if (!window.indexedDB) {
        fail();
        return;
      }

      function cursorMessages(db) {
        try {
          if (!db.objectStoreNames.contains('message')) {
            fail();
            return;
          }

          var byChat = Object.create(null);
          var txn = db.transaction('message', 'readonly');
          var cursorReq = txn.objectStore('message').openCursor();

          cursorReq.onsuccess = function (event) {
            var cursor = event.target.result;
            if (!cursor) {
              enrichAndResolve(db, byChat);
              return;
            }

            var v = cursor.value;
            if (v && v.t) {
              var jid = umChatJidFromMessage(v, cursor.primaryKey);
              if (jid) {
                var rec = byChat[jid] ||
                  (byChat[jid] = { inboundCount: 0, lastT: 0, lastFromMe: false, lastInboundBody: '', lastInboundT: 0 });
                if (v.t >= rec.lastT) {
                  rec.lastT = v.t;
                  rec.lastFromMe = !!v.fromMe;
                }
                if (!v.fromMe) {
                  rec.inboundCount++;
                  if (v.t >= rec.lastInboundT) {
                    rec.lastInboundT = v.t;
                    rec.lastInboundBody = umMessageBody(v);
                  }
                }
              }
            }

            cursor.continue();
          };

          cursorReq.onerror = fail;
        } catch (error) {
          fail();
        }
      }

      function enrichAndResolve(db, byChat) {
        var titles = Object.create(null);
        var unread = Object.create(null);

        function emit() {
          var keys = Object.keys(byChat).filter(function (jid) {
            return byChat[jid].lastInboundT > 0;
          });
          keys.sort(function (a, b) {
            return byChat[b].lastInboundT - byChat[a].lastInboundT;
          });

          var conversations = [];
          for (var i = 0; i < keys.length && conversations.length < maxChats; i++) {
            var jid = keys[i];
            var rec = byChat[jid];
            conversations.push({
              conversationKey: jid,
              customerName: titles[jid] || jid,
              lastInboundBody: rec.lastInboundBody,
              lastInboundTimestampUtc: new Date(rec.lastInboundT * 1000).toISOString(),
              lastActivityTimestampUtc: new Date(rec.lastT * 1000).toISOString(),
              lastMessageFromMe: rec.lastFromMe,
              unreadCount: unread[jid] || 0,
              inboundCount: rec.inboundCount
            });
          }

          resolve({ ok: conversations.length > 0, conversations: conversations });
        }

        try {
          if (!db.objectStoreNames.contains('chat')) {
            emit();
            return;
          }

          var q = db.transaction('chat', 'readonly').objectStore('chat').getAll();
          q.onsuccess = function (event) {
            var chats = event.target.result || [];
            for (var k = 0; k < chats.length; k++) {
              var key = getChatKey(chats[k]);
              if (!key) {
                continue;
              }
              titles[key] = getChatTitle(chats[k]);
              unread[key] = chats[k].unreadCount || 0;
            }
            emit();
          };
          q.onerror = emit;
        } catch (error) {
          emit();
        }
      }

      if (dbCache) {
        cursorMessages(dbCache);
        return;
      }

      if (!indexedDB.databases) {
        fail();
        return;
      }

      indexedDB.databases().then(function (databases) {
        if (!databases.some(function (d) { return d.name === 'model-storage'; })) {
          fail();
          return;
        }

        var request = indexedDB.open('model-storage');
        request.onsuccess = function () {
          dbCache = request.result;
          cursorMessages(dbCache);
        };
        request.onerror = fail;
      }).catch(fail);
    });
  };

  function collectVisibleHistoryMessages(conversationKey, customerName) {
    var messages = [];
    var containers = document.querySelectorAll('div[data-testid="msg-container"]');
    for (var i = 0; i < containers.length; i++) {
      var node = containers[i];
      var isOutgoing = isOutgoingContainer(node);
      var previewNode = node.querySelector('span.selectable-text, span.copyable-text');
      var body = previewNode ? normalizeText(previewNode.textContent || '') : '';
      if (!body) {
        continue;
      }

      messages.push({
        conversationKey: conversationKey,
        customerName: customerName,
        body: body,
        timestampUtc: parseMessageTimestamp(node),
        isOutgoing: isOutgoing
      });
    }

    return messages;
  }

  window.__umScrollBackOpenChatHistory = function (maxIterations) {
    maxIterations = maxIterations || 4;
    var header = extractChatHeader();
    if (!header || !header.title) {
      return { ok: false, messages: [] };
    }

    var conversationKey = resolveActiveConversationKey(header) || header.title;
    var panel = document.querySelector('#main [data-testid="conversation-panel-messages"], #main');
    var iterations = 0;

    while (panel && iterations < maxIterations) {
      panel.scrollTop = 0;
      iterations++;
    }

    var messages = collectVisibleHistoryMessages(conversationKey, header.title);
    for (var i = 0; i < messages.length; i++) {
      postMessage({
        type: 'whatsapp-history-chunk',
        instanceId: INSTANCE_ID,
        platform: PLATFORM,
        conversationKey: messages[i].conversationKey,
        customerName: messages[i].customerName,
        body: messages[i].body,
        timestampUtc: messages[i].timestampUtc,
        isOutgoing: messages[i].isOutgoing
      });
    }

    return { ok: messages.length > 0, messages: messages };
  };

  window.__umRunDeepBackfillWalk = function (maxChats) {
    maxChats = Math.min(maxChats || 3, 3);
    var rows = document.querySelectorAll(
      '#pane-side [role="row"], #side [role="row"], [data-testid="chat-list"] [role="row"]'
    );
    var collected = [];
    var processed = 0;

    for (var i = 0; i < rows.length && processed < maxChats; i++) {
      var row = rows[i];
      var title = getChatRowTitle(row);
      if (!title) {
        continue;
      }

      try {
        row.click();
      } catch (error) {
        continue;
      }

      processed++;
      var header = extractChatHeader();
      var conversationKey = header ? resolveActiveConversationKey(header) || title : title;
      var chunk = collectVisibleHistoryMessages(conversationKey, title);
      for (var j = 0; j < chunk.length; j++) {
        collected.push(chunk[j]);
        postMessage({
          type: 'whatsapp-history-chunk',
          instanceId: INSTANCE_ID,
          platform: PLATFORM,
          conversationKey: chunk[j].conversationKey,
          customerName: chunk[j].customerName,
          body: chunk[j].body,
          timestampUtc: chunk[j].timestampUtc,
          isOutgoing: chunk[j].isOutgoing
        });
      }
    }

    return {
      ok: collected.length > 0,
      deferredNote: 'Deep backfill MVP — bounded synchronous walk only; async wait-for-#main deferred.',
      messages: collected
    };
  };

  function isEligibleChat(chat) {
    if (!chat || chat.unreadCount <= 0 || chat.archive) {
      return false;
    }

    if (window.__umShouldIncludeMutedBadges && window.__umShouldIncludeMutedBadges()) {
      return true;
    }

    if (window.__umIncludeMutedBadges) {
      return true;
    }

    return chat.muteExpiration === 0 && !chat.isAutoMuted;
  }

  function getChatKey(chat) {
    if (!chat.id) {
      return null;
    }

    if (typeof chat.id === 'string') {
      return chat.id;
    }

    if (chat.id._serialized) {
      return chat.id._serialized;
    }

    try {
      return JSON.stringify(chat.id);
    } catch (error) {
      return null;
    }
  }

  function getChatTitle(chat) {
    return chat.name ||
      chat.formattedTitle ||
      (chat.contact && chat.contact.name) ||
      'New message';
  }

  function getChatPreviewBody(chat, delta) {
    if (chat.lastMessage && typeof chat.lastMessage.body === 'string' && chat.lastMessage.body) {
      return chat.lastMessage.body;
    }

    if (chat.lastMessage && typeof chat.lastMessage.text === 'string' && chat.lastMessage.text) {
      return chat.lastMessage.text;
    }

    var domPreview = getPreviewFromDom(getChatTitle(chat));
    if (domPreview) {
      return domPreview;
    }

    return delta === 1 ? 'New message' : delta + ' new messages';
  }

  function getPreviewFromDom(chatTitle) {
    if (!chatTitle) {
      return '';
    }

    var row = findSidebarRowForTitle(chatTitle);
    if (!row) {
      return '';
    }

    var subtitle = row.querySelector(
      'span[data-testid="last-msg-text"], ' +
        'span[data-testid="last-msg-status"], ' +
        'div[data-testid="cell-frame-secondary"] span, ' +
        'span[dir="ltr"]:last-of-type, ' +
        'span[dir="auto"]:last-of-type, ' +
        'div._ak8k span'
    );
    if (subtitle && subtitle.textContent) {
      return subtitle.textContent.trim();
    }

    var secondaryCells = row.querySelectorAll(
      '[data-testid="cell-frame-secondary"] span, [role="gridcell"] span'
    );
    for (var j = secondaryCells.length - 1; j >= 0; j--) {
      var cellText = normalizeText(secondaryCells[j].textContent || '');
      if (!cellText || cellText === chatTitle) {
        continue;
      }

      if (cellText.length <= 200) {
        return cellText;
      }
    }

    return '';
  }

  function scanForNewPreviews(chats) {
    for (var i = 0; i < chats.length; i++) {
      var chat = chats[i];
      if (!isEligibleChat(chat)) {
        continue;
      }

      var chatKey = getChatKey(chat);
      if (!chatKey) {
        continue;
      }

      var previousUnread = chatSnapshots[chatKey];
      chatSnapshots[chatKey] = chat.unreadCount;

      if (!snapshotsInitialized) {
        continue;
      }

      if (previousUnread === undefined || chat.unreadCount <= previousUnread) {
        continue;
      }

      var delta = chat.unreadCount - (previousUnread || 0);
      var title = getChatTitle(chat);
      var body = getChatPreviewBody(chat, delta);
      var normalized = window.__umNormalizePreview(title, body);

      if (!window.__umShouldEmitPreview(INSTANCE_ID, normalized.title, normalized.body)) {
        continue;
      }

      var previewLabels = scrapeSidebarLabelsForTitle(title);
      postMessage({
        type: 'notification-preview',
        instanceId: INSTANCE_ID,
        platform: PLATFORM,
        title: normalized.title,
        body: normalized.body,
        conversationKey: chatKey,
        customerName: getChatTitle(chat),
        businessLabels: previewLabels
      });
    }

    snapshotsInitialized = true;
  }

  function readChatsFromIndexedDb(callback) {
    if (!window.indexedDB) {
      callback(null);
      return;
    }

    function readFromDb(db) {
      try {
        if (!db.objectStoreNames.contains('chat')) {
          callback(null);
          return;
        }

        var unreadCount = 0;
        var txn = db.transaction('chat', 'readonly');
        var store = txn.objectStore('chat');
        var query = store.getAll();

        query.onsuccess = function (event) {
          var chats = event.target.result || [];
          for (var j = 0; j < chats.length; j++) {
            if (isEligibleChat(chats[j])) {
              unreadCount += chats[j].unreadCount;
            }
          }

          callback({ unreadCount: unreadCount, chats: chats });
        };

        query.onerror = function () {
          callback(null);
        };
      } catch (error) {
        callback(null);
      }
    }

    if (dbCache) {
      readFromDb(dbCache);
      return;
    }

    if (!indexedDB.databases) {
      callback(null);
      return;
    }

    indexedDB.databases().then(function (databases) {
      var hasModelStorage = databases.some(function (db) {
        return db.name === 'model-storage';
      });

      if (!hasModelStorage) {
        callback(null);
        return;
      }

      var request = indexedDB.open('model-storage');
      request.onsuccess = function () {
        dbCache = request.result;
        dbCache.onversionchange = function () {
          dbCache.close();
          dbCache = null;
          snapshotsInitialized = false;
          chatSnapshots = Object.create(null);
        };
        readFromDb(dbCache);
      };
      request.onerror = function () {
        callback(null);
      };
    }).catch(function () {
      callback(null);
    });
  }

  function countFromDomBadges() {
    var total = 0;
    var selectors = [
      'span[data-testid="icon-unread"]',
      'span[data-testid="unread-count"]',
      'span[aria-label*="unread"]'
    ];

    selectors.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (badge) {
        if (window.__umIsDomBadgeMuted && window.__umIsDomBadgeMuted(badge)) {
          return;
        }

        var label = badge.getAttribute('aria-label') || '';
        total += /unread/i.test(label)
          ? window.__umSafeParseInt(label)
          : window.__umSafeParseInt(badge.textContent);
      });
    });

    if (total > 0) {
      return total;
    }

    var chatRows = document.querySelectorAll(
      '#pane-side [role="row"], #side [role="row"], [data-testid="chat-list"] [role="row"]'
    );
    chatRows.forEach(function (row) {
      if (window.__umIsDomBadgeMuted && window.__umIsDomBadgeMuted(row)) {
        return;
      }

      var badgeSpan = row.querySelector('span[aria-label*="unread"], span[data-testid="icon-unread"]');
      if (!badgeSpan) {
        return;
      }

      var label = badgeSpan.getAttribute('aria-label') || badgeSpan.textContent || '';
      if (/unread/i.test(label)) {
        total += window.__umSafeParseInt(label);
      }
    });

    return total;
  }

  function publishBadgeCountImmediate() {
    publishScheduled = false;

    readChatsFromIndexedDb(function (result) {
      if (result && result.chats) {
        scanForNewPreviews(result.chats);

        var count = result.unreadCount;
        if (count !== lastPostedCount) {
          lastPostedCount = count;
          postMessage({
            type: 'badge-count',
            instanceId: INSTANCE_ID,
            platform: PLATFORM,
            count: count
          });
        }

        return;
      }

      var domCount = countFromDomBadges();
      var count = domCount > 0 ? domCount : window.__umCountFromTitle();
      if (count === lastPostedCount) {
        return;
      }

      lastPostedCount = count;
      postMessage({
        type: 'badge-count',
        instanceId: INSTANCE_ID,
        platform: PLATFORM,
        count: count
      });
    });
  }

  function schedulePublishBadgeCount() {
    if (publishScheduled) {
      return;
    }

    publishScheduled = true;
    window.setTimeout(publishBadgeCountImmediate, 250);
  }

  window.__unifiedMessengerPublishBadge = publishBadgeCountImmediate;

  function attachSidebarObserver() {
    var sidebarRoots = [
      document.querySelector('#pane-side'),
      document.querySelector('#side'),
      document.querySelector('[data-testid="chat-list"]')
    ].filter(Boolean);

    if (!sidebarRoots.length) {
      return;
    }

    if (!sidebarDomObserver) {
      sidebarDomObserver = new MutationObserver(function () {
        schedulePublishBadgeCount();
      });
    } else {
      sidebarDomObserver.disconnect();
    }

    sidebarRoots.forEach(function (root) {
      sidebarDomObserver.observe(root, {
        childList: true,
        subtree: true,
        characterData: true,
        attributes: true,
        attributeFilter: ['aria-label', 'data-testid', 'title', 'class']
      });
    });
  }

  function attachMainObserver() {
    var mainRoot = document.querySelector('#main') ||
      document.querySelector('[data-testid="conversation-panel-messages"]');
    if (!mainRoot) {
      return;
    }

    if (!mainDomObserver) {
      mainDomObserver = new MutationObserver(function () {
        scheduleDomWork(false);
      });
    } else {
      mainDomObserver.disconnect();
    }

    mainDomObserver.observe(mainRoot, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['data-icon', 'aria-label', 'class', 'data-testid']
    });
  }

  function observeDom() {
    attachSidebarObserver();
    attachMainObserver();
  }

  function hookSpaNavigation() {
    if (historyHooked) {
      return;
    }

    historyHooked = true;
    spaNotify = function () {
      if (location.href !== lastUrl) {
        lastUrl = location.href;
        lastPostedCount = -1;
        snapshotsInitialized = false;
      }

      schedulePublishBadgeCount();
    };

    window.addEventListener('popstate', spaNotify);
    window.addEventListener('hashchange', spaNotify);

    originalPushState = history.pushState;
    originalReplaceState = history.replaceState;

    history.pushState = function () {
      originalPushState.apply(history, arguments);
      spaNotify();
    };

    history.replaceState = function () {
      originalReplaceState.apply(history, arguments);
      spaNotify();
    };
  }

  function unhookSpaNavigation() {
    if (!historyHooked) {
      return;
    }

    window.removeEventListener('popstate', spaNotify);
    window.removeEventListener('hashchange', spaNotify);

    if (originalPushState) {
      history.pushState = originalPushState;
    }

    if (originalReplaceState) {
      history.replaceState = originalReplaceState;
    }

    historyHooked = false;
    spaNotify = null;
  }

  function onVisibilityChange() {
    if (document.hidden) {
      disconnectDomObservers();
      return;
    }

    reconnectDomObservers();
    publishBadgeCountImmediate();
    scheduleDomWork(true);
  }

  function startPolling() {
    if (pollTimer) {
      return;
    }

    publishBadgeCountImmediate();
    pollTimer = window.setInterval(function () {
      if (!document.hidden) {
        publishBadgeCountImmediate();
      }
    }, 5000);
  }

  function disposeAdapter() {
    if (pollTimer) {
      window.clearInterval(pollTimer);
      pollTimer = null;
    }

    disconnectDomObservers();
    sidebarDomObserver = null;
    mainDomObserver = null;

    if (activeContextTimer) {
      window.clearInterval(activeContextTimer);
      activeContextTimer = null;
    }

    if (domWorkMaxWaitTimer) {
      window.clearTimeout(domWorkMaxWaitTimer);
      domWorkMaxWaitTimer = null;
    }

    domWorkRafScheduled = false;
    domWorkPending = false;
    domWorkDepth = 0;
    lastTelemetrySignature = '';
    lastOutgoingStatusKey = '';

    unhookSpaNavigation();
    document.removeEventListener('visibilitychange', onVisibilityChange);
    window.removeEventListener('load', publishBadgeCountImmediate);
    publishScheduled = false;

    if (dbCache) {
      try {
        dbCache.close();
      } catch (error) {
        // ignore
      }

      dbCache = null;
    }

    chatSnapshots = Object.create(null);
    snapshotsInitialized = false;
    lastPostedCount = -1;
  }

  function resolveLastMessageTimestamp(chat) {
    if (chat && chat.lastMessage && chat.lastMessage.timestamp) {
      try {
        return new Date(chat.lastMessage.timestamp * 1000).toISOString();
      } catch (error) {
        return null;
      }
    }

    if (chat && chat.t) {
      try {
        return new Date(chat.t * 1000).toISOString();
      } catch (error) {
        return null;
      }
    }

    return null;
  }

  window.__umCollectBackfillCandidates = function (maxChats) {
    maxChats = maxChats || backfillOptions.maxChats || 20;

    return new Promise(function (resolve) {
      readChatsFromIndexedDb(function (result) {
        if (!result || !result.chats) {
          resolve({ ok: false, candidates: [] });
          return;
        }

        var eligible = [];
        for (var i = 0; i < result.chats.length; i++) {
          var chat = result.chats[i];
          if (!isEligibleChatForBackfill(chat)) {
            continue;
          }

          eligible.push(chat);
        }

        eligible.sort(function (a, b) {
          var aTs = resolveLastMessageTimestamp(a);
          var bTs = resolveLastMessageTimestamp(b);
          return Date.parse(bTs || 0) - Date.parse(aTs || 0);
        });

        var candidates = [];
        for (var j = 0; j < eligible.length && candidates.length < maxChats; j++) {
          var eligibleChat = eligible[j];
          var chatKey = getChatKey(eligibleChat);
          if (!chatKey) {
            continue;
          }

          var title = getChatTitle(eligibleChat);
          var body = getChatPreviewBody(eligibleChat, eligibleChat.unreadCount || 1);
          if (!body || body.length < 8) {
            continue;
          }

          candidates.push({
            chatKey: chatKey,
            title: title,
            lastMessageBody: body,
            lastMessageTimestamp: resolveLastMessageTimestamp(eligibleChat),
            unreadCount: eligibleChat.unreadCount || 0
          });
        }

        resolve({ ok: candidates.length > 0, candidates: candidates });
      });
    });
  };

  window.__umCommitInboundBaseline = function () {
    readChatsFromIndexedDb(function (result) {
      if (result && result.chats) {
        for (var i = 0; i < result.chats.length; i++) {
          var chat = result.chats[i];
          var chatKey = getChatKey(chat);
          if (!chatKey) {
            continue;
          }

          chatSnapshots[chatKey] = chat.unreadCount;
        }
      }

      snapshotsInitialized = true;
    });

    return { ok: true };
  };

  window.__umAdapterDispose = disposeAdapter;
  if (window.__umRegisterDisposable) {
    window.__umRegisterDisposable(disposeAdapter);
  }

  window.__umInstallNotificationInterceptor(INSTANCE_ID, PLATFORM);
  window.__umInstallOutgoingMessageMonitor(INSTANCE_ID, PLATFORM, {
    composeSelectors: [
      'div[contenteditable="true"][data-tab="10"]',
      'footer div[contenteditable="true"]',
      'div[contenteditable="true"][data-lexical-editor="true"]'
    ],
    sendSelectors: [
      'span[data-testid="send"]',
      'button[aria-label*="Send"]',
      'button[data-testid="compose-btn-send"]'
    ],
    chatHintSelectors: [
      'header [data-testid="conversation-info-header-chat-title"]',
      'span[data-testid="conversation-info-header-chat-title"]',
      'header span[dir="auto"]'
    ]
  });
  if (typeof window.__umInstallVoiceNoteMonitor === 'function') {
    window.__umInstallVoiceNoteMonitor();
  }

  window.__umInstallOutgoingDomReplyMonitor(INSTANCE_ID, PLATFORM, {
    conversationPanelSelectors: [
      '[data-testid="conversation-panel-messages"]',
      '#main'
    ],
    outgoingMessageSelectors: [
      'div.message-out',
      '[data-testid*="msg-out"]'
    ],
    chatHintSelectors: [
      'header [data-testid="conversation-info-header-chat-title"]',
      'span[data-testid="conversation-info-header-chat-title"]',
      'header span[dir="auto"]'
    ]
  });
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);

  window.__umStressTestDomFlood = function (count) {
    if (!UM_DEV && !window.__umStressTestEnabled) {
      return { ok: false, reason: 'dev-only' };
    }

    count = Math.max(1, Math.min(parseInt(count, 10) || 5000, 10000));
    var root = document.querySelector('#main') ||
      document.querySelector('[data-testid="conversation-panel-messages"]') ||
      document.body;
    if (!root) {
      return { ok: false, reason: 'no-root' };
    }

    var intervalMs = 2000 / count;
    var mutations = 0;
    var startedAt = Date.now();
    var floodTimer = window.setInterval(function () {
      if (mutations >= count) {
        window.clearInterval(floodTimer);
        return;
      }

      mutations++;
      var span = document.createElement('span');
      span.textContent = 'x' + mutations;
      span.setAttribute('data-icon', mutations % 4 === 0 ? 'msg-dblcheck-ack' : 'msg-check');
      root.appendChild(span);
      if (mutations % 3 === 0) {
        span.remove();
      }
    }, intervalMs);

    return {
      ok: true,
      target: count,
      intervalMs: intervalMs,
      root: root.id || root.tagName,
      startedAtUtc: new Date(startedAt).toISOString()
    };
  };

  hookSpaNavigation();
  observeDom();
  startPolling();
  startActiveContextMonitor();

  document.addEventListener('visibilitychange', onVisibilityChange);
  window.addEventListener('load', publishBadgeCountImmediate);
})();
