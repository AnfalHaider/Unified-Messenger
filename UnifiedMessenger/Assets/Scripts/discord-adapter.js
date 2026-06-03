(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';
  var ADAPTER_ID = 'discord';
  var lastPostedCount = -1;
  var pollTimer = null;

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function countSidebarBadges() {
    var total = 0;
    var selectors = [
      '[class*="numberBadge"]',
      '[class*="mentionsBadge"]',
      '[class*="unreadPill"]',
      '[aria-label*="unread"] [class*="numberBadge"]'
    ];

    selectors.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (badge) {
        var text = badge.textContent || badge.getAttribute('aria-label') || '';
        total += window.__umSafeParseInt(text);
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
      attributeFilter: ['class', 'aria-label']
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
      'div[role="textbox"][contenteditable="true"]',
      '[class*="textArea"] [contenteditable="true"]',
      '[data-slate-editor="true"]'
    ],
    sendSelectors: [
      'button[aria-label="Send Message"]',
      'button[aria-label*="Send"]'
    ],
    chatHintSelectors: [
      '[class*="title"] h1',
      '[class*="title"] span',
      'h3[class*="title"]'
    ]
  });
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  observeDom();
  startPolling();

  window.addEventListener('load', publishBadgeCount);
})();
