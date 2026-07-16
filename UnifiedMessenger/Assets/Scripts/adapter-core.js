(function () {
  'use strict';

  if (window.__unifiedMessengerCore) {
    return;
  }

  window.__unifiedMessengerCore = true;
  window.__umRecentPreviews = Object.create(null);
  window.__umIncludeMutedBadges = !!(window.__umConfig && window.__umConfig.includeMutedBadges);
  window.__umRuntimeDisposables = [];

  var previewPruneCounter = 0;
  var previewMaxEntries = 200;
  var previewMaxAgeMs = 600000;

  window.__umPostMessage = function (payload) {
    if (!window.chrome || !window.chrome.webview) {
      return;
    }

    try {
      window.chrome.webview.postMessage(payload);
    } catch (error) {
      console.warn('[UnifiedMessenger] postMessage failed', error);
    }
  };

  window.__umCollapseWhitespace = function (value) {
    return String(value || '').replace(/\s+/g, ' ').trim();
  };

  /**
   * Resolves the active WhatsApp chat JID from sidebar selection or conversation header.
   * Shared by whatsapp-adapter, conversation-context-scraper, and whatsapp-voice-monitor.
   */
  window.__umResolveActiveChatJid = function () {
    var rows = document.querySelectorAll(
      '#pane-side [role="row"][aria-selected="true"], #side [role="row"][aria-selected="true"]'
    );
    for (var i = 0; i < rows.length; i++) {
      var rowId = rows[i].getAttribute && rows[i].getAttribute('data-id');
      if (rowId && rowId.indexOf('@') >= 0) {
        return rowId;
      }
    }

    var headerSelectors = [
      'header [data-testid="conversation-info-header"]',
      '#main header',
      'header[data-testid="conversation-header"]'
    ];

    for (var s = 0; s < headerSelectors.length; s++) {
      var headerNode = document.querySelector(headerSelectors[s]);
      if (!headerNode) {
        continue;
      }

      var dataId = headerNode.getAttribute && headerNode.getAttribute('data-id');
      if (dataId) {
        var match = String(dataId).match(/(\d+@[^_\s]+)/);
        if (match) {
          return match[1];
        }
      }
    }

    return '';
  };

  /**
   * Chrome labels that are not customer names — must stay aligned with ConversationKeyResolver in C#.
   */
  window.__umGenericConversationLabels = {
    inbox: true,
    messages: true,
    'message requests': true,
    'all messages': true,
    unread: true,
    archived: true,
    spam: true
  };

  window.__umIsGenericConversationLabel = function (value) {
    var normalized = window.__umCollapseWhitespace(value).toLowerCase();
    if (!normalized) {
      return true;
    }

    if (window.__umGenericConversationLabels[normalized]) {
      return true;
    }

    return /^(business inbox|customer messages)$/i.test(normalized);
  };

  window.__umResolvePlatformConversationIdentity = function (platform, options) {
    options = options || {};
    var platformKey = String(platform || 'generic').toLowerCase();
    var headerTitle = window.__umCollapseWhitespace(options.headerTitle || options.conversationHint || '');
    var conversationKey = window.__umResolveConversationKey(platformKey, options);
    var customer = window.__umCollapseWhitespace(options.customerName || '');
    if (!customer && headerTitle && !window.__umIsGenericConversationLabel(headerTitle)) {
      customer = headerTitle.split(/[·•|-]/)[0].trim();
    }

    if (!customer) {
      customer = conversationKey || 'Customer';
    }

    return {
      customerName: customer,
      conversationKey: conversationKey,
      conversationHint: conversationKey
    };
  };

  /**
   * Canonical conversation key — must stay aligned with ConversationKeyResolver.cs.
   * WhatsApp: JID; fallback: customer name or message preview prefix.
   */
  window.__umResolveConversationKey = function (platform, options) {
    options = options || {};
    var platformKey = String(platform || 'generic').toLowerCase();

    var explicit = window.__umCollapseWhitespace(options.conversationKey || '');
    if (explicit) {
      return explicit;
    }

    if (options.chatJid) {
      var jid = window.__umCollapseWhitespace(options.chatJid);
      if (jid) {
        return jid;
      }
    }

    var headerTitle = window.__umCollapseWhitespace(options.headerTitle || options.conversationHint || '');
    if (headerTitle && !window.__umIsGenericConversationLabel(headerTitle)) {
      return headerTitle;
    }

    var customer = window.__umCollapseWhitespace(options.customerName || '');
    if (customer && !window.__umIsGenericConversationLabel(customer)) {
      return customer;
    }

    var messagePreview = window.__umCollapseWhitespace(options.messagePreview || options.messageText || '');
    if (messagePreview.length >= 8) {
      return messagePreview.length <= 48 ? messagePreview : messagePreview.slice(0, 48).trim();
    }

    return 'unknown';
  };

  // React-controlled <input>: set the value via the native setter and fire an input event, or React
  // ignores a direct .value assignment.
  function umSetReactInputValue(input, value) {
    var desc = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
    if (desc && desc.set) {
      desc.set.call(input, value);
    } else {
      input.value = value;
    }
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }

  // Diagnostic breadcrumb trail for click-to-focus. Focus has been "flaky, works sometimes" across v4.75-4.79
  // and every fix so far was a hypothesis (name vs number as the search term) shipped without ever seeing what
  // the page actually did on a failing attempt. This records ONE line per attempt; ConversationFocusHelper
  // drains it to app.log after the retry loop ends. Routed through the log rather than a DevTools global on
  // purpose: focus runs on the instance's own webview, and attaching DevTools to the wrong one has produced
  // false readings (callCount:0) twice before.
  window.__umFocusTrace = [];
  function umTrace(step, extra) {
    try {
      var e = { step: step };
      for (var k in extra) { if (Object.prototype.hasOwnProperty.call(extra, k)) { e[k] = extra[k]; } }
      window.__umFocusTrace.push(e);
      if (window.__umFocusTrace.length > 40) { window.__umFocusTrace.shift(); }
    } catch (err) { /* never break focus for a trace */ }
  }
  // WhatsApp's chat rows do NOT respond to element.click(): a synthetic click dispatches only a `click` event,
  // and the row's handler wants the pointer/mouse sequence a real mouse produces. Proven live in v4.86.0 —
  // focus clicked the correct row every time, reported success, and no conversation ever opened
  // (opened=<no-main-pane>). That silent no-op is the whole "click-to-focus is flaky" story; the search term
  // (name vs number, v4.76/v4.78) was never the variable.
  // Dispatch on the DEEPEST element (the title span), never the row: React listens at the document root and
  // maps the event by its target, so an event fired at the row would skip handlers bound to its children.
  function umRealClick(el) {
    if (!el) { return false; }
    try { el.scrollIntoView({ block: 'center' }); } catch (err) { /* not fatal */ }
    var base = { bubbles: true, cancelable: true, view: window, button: 0, detail: 1 };
    var seq = [
      ['pointerdown', 1], ['mousedown', 1],
      ['pointerup', 0], ['mouseup', 0], ['click', 0]
    ];
    try {
      for (var i = 0; i < seq.length; i++) {
        var type = seq[i][0];
        var opts = Object.assign({}, base, { buttons: seq[i][1] });
        var ev;
        if (type.indexOf('pointer') === 0 && typeof window.PointerEvent === 'function') {
          ev = new PointerEvent(type, Object.assign({ pointerId: 1, isPrimary: true, pointerType: 'mouse' }, opts));
        } else {
          ev = new MouseEvent(type, opts);
        }
        el.dispatchEvent(ev);
      }
      return true;
    } catch (err) {
      // Last resort: the plain click that never worked, rather than nothing at all.
      try { el.click(); return true; } catch (err2) { return false; }
    }
  }

  function umTitleOf(row) {
    try {
      var el = row.querySelector('span[title]');
      return el ? window.__umCollapseWhitespace(el.getAttribute('title') || el.textContent || '') : '';
    } catch (err) { return '?'; }
  }

  window.__umFocusConversation = function (platform, conversationKey, customerName, contactPhone) {
    var platformKey = String(platform || '').toLowerCase();
    var key = window.__umCollapseWhitespace(conversationKey || '');
    var name = window.__umCollapseWhitespace(customerName || '');

    if (platformKey === 'whatsapp' || platformKey === 'whatsappbusiness') {
      var keyLower = key.toLowerCase();
      var isLid = keyLower.indexOf('@lid') >= 0;
      var at = keyLower.indexOf('@');
      var bareId = at > 0 ? keyLower.slice(0, at) : keyLower;
      var nameLower = name.toLowerCase();
      // A real name to match on — NOT the "New message" placeholder, and NOT a bare number (unsaved
      // contacts carry their number as the name, which we handle via digits instead).
      var hasRealName = !!nameLower && nameLower !== 'new message' && /[a-z]/i.test(name);
      // The searchable/verifiable phone: prefer the resolved contactPhone (from the lid→phone map for @lid
      // privacy chats) — it's what WhatsApp search matches and what the sidebar row shows. Fall back to the
      // @c.us JID's own digits. Never search a bare @lid id — it isn't a phone (matched unrelated message
      // text before, opening the wrong chat), which is why an @lid with no resolved phone stays unfocusable.
      var phoneDigits = String(contactPhone || '').replace(/\D/g, '');
      // If the resolved contactPhone is missing/short (the contact-store lid→phone map can miss, and an @lid
      // JID isn't itself a phone), fall back to the digits WhatsApp actually shows as the row title — which
      // the scan captured into customerName. That's the real, searchable number even when ContactPhone is empty.
      if (phoneDigits.length < 10 && !hasRealName) {
        var umNameDigits = name.replace(/\D/g, '');
        if (umNameDigits.length >= 10) { phoneDigits = umNameDigits; }
      }
      if (!phoneDigits && !isLid) { phoneDigits = bareId.replace(/\D/g, ''); }

      function umDigitsOf(s) { return String(s || '').replace(/\D/g, ''); }

      // A rendered row identifies OUR chat only if its visible title matches by phone digits or by real name.
      // This is the guard that stops us ever clicking the wrong chat (e.g. a search hit on message text).
      function umRowIsTarget(row) {
        var el = row.querySelector('span[title]');
        var raw = el ? (el.getAttribute('title') || el.textContent || '') : '';
        var lc = window.__umCollapseWhitespace(raw).toLowerCase();
        if (!lc) { return null; }
        var td = umDigitsOf(raw);
        if (phoneDigits && phoneDigits.length >= 8 && td.length >= 8 &&
            (td.indexOf(phoneDigits) >= 0 || phoneDigits.indexOf(td) >= 0)) {
          return el || row;
        }
        if (hasRealName && lc.indexOf(nameLower) >= 0) {
          return el || row;
        }
        return null;
      }

      // 1) Already-rendered row (saved contacts on screen, or a chat whose number is visible).
      var rows = document.querySelectorAll(
        '#pane-side [role="row"], #side [role="row"], [data-testid="chat-list"] [role="row"]'
      );
      for (var r = 0; r < rows.length; r++) {
        var hit = umRowIsTarget(rows[r]);
        if (hit) {
          umTrace('click-rendered', { title: umTitleOf(rows[r]), rows: rows.length });
          umRealClick(hit);
          return true;
        }
      }

      // 2) Search. The phone NUMBER is the most reliable key: WhatsApp matches it to the contact whether the
      //    number is saved under a name or not. Searching by a saved name is flaky (formatting/status noise),
      //    so prefer the number whenever we have one — the row that comes back (shown by name for a saved
      //    contact) is still verified by umRowIsTarget's name check, or by the top-result fallback below.
      //    Only fall back to the name when there is no number, and never search a bare @lid (not a real number).
      var term = phoneDigits.length >= 8 ? phoneDigits : (hasRealName ? name : '');
      if (!term) {
        umTrace('no-term', { name: name, phone: phoneDigits, key: key });
        return false;
      }

      var search = document.querySelector(
        'input[aria-label="Search or start a new chat"], #side input[role="textbox"], #side input[type="text"]'
      );
      if (search) {
        var current = window.__umCollapseWhitespace(search.value || '');
        if (current.toLowerCase() !== window.__umCollapseWhitespace(term).toLowerCase()) {
          // Apply the filter once, then let the focus-helper retry while the filtered list renders. Do NOT
          // clear it on later misses — clearing caused a set→clear→set oscillation that made focus flaky
          // (worked or not depending on which retry the async filter happened to land on).
          umTrace('apply-filter', { term: term, was: current });
          umSetReactInputValue(search, term);
          return false;
        }
        // Filter is applied. Results render asynchronously, so early retries may see zero rows — that's fine,
        // we keep the filter and try again next tick (no clearing).
        var results = document.querySelectorAll('#pane-side [role="row"], #side [role="row"]');
        // Prefer an explicitly verified row (matches our number, or our name).
        for (var i = 0; i < results.length; i++) {
          var vhit = umRowIsTarget(results[i]);
          if (vhit) {
            umTrace('click-verified', { title: umTitleOf(results[i]), at: i, rows: results.length });
            umRealClick(vhit);
            return true;
          }
        }
        // Otherwise, once results have rendered, take the TOP one. A specific number/name search lists the
        // matching chat/contact first (above any message-text hits), and a saved contact is shown by name
        // (no digits) so a number search verifies nothing — the top row is still the right chat.
        if (results.length > 0) {
          // SUSPECT: if the filtered list hasn't re-rendered yet, these rows are still the UNFILTERED sidebar
          // and results[0] is simply the top chat — i.e. the wrong one. The trace records what we're about to
          // click plus the next two titles, so a failing click shows whether the list had actually updated.
          umTrace('click-top-unverified', {
            title: umTitleOf(results[0]),
            next: results.length > 1 ? umTitleOf(results[1]) : '',
            next2: results.length > 2 ? umTitleOf(results[2]) : '',
            rows: results.length,
            term: term
          });
          umRealClick(results[0].querySelector('span[title]') || results[0]);
          return true;
        }
        umTrace('no-results-yet', { term: term });
        // No results rendered yet — keep the filter, retry. (If focus ultimately gives up, the box stays
        // filtered to the target chat, so it's one manual click away rather than lost.)
        return false;
      }
    }

    return false;
  };

  window.__umShouldIncludeMutedBadges = function () {
    return window.__umIncludeMutedBadges === true;
  };

  window.__umIsDomBadgeMuted = function (element) {
    if (window.__umShouldIncludeMutedBadges()) {
      return false;
    }

    if (!element) {
      return false;
    }

    var node = element.nodeType === 1 ? element : element.parentElement;
    while (node) {
      if (node.classList &&
        (node.classList.contains('muted') ||
          node.classList.contains('is-muted') ||
          node.classList.contains('isMuted') ||
          node.getAttribute('data-muted') === 'true')) {
        return true;
      }

      node = node.parentElement;
    }

    return false;
  };

  window.__umRegisterDisposable = function (disposeFn) {
    if (typeof disposeFn === 'function') {
      window.__umRuntimeDisposables.push(disposeFn);
    }
  };

  window.__umSafeParseInt = function (value) {
    if (value === null || value === undefined) {
      return 0;
    }

    var match = String(value).match(/^\d+/);
    return match ? parseInt(match[0], 10) : 0;
  };

  window.__umNormalizePreview = function (title, body) {
    var normalizedTitle = String(title || '').trim();
    var normalizedBody = String(body || '').trim();

    if (/^new message$/i.test(normalizedTitle) && normalizedBody) {
      normalizedTitle = normalizedBody;
      normalizedBody = '';
    }

    return {
      title: normalizedTitle || 'New message',
      body: normalizedBody
    };
  };

  function pruneRecentPreviews(now) {
    var keys = Object.keys(window.__umRecentPreviews);
    if (keys.length <= previewMaxEntries) {
      return;
    }

    for (var i = 0; i < keys.length; i++) {
      var key = keys[i];
      if (now - window.__umRecentPreviews[key] > previewMaxAgeMs) {
        delete window.__umRecentPreviews[key];
      }
    }

    keys = Object.keys(window.__umRecentPreviews);
    if (keys.length > previewMaxEntries) {
      keys.sort(function (a, b) {
        return window.__umRecentPreviews[a] - window.__umRecentPreviews[b];
      });

      var overflow = keys.length - previewMaxEntries;
      for (var j = 0; j < overflow; j++) {
        delete window.__umRecentPreviews[keys[j]];
      }
    }
  }

  window.__umShouldEmitPreview = function (instanceId, title, body, windowMs) {
    var normalized = window.__umNormalizePreview(title, body);
    var signature = instanceId + '|' + normalized.title + '|' + normalized.body;
    var now = Date.now();
    var throttleMs = windowMs || 8000;

    if (window.__umRecentPreviews[signature] &&
      now - window.__umRecentPreviews[signature] < throttleMs) {
      return false;
    }

    window.__umRecentPreviews[signature] = now;

    previewPruneCounter += 1;
    if (previewPruneCounter % 25 === 0) {
      pruneRecentPreviews(now);
    }

    return true;
  };

  window.__umForwardPreview = function (instanceId, platform, title, options) {
    var opts = options || {};
    var normalized = window.__umNormalizePreview(title, opts.body);

    if (!window.__umShouldEmitPreview(instanceId, normalized.title, normalized.body)) {
      return;
    }

    window.__umPostMessage({
      type: 'notification-preview',
      instanceId: instanceId,
      platform: platform,
      title: normalized.title,
      body: normalized.body,
      conversationKey: opts.conversationKey || '',
      customerName: opts.customerName || normalized.title
    });
  };

  window.__umInstallNotificationInterceptor = function (instanceId, platform) {
    if (window.__umNotificationInterceptorInstalled) {
      return;
    }

    window.__umNotificationInterceptorInstalled = true;

    if (window.Notification && !window.__umOriginalNotification) {
      window.__umOriginalNotification = window.Notification;
    }

    if (window.Notification) {
      function UnifiedNotification(title, options) {
        window.__umForwardPreview(instanceId, platform, title, options);
        return {
          close: function () { },
          addEventListener: function () { },
          removeEventListener: function () { }
        };
      }

      UnifiedNotification.permission = 'granted';
      UnifiedNotification.requestPermission = function () {
        return Promise.resolve('granted');
      };

      UnifiedNotification.__unifiedMessengerHooked = true;
      window.Notification = UnifiedNotification;
    }

    if (typeof ServiceWorkerRegistration !== 'undefined' &&
      ServiceWorkerRegistration.prototype.showNotification &&
      !ServiceWorkerRegistration.prototype.__umOriginalShowNotification) {
      ServiceWorkerRegistration.prototype.__umOriginalShowNotification =
        ServiceWorkerRegistration.prototype.showNotification;

      ServiceWorkerRegistration.prototype.showNotification = function (title, options) {
        window.__umForwardPreview(instanceId, platform, title, options);
        return Promise.resolve();
      };
    }

    if (typeof Notification !== 'undefined') {
      try {
        Object.defineProperty(Notification, 'permission', {
          configurable: true,
          get: function () { return 'granted'; }
        });
      } catch (error) {
        // ignore
      }
    }
  };

  window.__umPublishReady = function (instanceId, platform, adapterId) {
    window.__umPostMessage({
      type: 'adapter-ready',
      instanceId: instanceId,
      platform: platform,
      adapterId: adapterId
    });
  };

  window.__umStartHeartbeat = function (instanceId, platform, adapterId, intervalMs) {
    var interval = intervalMs || 30000;

    function beat() {
      window.__umPostMessage({
        type: 'adapter-heartbeat',
        instanceId: instanceId,
        platform: platform,
        adapterId: adapterId
      });
    }

    if (window.__umHeartbeatHandle) {
      window.clearInterval(window.__umHeartbeatHandle);
      window.__umHeartbeatHandle = null;
    }

    beat();
    window.__umHeartbeatHandle = window.setInterval(beat, interval);

    window.__umRegisterDisposable(function () {
      if (window.__umHeartbeatHandle) {
        window.clearInterval(window.__umHeartbeatHandle);
        window.__umHeartbeatHandle = null;
      }
    });
  };

  window.__umResetAdapterRuntime = function () {
    if (window.__umHeartbeatHandle) {
      window.clearInterval(window.__umHeartbeatHandle);
      window.__umHeartbeatHandle = null;
    }

    if (Array.isArray(window.__umRuntimeDisposables)) {
      window.__umRuntimeDisposables.forEach(function (disposeFn) {
        try {
          disposeFn();
        } catch (error) {
          // ignore dispose errors during recovery
        }
      });
    }

    window.__umRuntimeDisposables = [];

    if (typeof window.__umAdapterDispose === 'function') {
      try {
        window.__umAdapterDispose();
      } catch (error) {
        // ignore
      }
    }

    delete window.__umAdapterDispose;

    if (window.__umOutgoingKeydownHandler) {
      document.removeEventListener('keydown', window.__umOutgoingKeydownHandler, true);
      delete window.__umOutgoingKeydownHandler;
    }

    if (window.__umOutgoingClickHandler) {
      document.removeEventListener('click', window.__umOutgoingClickHandler, true);
      delete window.__umOutgoingClickHandler;
    }

    if (window.__umOriginalNotification) {
      window.Notification = window.__umOriginalNotification;
      delete window.__umOriginalNotification;
    }

    if (typeof ServiceWorkerRegistration !== 'undefined' &&
      ServiceWorkerRegistration.prototype.__umOriginalShowNotification) {
      ServiceWorkerRegistration.prototype.showNotification =
        ServiceWorkerRegistration.prototype.__umOriginalShowNotification;
      delete ServiceWorkerRegistration.prototype.__umOriginalShowNotification;
    }

    delete window.__unifiedMessengerAdapterInstalled;
    delete window.__unifiedMessengerCore;
    delete window.__umNotificationInterceptorInstalled;
    delete window.__umOutgoingMonitorInstalled;
    delete window.__umOutgoingDomMonitorInstalled;
    delete window.__umLastMessageSentAt;
    delete window.__umRecentPreviews;
    delete window.__unifiedMessengerPublishBadge;

    // Secondary script install guards — must clear so ReinjectAsync can re-run IIFEs.
    delete window.__umInboundMonitorInstalled;
    delete window.__umConversationContextInstalled;
    delete window.__umConnectionHandshakeInstalled;
    delete window.__umAiDraftInjectInstalled;
    delete window.__umVoiceMonitorInstalled;
    delete window.__umThreadStatusAuditorCore;
    delete window.__umThreadStatusAuditorInstalls;
    delete window.__umWhatsAppAuditorInstalled;
  };

  window.__umCountFromTitle = function () {
    var match = document.title.match(/\((\d+)\)/);
    return match ? parseInt(match[1], 10) : 0;
  };

  window.__umEmitMessageSent = function (instanceId, platform, source, chatHint, conversationKey) {
    var debounceMs = 500;
    var now = Date.now();
    if (window.__umLastMessageSentAt && now - window.__umLastMessageSentAt < debounceMs) {
      return;
    }

    window.__umLastMessageSentAt = now;
    var chatJid = typeof window.__umResolveActiveChatJid === 'function'
      ? window.__umResolveActiveChatJid()
      : '';
    var resolvedKey = window.__umResolveConversationKey(platform, {
      conversationKey: conversationKey,
      conversationHint: chatHint,
      customerName: chatHint,
      headerTitle: chatHint,
      chatJid: chatJid
    });
    window.__umPostMessage({
      type: 'message-sent',
      instanceId: instanceId,
      platform: platform,
      source: source || 'unknown',
      chatHint: chatHint || '',
      conversationKey: resolvedKey,
      timestampUtc: new Date().toISOString()
    });
  };

  window.__umInstallOutgoingDomReplyMonitor = function (instanceId, platform, options) {
    if (window.__umOutgoingDomMonitorInstalled) {
      return;
    }

    window.__umOutgoingDomMonitorInstalled = true;

    var opts = options || {};
    var outgoingSelectors = (opts.outgoingMessageSelectors || []).concat([
      'div.message-out',
      '[class*="message-out"]',
      '[data-testid*="outgoing"]',
      '[data-testid*="message-out"]'
    ]);
    var panelSelectors = (opts.conversationPanelSelectors || []).concat([
      '[data-testid="conversation-panel-messages"]',
      '[role="main"]',
      'main'
    ]);
    var chatHintSelectors = opts.chatHintSelectors || [
      'header [data-testid="conversation-info-header-chat-title"]',
      'header span[title]',
      'header h1',
      'header h2',
      '[aria-label*="Conversation" i]',
      '[role="heading"]'
    ];
    var lastOutgoingSignature = '';
    var domDebounceTimer = null;

    function resolveChatHint() {
      for (var i = 0; i < chatHintSelectors.length; i++) {
        try {
          var node = document.querySelector(chatHintSelectors[i]);
          if (!node) {
            continue;
          }

          var hint = (node.getAttribute && node.getAttribute('title')) ||
            node.textContent ||
            node.innerText ||
            '';
          hint = String(hint).trim();
          if (hint) {
            return hint;
          }
        } catch (error) {
          // ignore
        }
      }

      return '';
    }

    function findLatestOutgoingSignature() {
      for (var p = 0; p < panelSelectors.length; p++) {
        var panel = null;
        try {
          panel = document.querySelector(panelSelectors[p]);
        } catch (error) {
          panel = null;
        }

        if (!panel) {
          continue;
        }

        for (var o = 0; o < outgoingSelectors.length; o++) {
          var nodes;
          try {
            nodes = panel.querySelectorAll(outgoingSelectors[o]);
          } catch (error) {
            nodes = null;
          }

          if (!nodes || nodes.length === 0) {
            continue;
          }

          var lastNode = nodes[nodes.length - 1];
          var text = String(lastNode.textContent || lastNode.innerText || '').replace(/\s+/g, ' ').trim();
          if (text.length >= 1) {
            return text.length > 160 ? text.slice(0, 157) + '...' : text;
          }
        }
      }

      return '';
    }

    function checkOutgoingDom() {
      domDebounceTimer = null;
      var signature = findLatestOutgoingSignature();
      if (!signature || signature === lastOutgoingSignature) {
        return;
      }

      lastOutgoingSignature = signature;
      var chatHint = resolveChatHint();
      var chatJid = typeof window.__umResolveActiveChatJid === 'function'
        ? window.__umResolveActiveChatJid()
        : '';
      var identity = typeof window.__umResolvePlatformConversationIdentity === 'function'
        ? window.__umResolvePlatformConversationIdentity(platform, {
            headerTitle: chatHint,
            customerName: chatHint,
            messagePreview: signature,
            chatJid: chatJid
          })
        : null;
      var conversationKey = identity
        ? identity.conversationKey
        : window.__umResolveConversationKey(platform, {
            headerTitle: chatHint,
            conversationHint: chatHint,
            customerName: chatHint
          });
      var resolvedHint = identity ? identity.customerName : chatHint;
      window.__umEmitMessageSent(instanceId, platform, 'dom-outgoing', resolvedHint, conversationKey);
    }

    function scheduleDomCheck() {
      if (domDebounceTimer) {
        window.clearTimeout(domDebounceTimer);
      }

      domDebounceTimer = window.setTimeout(checkOutgoingDom, 350);
    }

    var domObserver = new MutationObserver(function () {
      scheduleDomCheck();
    });

    var root = document.documentElement || document.body;
    if (root) {
      domObserver.observe(root, {
        childList: true,
        subtree: true,
        characterData: true
      });
    }

    document.addEventListener('click', scheduleDomCheck, true);
    scheduleDomCheck();

    window.__umRegisterDisposable(function () {
      if (domObserver) {
        domObserver.disconnect();
      }

      document.removeEventListener('click', scheduleDomCheck, true);
      if (domDebounceTimer) {
        window.clearTimeout(domDebounceTimer);
        domDebounceTimer = null;
      }

      delete window.__umOutgoingDomMonitorInstalled;
    });
  };

  window.__umInstallOutgoingMessageMonitor = function (instanceId, platform, options) {
    if (window.__umOutgoingMonitorInstalled) {
      return;
    }

    window.__umOutgoingMonitorInstalled = true;

    var opts = options || {};

    var defaultComposeSelectors = [
      'div[contenteditable="true"]',
      'textarea',
      'input[type="text"]',
      '[role="textbox"]'
    ];

    var defaultSendSelectors = [
      'span[data-testid="send"]',
      'button[data-testid="send"]',
      'button[aria-label*="Send"]',
      'button[aria-label*="send"]',
      '.btn-send',
      '.send-button',
      '[data-testid="compose-btn-send"]'
    ];

    var composeSelectors = (opts.composeSelectors || []).concat(defaultComposeSelectors);
    var sendSelectors = (opts.sendSelectors || []).concat(defaultSendSelectors);
    var chatHintSelectors = opts.chatHintSelectors || [
      'header [data-testid="conversation-info-header-chat-title"]',
      'header span[title]',
      '.chat-header .title',
      '.chat-info .peer-title',
      'header h1',
      'header h2'
    ];

    function matchesSelector(element, selectors) {
      if (!element || !element.closest) {
        return false;
      }

      for (var i = 0; i < selectors.length; i++) {
        try {
          if (element.matches(selectors[i]) || element.closest(selectors[i])) {
            return true;
          }
        } catch (error) {
          // ignore invalid selector
        }
      }

      return false;
    }

    function isComposeElement(element) {
      return matchesSelector(element, composeSelectors);
    }

    function isSendButton(element) {
      return matchesSelector(element, sendSelectors);
    }

    function getComposeText(element) {
      if (!element) {
        return '';
      }

      if (typeof element.value === 'string') {
        return element.value;
      }

      return element.textContent || element.innerText || '';
    }

    function getChatHint() {
      for (var i = 0; i < chatHintSelectors.length; i++) {
        try {
          var node = document.querySelector(chatHintSelectors[i]);
          if (node) {
            var hint = (node.getAttribute && node.getAttribute('title')) ||
              node.textContent ||
              node.innerText ||
              '';
            hint = String(hint).trim();
            if (hint) {
              return hint;
            }
          }
        } catch (error) {
          // ignore
        }
      }

      return '';
    }

    function emitSent(source) {
      var chatHint = getChatHint();
      var chatJid = typeof window.__umResolveActiveChatJid === 'function'
        ? window.__umResolveActiveChatJid()
        : '';
      var conversationKey = typeof window.__umResolveConversationKey === 'function'
        ? window.__umResolveConversationKey(platform, {
            chatJid: chatJid,
            headerTitle: chatHint,
            conversationHint: chatHint,
            customerName: chatHint
          })
        : '';
      window.__umEmitMessageSent(instanceId, platform, source, chatHint, conversationKey);
    }

    window.__umOutgoingKeydownHandler = function (event) {
      if (event.key !== 'Enter' || event.shiftKey || event.ctrlKey || event.altKey || event.isComposing) {
        return;
      }

      var target = event.target;
      if (!isComposeElement(target)) {
        return;
      }

      if (!getComposeText(target).trim()) {
        return;
      }

      window.setTimeout(function () {
        emitSent('enter-key');
      }, 0);
    };

    window.__umOutgoingClickHandler = function (event) {
      if (!isSendButton(event.target)) {
        return;
      }

      emitSent('send-button');
    };

    document.addEventListener('keydown', window.__umOutgoingKeydownHandler, true);
    document.addEventListener('click', window.__umOutgoingClickHandler, true);

    window.__umRegisterDisposable(function () {
      if (window.__umOutgoingKeydownHandler) {
        document.removeEventListener('keydown', window.__umOutgoingKeydownHandler, true);
        delete window.__umOutgoingKeydownHandler;
      }

      if (window.__umOutgoingClickHandler) {
        document.removeEventListener('click', window.__umOutgoingClickHandler, true);
        delete window.__umOutgoingClickHandler;
      }

      delete window.__umOutgoingMonitorInstalled;
    });
  };

  window.__umPublishDashboardScrapeStatus = function (instanceId, platform, success, context, detail) {
    window.__umPostMessage({
      type: 'dashboard-scrape-status',
      instanceId: instanceId,
      platform: platform,
      success: success !== false,
      context: context || 'dashboard-scrape',
      detail: detail || '',
      timestampUtc: new Date().toISOString()
    });
  };

  window.__umRunSafeScrape = function (instanceId, platform, context, scrapeFn) {
    try {
      var result = typeof scrapeFn === 'function' ? scrapeFn() : null;
      window.__umPublishDashboardScrapeStatus(instanceId, platform, true, context, '');
      return result;
    } catch (error) {
      var message = error && error.message ? error.message : String(error);
      window.__umPublishDashboardScrapeStatus(instanceId, platform, false, context, message);
      console.warn('[UnifiedMessenger] scrape failed', context, error);
      return null;
    }
  };

  window.__umQueryVisible = function (selector, root) {
    try {
      var scope = root || document;
      var node = scope.querySelector(selector);
      if (!node) {
        return null;
      }

      if (node.offsetParent !== null || node.getClientRects().length > 0) {
        return node;
      }
    } catch (error) {
      console.warn('[UnifiedMessenger] query failed', selector, error);
    }

    return null;
  };

  window.__umFindTextMatch = function (pattern, root) {
    var body = (root || document.body);
    if (!body || !body.innerText) {
      return null;
    }

    var match = body.innerText.match(pattern);
    return match || null;
  };

})();
