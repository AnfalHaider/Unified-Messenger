(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';
  var ADAPTER_ID = 'telegram';
  var lastPostedCount = -1;
  var pollTimer = null;

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function isWebK() {
    var meta = document.querySelector('meta[property="og:url"]');
    return meta && meta.getAttribute('content') && meta.getAttribute('content').indexOf('/k/') >= 0;
  }

  function countWebZ() {
    var total = 0;
    var includeMuted = window.__umIncludeMutedBadges;
    var selectors = includeMuted
      ? [
        '.chat-list .ListItem.private .ChatBadge.unread',
        '.chat-list .ListItem.group .ChatBadge.unread'
      ]
      : [
        '.chat-list .ListItem.private .ChatBadge.unread:not(.muted)',
        '.chat-list .ListItem.group .ChatBadge.unread:not(.muted)'
      ];

    selectors.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (badge) {
        total += window.__umSafeParseInt(badge.textContent);
      });
    });

    return total;
  }

  function countWebK() {
    var total = 0;
    var selector = window.__umIncludeMutedBadges ? '.rp' : '.rp:not(.is-muted)';
    var elements = document.querySelectorAll(selector);

    elements.forEach(function (element) {
      var subtitleBadge = element.querySelector('.dialog-subtitle-badge');
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
      attributeFilter: ['class', 'title']
    });
  }

  function startPolling() {
    if (pollTimer) {
      return;
    }

    publishBadgeCount();
    pollTimer = window.setInterval(publishBadgeCount, 2500);
  }

  window.__umInstallNotificationInterceptor(INSTANCE_ID, PLATFORM);
  window.__umInstallOutgoingMessageMonitor(INSTANCE_ID, PLATFORM, {
    composeSelectors: [
      '.input-message-input',
      'div[contenteditable="true"].input-field-input'
    ],
    sendSelectors: [
      '.btn-send',
      'button.send',
      '.Button.send'
    ],
    chatHintSelectors: [
      '.chat-info .peer-title',
      '.ChatInfo .title',
      '.chat-header .person-title'
    ]
  });
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  observeDom();
  startPolling();

  window.addEventListener('load', publishBadgeCount);
})();
