(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';
  var ADAPTER_ID = 'signal';
  var lastPostedCount = -1;
  var pollTimer = null;

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function computeUnreadCount() {
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

  function startPolling() {
    if (pollTimer) {
      return;
    }

    publishBadgeCount();
    pollTimer = window.setInterval(publishBadgeCount, 5000);
  }

  window.__umInstallNotificationInterceptor(INSTANCE_ID, PLATFORM);
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  startPolling();

  document.addEventListener('visibilitychange', publishBadgeCount);
  window.addEventListener('load', publishBadgeCount);
})();
