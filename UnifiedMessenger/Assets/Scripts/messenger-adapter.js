(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = __INSTANCE_ID__;
  var PLATFORM = __PLATFORM__;
  var ADAPTER_ID = 'messenger';
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

  function parseUnreadFromLabel(label) {
    if (!label) {
      return 0;
    }

    var match = String(label).match(/(\d+)/);
    return match ? parseInt(match[1], 10) : 1;
  }

  function countFromDom() {
    var total = 0;
    var seen = new Set();

    var badgeSelectors = [
      'span[data-testid="mw_unread_count"]',
      '[data-testid="unread-count"]',
      '[data-testid="MWUnreadBadge"]',
      '[aria-label*="unread message"]',
      '[aria-label*="Unread message"]',
      '[aria-label*="unread messages"]',
      '[aria-label*="Unread messages"]',
      '[aria-label*="unread conversation"]',
      '[aria-label*="Unread conversation"]'
    ];

    badgeSelectors.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (element) {
        if (seen.has(element)) {
          return;
        }

        if (window.__umIsDomBadgeMuted && window.__umIsDomBadgeMuted(element)) {
          return;
        }

        seen.add(element);
        var label = element.getAttribute('aria-label');
        var text = element.textContent;
        total += label ? parseUnreadFromLabel(label) : window.__umSafeParseInt(text);
      });
    });

    if (total > 0) {
      return total;
    }

    document.querySelectorAll(
      'div[role="row"], div[role="listitem"], div[role="gridcell"]'
    ).forEach(function (row) {
      var unreadMarker = row.querySelector(
        '[aria-label*="unread"], [data-testid="mw_unread_count"], [data-testid="unread-count"], [data-testid="MWUnreadBadge"]'
      );
      if (!unreadMarker || seen.has(unreadMarker)) {
        return;
      }

      if (window.__umIsDomBadgeMuted && window.__umIsDomBadgeMuted(unreadMarker)) {
        return;
      }

      seen.add(unreadMarker);
      var label = unreadMarker.getAttribute('aria-label');
      total += label ? parseUnreadFromLabel(label) : window.__umSafeParseInt(unreadMarker.textContent);
    });

    return total;
  }

  function computeUnreadCount() {
    var domCount = countFromDom();
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
      attributeFilter: ['class', 'title', 'aria-label', 'data-testid']
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
    window.removeEventListener('pageshow', publishBadgeCountImmediate);
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
      'div[contenteditable="true"][role="textbox"]',
      'div[aria-label="Message"][contenteditable="true"]',
      'div[aria-label*="Message"][contenteditable="true"]',
      'div[aria-label*="Aa"][contenteditable="true"]'
    ],
    sendSelectors: [
      'div[aria-label="Send"]',
      'div[aria-label="Press enter to send"]',
      'div[aria-label*="Send"][role="button"]',
      'div[aria-label*="Send message"][role="button"]'
    ],
    chatHintSelectors: [
      'h1[dir="auto"]',
      '[data-testid="conversation-title"]',
      '[data-testid="thread-title"]',
      '[data-testid="chat-title"]'
    ]
  });
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  hookSpaNavigation();
  observeDom();
  startPolling();

  document.addEventListener('visibilitychange', onVisibilityChange);
  window.addEventListener('load', publishBadgeCountImmediate);
  window.addEventListener('pageshow', publishBadgeCountImmediate);
})();
