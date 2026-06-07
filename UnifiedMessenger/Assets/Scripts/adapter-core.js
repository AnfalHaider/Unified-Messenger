(function () {
  'use strict';

  if (window.__unifiedMessengerCore) {
    return;
  }

  window.__unifiedMessengerCore = true;
  window.__umRecentPreviews = Object.create(null);
  window.__umIncludeMutedBadges = __INCLUDE_MUTED_BADGES__;
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
   * Canonical conversation key — must stay aligned with ConversationKeyResolver.cs.
   * WhatsApp: JID; Google: review:{id}; Meta: header title; fallback: customer name.
   */
  window.__umResolveConversationKey = function (platform, options) {
    options = options || {};
    var explicit = window.__umCollapseWhitespace(options.conversationKey || '');
    if (explicit) {
      if (/^review:/i.test(explicit)) {
        return 'review:' + explicit.replace(/^review:/i, '').trim();
      }

      return explicit;
    }

    if (options.reviewId) {
      return 'review:' + String(options.reviewId).trim();
    }

    if (options.chatJid) {
      var jid = window.__umCollapseWhitespace(options.chatJid);
      if (jid) {
        return jid;
      }
    }

    var headerTitle = window.__umCollapseWhitespace(options.headerTitle || options.conversationHint || '');
    if (headerTitle) {
      return headerTitle;
    }

    var customer = window.__umCollapseWhitespace(options.customerName || '');
    return customer || 'unknown';
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
      body: normalized.body
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
    var resolvedKey = window.__umResolveConversationKey(platform, {
      conversationKey: conversationKey,
      conversationHint: chatHint,
      customerName: chatHint
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
      var conversationKey = window.__umResolveConversationKey(platform, {
        headerTitle: chatHint,
        conversationHint: chatHint,
        customerName: chatHint
      });
      window.__umEmitMessageSent(instanceId, platform, 'dom-outgoing', chatHint, conversationKey);
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
      window.__umEmitMessageSent(instanceId, platform, source, getChatHint());
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
