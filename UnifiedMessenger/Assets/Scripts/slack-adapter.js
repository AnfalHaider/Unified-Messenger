(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';
  var ADAPTER_ID = 'slack';
  var lastPostedCount = -1;
  var pollTimer = null;

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function countSidebarBadges() {
    var total = 0;
    var selectors = [
      '.p-channel_sidebar__badge',
      '[data-qa="channel_sidebar_badge"]',
      '.p-unread-badge',
      '.c-unread-badge__count'
    ];

    selectors.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (badge) {
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

  function publishBadgeCount() {
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

  window.__unifiedMessengerPublishBadge = publishBadgeCount;

  function observeDom() {
    var root = document.body || document.documentElement;
    if (!root) {
      return;
    }

    var observer = new MutationObserver(function () {
      publishBadgeCount();
    });

    observer.observe(root, {
      childList: true,
      subtree: true,
      characterData: true,
      attributes: true,
      attributeFilter: ['class', 'data-qa', 'aria-label']
    });
  }

  function startPolling() {
    if (pollTimer) {
      return;
    }

    publishBadgeCount();
    pollTimer = window.setInterval(publishBadgeCount, 4000);
  }

  window.__umInstallNotificationInterceptor(INSTANCE_ID, PLATFORM);
  window.__umInstallOutgoingMessageMonitor(INSTANCE_ID, PLATFORM, {
    composeSelectors: [
      '[data-qa="message_input"]',
      'div[role="textbox"][contenteditable="true"]',
      '.ql-editor'
    ],
    sendSelectors: [
      '[data-qa="texty_send_button"]',
      'button[aria-label="Send"]',
      'button[aria-label="Send now"]'
    ],
    chatHintSelectors: [
      '[data-qa="channel_name"]',
      '.p-view_header__title',
      '.p-channel_sidebar__name'
    ]
  });
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  observeDom();
  startPolling();

  window.addEventListener('load', publishBadgeCount);
})();
