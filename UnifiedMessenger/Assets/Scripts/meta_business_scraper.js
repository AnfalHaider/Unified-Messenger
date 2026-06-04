(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';
  var ADAPTER_ID = 'metabusiness';
  var lastPostedCount = -1;
  var lastTitleCount = -1;
  var pollTimer = null;
  var publishScheduled = false;
  var activeObservers = [];
  var lastUrl = location.href;
  var spaNotify = null;
  var historyHooked = false;
  var originalPushState = null;
  var originalReplaceState = null;

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function trackObserver(observer) {
    activeObservers.push(observer);
    return observer;
  }

  function disconnectObservers() {
    for (var i = 0; i < activeObservers.length; i++) {
      activeObservers[i].disconnect();
    }

    activeObservers = [];
  }

  function countFromTitle() {
    return window.__umCountFromTitle();
  }

  function walkTextNodes(root, visitor) {
    if (!root) {
      return;
    }

    var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
    var node;
    while ((node = walker.nextNode())) {
      visitor(node);
    }
  }

  function extractNumericBadge(element) {
    if (!element) {
      return 0;
    }

    var aria = element.getAttribute && element.getAttribute('aria-label');
    if (aria) {
      var ariaMatch = aria.match(/(\d+)\s*(?:unread|new|message|notification)/i);
      if (ariaMatch) {
        return window.__umSafeParseInt(ariaMatch[1]);
      }
    }

    var text = (element.textContent || '').trim();
    if (/^\d+$/.test(text)) {
      return window.__umSafeParseInt(text);
    }

    var childMatch = text.match(/\b(\d+)\b/);
    return childMatch ? window.__umSafeParseInt(childMatch[1]) : 0;
  }

  function countFromNavigation() {
    var navRoots = document.querySelectorAll(
      '[role="navigation"], nav, [aria-label*="navigation" i], [data-pagelet*="BizInbox"]'
    );
    var maxCount = 0;

    for (var n = 0; n < navRoots.length; n++) {
      var nav = navRoots[n];
      var inboxNodes = nav.querySelectorAll(
        '[aria-label*="Inbox" i], [href*="inbox" i], a, button, [role="link"], [role="button"]'
      );

      for (var i = 0; i < inboxNodes.length; i++) {
        var node = inboxNodes[i];
        var label = (node.getAttribute && node.getAttribute('aria-label')) || node.textContent || '';
        if (!/inbox|messages|messenger/i.test(label)) {
          continue;
        }

        var badge = extractNumericBadge(node);
        if (badge > maxCount) {
          maxCount = badge;
        }

        if (node.parentElement) {
          badge = extractNumericBadge(node.parentElement);
          if (badge > maxCount) {
            maxCount = badge;
          }
        }

        var sibling = node.nextElementSibling;
        if (sibling) {
          badge = extractNumericBadge(sibling);
          if (badge > maxCount) {
            maxCount = badge;
          }
        }
      }

      walkTextNodes(nav, function (textNode) {
        var value = (textNode.textContent || '').trim();
        if (/^inbox$/i.test(value) || /unread/i.test(value)) {
          var parent = textNode.parentElement;
          if (!parent) {
            return;
          }

          var siblings = parent.parentElement ? parent.parentElement.children : [];
          for (var s = 0; s < siblings.length; s++) {
            var siblingBadge = extractNumericBadge(siblings[s]);
            if (siblingBadge > maxCount) {
              maxCount = siblingBadge;
            }
          }
        }
      });
    }

    return maxCount;
  }

  function resolveUnreadCount() {
    var titleCount = countFromTitle();
    var navCount = countFromNavigation();
    return Math.max(titleCount, navCount, 0);
  }

  function emitInboundSignal(count, source) {
    var now = new Date().toISOString();

    postMessage({
      type: 'meta-inbound-message',
      instanceId: INSTANCE_ID,
      platform: PLATFORM,
      unreadCount: count,
      source: source || 'observer',
      timestampUtc: now
    });

    var previewTitle = 'Meta Business inbox';
    var previewBody = count === 1 ? '1 unread customer message' : count + ' unread customer messages';
    var normalized = window.__umNormalizePreview(previewTitle, previewBody);

    if (window.__umShouldEmitPreview(INSTANCE_ID, normalized.title, normalized.body)) {
      window.__umForwardPreview(INSTANCE_ID, PLATFORM, normalized.title, {
        body: normalized.body
      });
    }
  }

  function publishImmediate() {
    publishScheduled = false;
    var count = resolveUnreadCount();

    if (count !== lastPostedCount) {
      if (count > lastPostedCount && lastPostedCount >= 0) {
        emitInboundSignal(count, 'badge-increase');
      }

      lastPostedCount = count;
      postMessage({
        type: 'badge-count',
        instanceId: INSTANCE_ID,
        platform: PLATFORM,
        count: count
      });
    }
  }

  function schedulePublish() {
    if (publishScheduled) {
      return;
    }

    publishScheduled = true;
    window.setTimeout(publishImmediate, 150);
  }

  window.__unifiedMessengerPublishBadge = publishImmediate;

  function observeTitle() {
    var titleElement = document.querySelector('title');
    if (!titleElement) {
      return;
    }

    var titleObserver = trackObserver(new MutationObserver(function () {
      var titleCount = countFromTitle();
      if (titleCount !== lastTitleCount) {
        lastTitleCount = titleCount;
        schedulePublish();
      }
    }));

    titleObserver.observe(titleElement, {
      childList: true,
      characterData: true,
      subtree: true
    });
  }

  function observeNavigation() {
    var attachObserver = function (root) {
      if (!root) {
        return;
      }

      var navObserver = trackObserver(new MutationObserver(function () {
        schedulePublish();
      }));

      navObserver.observe(root, {
        childList: true,
        subtree: true,
        characterData: true,
        attributes: true,
        attributeFilter: ['aria-label', 'aria-hidden', 'data-count', 'class']
      });
    };

    var navRoots = document.querySelectorAll('[role="navigation"], nav, [data-pagelet*="BizInbox"]');
    for (var i = 0; i < navRoots.length; i++) {
      attachObserver(navRoots[i]);
    }

    var bodyObserver = trackObserver(new MutationObserver(function (mutations) {
      for (var m = 0; m < mutations.length; m++) {
        var added = mutations[m].addedNodes;
        for (var a = 0; a < added.length; a++) {
          var node = added[a];
          if (node.nodeType !== 1) {
            continue;
          }

          if (node.matches && (node.matches('[role="navigation"]') || node.matches('nav'))) {
            attachObserver(node);
          }

          var nested = node.querySelectorAll
            ? node.querySelectorAll('[role="navigation"], nav, [data-pagelet*="BizInbox"]')
            : [];
          for (var n = 0; n < nested.length; n++) {
            attachObserver(nested[n]);
          }
        }
      }

      schedulePublish();
    }));

    if (document.body) {
      bodyObserver.observe(document.body, { childList: true, subtree: true });
    }
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
        lastTitleCount = -1;
      }

      schedulePublish();
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
      publishImmediate();
    }
  }

  function startPolling() {
    if (pollTimer) {
      return;
    }

    publishImmediate();
    pollTimer = window.setInterval(function () {
      if (!document.hidden) {
        publishImmediate();
      }
    }, 15000);
  }

  function disposeAdapter() {
    if (pollTimer) {
      window.clearInterval(pollTimer);
      pollTimer = null;
    }

    disconnectObservers();
    unhookSpaNavigation();
    document.removeEventListener('visibilitychange', onVisibilityChange);
    publishScheduled = false;
    lastPostedCount = -1;
    lastTitleCount = -1;
  }

  window.__umAdapterDispose = disposeAdapter;
  if (window.__umRegisterDisposable) {
    window.__umRegisterDisposable(disposeAdapter);
  }

  window.__umInstallNotificationInterceptor(INSTANCE_ID, PLATFORM);
  window.__umInstallOutgoingMessageMonitor(INSTANCE_ID, PLATFORM, {
    composeSelectors: [
      '[role="textbox"]',
      '[contenteditable="true"]',
      'textarea',
      '[aria-label*="Reply" i]',
      '[aria-label*="Message" i]'
    ],
    sendSelectors: [
      'button[aria-label*="Send" i]',
      '[aria-label*="Press Enter to send" i]',
      'div[role="button"][aria-label*="Send" i]'
    ],
    chatHintSelectors: [
      '[aria-label*="Conversation" i]',
      'header h1',
      'header h2',
      '[role="heading"]'
    ]
  });

  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  hookSpaNavigation();
  observeTitle();
  observeNavigation();
  startPolling();

  document.addEventListener('visibilitychange', onVisibilityChange);
})();
