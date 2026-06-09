(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = __INSTANCE_ID__;
  var PLATFORM = __PLATFORM__;
  var ADAPTER_ID = 'telegram';
  var lastPostedCount = -1;
  var pollTimer = null;
  var domObserver = null;
  var publishScheduled = false;
  var lastUrl = location.href;
  var spaNotify = null;
  var historyHooked = false;
  var originalPushState = null;
  var originalReplaceState = null;

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function includeMutedBadges() {
    if (window.__umShouldIncludeMutedBadges) {
      return window.__umShouldIncludeMutedBadges();
    }

    return window.__umIncludeMutedBadges === true;
  }

  function isWebK() {
    if (location.pathname.indexOf('/k/') >= 0) {
      return true;
    }

    var meta = document.querySelector('meta[property="og:url"]');
    return meta && meta.getAttribute('content') && meta.getAttribute('content').indexOf('/k/') >= 0;
  }

  function countWebZ() {
    var total = 0;
    var selectors = includeMutedBadges()
      ? [
        '.chat-list .ListItem .ChatBadge.unread',
        '.chat-list .ListItem.private .ChatBadge.unread',
        '.chat-list .ListItem.group .ChatBadge.unread',
        '.Chat .ChatBadge.unread'
      ]
      : [
        '.chat-list .ListItem.private .ChatBadge.unread:not(.muted)',
        '.chat-list .ListItem.group .ChatBadge.unread:not(.muted)',
        '.Chat .ChatBadge.unread:not(.muted)'
      ];

    selectors.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (badge) {
        if (!includeMutedBadges() && window.__umIsDomBadgeMuted && window.__umIsDomBadgeMuted(badge)) {
          return;
        }

        total += window.__umSafeParseInt(badge.textContent);
      });
    });

    return total;
  }

  function countWebK() {
    var total = 0;
    var selector = includeMutedBadges() ? '.rp' : '.rp:not(.is-muted)';
    var elements = document.querySelectorAll(selector);

    elements.forEach(function (element) {
      if (!includeMutedBadges() && window.__umIsDomBadgeMuted && window.__umIsDomBadgeMuted(element)) {
        return;
      }

      var subtitleBadge = element.querySelector('.dialog-subtitle-badge, .badge');
      if (subtitleBadge) {
        total += window.__umSafeParseInt(subtitleBadge.textContent);
      }
    });

    return total;
  }

  function computeUnreadCount() {
    var domCount = isWebK() ? countWebK() : countWebZ();
    if (domCount > 0) {
      return domCount;
    }

    return window.__umCountFromTitle();
  }

  function publishBadgeCountImmediate() {
    publishScheduled = false;
    var count = computeUnreadCount();
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
  }

  function schedulePublishBadgeCount() {
    if (publishScheduled) {
      return;
    }

    publishScheduled = true;
    window.setTimeout(publishBadgeCountImmediate, 120);
  }

  window.__unifiedMessengerPublishBadge = publishBadgeCountImmediate;

  function observeDom() {
    var root = document.body || document.documentElement;
    if (!root || domObserver) {
      return;
    }

    domObserver = new MutationObserver(function () {
      schedulePublishBadgeCount();
    });

    domObserver.observe(root, {
      childList: true,
      subtree: true,
      characterData: true,
      attributes: true,
      attributeFilter: ['class', 'title', 'data-peer-id']
    });
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
    if (!document.hidden) {
      publishBadgeCountImmediate();
    }
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

    if (domObserver) {
      domObserver.disconnect();
      domObserver = null;
    }

    unhookSpaNavigation();
    document.removeEventListener('visibilitychange', onVisibilityChange);
    window.removeEventListener('load', publishBadgeCountImmediate);
    publishScheduled = false;
    lastPostedCount = -1;
  }

  window.__umAdapterDispose = disposeAdapter;
  if (window.__umRegisterDisposable) {
    window.__umRegisterDisposable(disposeAdapter);
  }

  window.__umInstallNotificationInterceptor(INSTANCE_ID, PLATFORM);
  window.__umInstallOutgoingMessageMonitor(INSTANCE_ID, PLATFORM, {
    composeSelectors: [
      '.input-message-input',
      'div[contenteditable="true"].input-field-input',
      '#editable-message-text',
      'div[contenteditable="true"][role="textbox"]'
    ],
    sendSelectors: [
      '.btn-send',
      'button.send',
      '.Button.send',
      'button[aria-label*="Send"]'
    ],
    chatHintSelectors: [
      '.chat-info .peer-title',
      '.ChatInfo .title',
      '.chat-header .person-title',
      '.MiddleHeader .title'
    ]
  });
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  hookSpaNavigation();
  observeDom();
  startPolling();

  document.addEventListener('visibilitychange', onVisibilityChange);
  window.addEventListener('load', publishBadgeCountImmediate);
})();
