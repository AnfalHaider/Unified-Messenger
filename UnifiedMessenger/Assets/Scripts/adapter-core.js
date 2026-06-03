(function () {
  'use strict';

  if (window.__unifiedMessengerCore) {
    return;
  }

  window.__unifiedMessengerCore = true;
  window.__umRecentPreviews = Object.create(null);
  window.__umIncludeMutedBadges = __INCLUDE_MUTED_BADGES__;

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
      ServiceWorkerRegistration.prototype.showNotification) {
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
    }

    beat();
    window.__umHeartbeatHandle = window.setInterval(beat, interval);
  };

  window.__umResetAdapterRuntime = function () {
    if (window.__umHeartbeatHandle) {
      window.clearInterval(window.__umHeartbeatHandle);
      window.__umHeartbeatHandle = null;
    }

    delete window.__unifiedMessengerAdapterInstalled;
    delete window.__unifiedMessengerCore;
    delete window.__umOutgoingMonitorInstalled;
    delete window.__umRecentPreviews;
  };

  window.__umCountFromTitle = function () {
    var match = document.title.match(/\((\d+)\)/);
    return match ? parseInt(match[1], 10) : 0;
  };

  window.__umInstallOutgoingMessageMonitor = function (instanceId, platform, options) {
    if (window.__umOutgoingMonitorInstalled) {
      return;
    }

    window.__umOutgoingMonitorInstalled = true;

    var opts = options || {};
    var debounceMs = opts.debounceMs || 500;
    var lastSentAt = 0;

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
      var now = Date.now();
      if (now - lastSentAt < debounceMs) {
        return;
      }

      lastSentAt = now;
      window.__umPostMessage({
        type: 'message-sent',
        instanceId: instanceId,
        platform: platform,
        source: source || 'unknown',
        chatHint: getChatHint(),
        timestampUtc: new Date().toISOString()
      });
    }

    document.addEventListener('keydown', function (event) {
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
    }, true);

    document.addEventListener('click', function (event) {
      if (!isSendButton(event.target)) {
        return;
      }

      emitSent('send-button');
    }, true);
  };

})();
