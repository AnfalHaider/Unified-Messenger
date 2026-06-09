(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = __INSTANCE_ID__;
  var PLATFORM = __PLATFORM__;
  var ADAPTER_ID = 'slack';
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

  function countSidebarBadges() {
    var total = 0;
    var selectors = [
      '.p-channel_sidebar__badge',
      '[data-qa="channel_sidebar_badge"]',
      '.p-unread-badge',
      '.c-unread-badge__count',
      '[data-qa="sidebar_menu_unread_badge"]',
      '.p-channel_sidebar__channel--unread .p-channel_sidebar__badge'
    ];

    selectors.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (badge) {
        if (window.__umIsDomBadgeMuted && window.__umIsDomBadgeMuted(badge)) {
          return;
        }

        total += window.__umSafeParseInt(badge.textContent);
      });
    });

    return total;
  }

  function computeUnreadCount() {
    var domCount = countSidebarBadges();
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
      attributeFilter: ['class', 'data-qa', 'aria-label']
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
      '[data-qa="message_input"]',
      'div[role="textbox"][contenteditable="true"]',
      '.ql-editor',
      '[data-qa="slack_message_input"]'
    ],
    sendSelectors: [
      '[data-qa="texty_send_button"]',
      'button[aria-label="Send"]',
      'button[aria-label="Send now"]',
      'button[data-qa="send_button"]'
    ],
    chatHintSelectors: [
      '[data-qa="channel_name"]',
      '.p-view_header__title',
      '.p-channel_sidebar__name',
      '[data-qa="channel_title"]'
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
