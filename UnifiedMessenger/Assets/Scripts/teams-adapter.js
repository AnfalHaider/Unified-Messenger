(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';
  var ADAPTER_ID = 'teams';
  var lastPostedCount = -1;
  var pollTimer = null;

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function countActivityBadges() {
    var total = 0;
    var selectors = [
      '[data-tid="activity-feed-badge"]',
      '.activity-badge',
      '[aria-label*="Activity"] [class*="badge"]',
      '.ts-badge'
    ];

    selectors.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (badge) {
        total += window.__umSafeParseInt(badge.textContent);
      });
    });

    return total;
  }

  function computeUnreadCount() {
    var domCount = countActivityBadges();
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
      attributeFilter: ['class', 'data-tid', 'aria-label']
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
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  observeDom();
  startPolling();

  document.addEventListener('visibilitychange', publishBadgeCount);
  window.addEventListener('load', publishBadgeCount);
})();
