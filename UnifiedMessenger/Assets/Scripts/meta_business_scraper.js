(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = __INSTANCE_ID__;
  var PLATFORM = __PLATFORM__;
  var ADAPTER_ID = 'metabusiness';
  var lastPostedCount = -1;
  var lastTitleCount = -1;
  var lastInboundSignalAt = 0;
  var INBOUND_SIGNAL_COOLDOWN_MS = 30000;
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

  function isInboxContext() {
    var href = (location.href || '').toLowerCase();
    if (/business\.facebook\.com.*\/inbox|facebook\.com\/latest\/inbox/.test(href)) {
      return true;
    }

    return !!document.querySelector('[data-pagelet="BizInbox"]');
  }

  function extractInboxBadge(element) {
    if (!element) {
      return 0;
    }

    var aria = element.getAttribute && element.getAttribute('aria-label');
    if (aria) {
      if (/(?:notification\s+settings|ad\s+preferences|help\s+center)/i.test(aria)) {
        return 0;
      }

      var ariaMatch = aria.match(/(\d+)\s*(?:unread|new)\s*(?:customer\s+)?(?:message|conversation)/i);
      if (ariaMatch) {
        return window.__umSafeParseInt(ariaMatch[1]);
      }
    }

    var text = (element.textContent || '').trim();
    if (/^\d{1,3}$/.test(text)) {
      var parentLabel = element.parentElement && element.parentElement.getAttribute('aria-label');
      if (parentLabel && /unread|inbox/i.test(parentLabel)) {
        return window.__umSafeParseInt(text);
      }
    }

    return 0;
  }

  function countFromBizInbox() {
    var roots = document.querySelectorAll('[data-pagelet="BizInbox"]');
    var maxCount = 0;

    for (var r = 0; r < roots.length; r++) {
      var root = roots[r];
      var badges = root.querySelectorAll(
        '[aria-label*="unread" i], [aria-label*="new message" i], [data-testid*="unread" i]'
      );

      for (var b = 0; b < badges.length; b++) {
        var badge = extractInboxBadge(badges[b]);
        if (badge > maxCount) {
          maxCount = badge;
        }
      }
    }

    return maxCount;
  }

  function countFromInboxNav() {
    var inboxLinks = document.querySelectorAll(
      'a[href*="inbox" i], [role="navigation"] a[href*="inbox" i]'
    );
    var maxCount = 0;

    for (var i = 0; i < inboxLinks.length; i++) {
      var node = inboxLinks[i];
      var label = (node.getAttribute && node.getAttribute('aria-label')) || node.textContent || '';
      if (!/inbox/i.test(label) && !/inbox/i.test(node.getAttribute('href') || '')) {
        continue;
      }

      if (/messenger|notification|settings|ads/i.test(label) && !/inbox/i.test(label)) {
        continue;
      }

      var badge = extractInboxBadge(node);
      if (badge > maxCount) {
        maxCount = badge;
      }

      if (node.parentElement) {
        badge = extractInboxBadge(node.parentElement);
        if (badge > maxCount) {
          maxCount = badge;
        }
      }

      var sibling = node.nextElementSibling;
      if (sibling) {
        badge = extractInboxBadge(sibling);
        if (badge > maxCount) {
          maxCount = badge;
        }
      }
    }

    return maxCount;
  }

  function getTelemetryRoot() {
    return document.querySelector('[data-pagelet="BizInbox"]') ||
      document.querySelector('[role="main"]') ||
      null;
  }

  function isTelemetryNoise(text) {
    return /(?:help\s+center|learn\s+more|notification\s+settings|ad\s+preferences|privacy\s+policy)/i.test(text);
  }

  function resolveUnreadCountResult() {
    var inboxCount = countFromBizInbox();
    var navCount = countFromInboxNav();
    var titleCount = isInboxContext() ? countFromTitle() : 0;
    var source = 'none';
    var trusted = false;
    var count = 0;

    if (inboxCount > 0) {
      count = inboxCount;
      source = 'biz-inbox';
      trusted = true;
    } else if (navCount > 0 && titleCount > 0 && Math.abs(navCount - titleCount) <= 1) {
      count = Math.min(navCount, titleCount);
      source = 'nav-title-consensus';
      trusted = true;
    } else if (navCount > 0) {
      count = navCount;
      source = 'inbox-nav';
      trusted = true;
    } else if (titleCount > 0) {
      count = titleCount;
      source = 'title';
      trusted = isInboxContext();
    } else if (isInboxContext()) {
      var main = getTelemetryRoot();
      if (main) {
        var scopedText = main.innerText || '';
        if (!isTelemetryNoise(scopedText)) {
          var match = scopedText.match(
            /(\d+)\s*(?:unread|new)\s*(?:customer\s+)?(?:message|conversation)/i
          );
          if (match) {
            count = window.__umSafeParseInt(match[1]);
            source = 'scoped-main';
          }
        }
      }
    }

    return { count: count, source: source, trusted: trusted };
  }

  function resolveUnreadCount() {
    return resolveUnreadCountResult().count;
  }

  function scanTelemetrySnapshot(unreadCount) {
    var root = getTelemetryRoot();
    if (!root) {
      return {
        averageResponseMinutes: null,
        slaBreachHints: 0,
        unreadCount: unreadCount
      };
    }

    var bodyText = root.innerText || '';
    if (isTelemetryNoise(bodyText)) {
      return {
        averageResponseMinutes: null,
        slaBreachHints: 0,
        unreadCount: unreadCount
      };
    }

    var avgMatch = bodyText.match(
      /(?:avg|average)\s*(?:response|reply)\s*(?:time)?\s*[:.]?\s*(\d+(?:\.\d+)?)\s*(min|minute|hr|hour)/i
    );
    var averageResponseMinutes = null;
    if (avgMatch) {
      var value = parseFloat(avgMatch[1]);
      var unit = String(avgMatch[2] || '').toLowerCase();
      averageResponseMinutes = /hr|hour/.test(unit) ? value * 60 : value;
    }

    var slaBreachHints = 0;
    if (/(?:\d+\s+)?(?:customer\s+)?(?:message|conversation).{0,40}(?:sla|response\s+time)\s*(?:breach|missed|overdue)/i.test(bodyText)) {
      slaBreachHints++;
    }

    var breachMatch = bodyText.match(
      /(\d+)\s*(?:customer\s+)?(?:message|conversation).{0,20}(?:sla\s*)?(?:breach|overdue|late)\s*(?:response|reply)/i
    );
    if (breachMatch) {
      slaBreachHints = Math.max(slaBreachHints, window.__umSafeParseInt(breachMatch[1]));
    }

    return {
      averageResponseMinutes: averageResponseMinutes,
      slaBreachHints: slaBreachHints,
      unreadCount: unreadCount
    };
  }

  function publishTelemetry(unreadCount) {
    var telemetry = scanTelemetrySnapshot(unreadCount);
    if (telemetry.averageResponseMinutes === null &&
        telemetry.slaBreachHints === 0 &&
        telemetry.unreadCount === 0) {
      return;
    }

    postMessage({
      type: 'meta-telemetry-snapshot',
      instanceId: INSTANCE_ID,
      platform: PLATFORM,
      averageResponseMinutes: telemetry.averageResponseMinutes,
      slaBreachHints: telemetry.slaBreachHints,
      unreadCount: telemetry.unreadCount,
      timestampUtc: new Date().toISOString()
    });
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

    window.__umRunSafeScrape(INSTANCE_ID, PLATFORM, 'meta-dashboard', function () {
      var resolved = resolveUnreadCountResult();
      var count = resolved.count;

      if (count !== lastPostedCount) {
        if (count > lastPostedCount &&
            lastPostedCount >= 0 &&
            resolved.trusted) {
          var now = Date.now();
          if (!lastInboundSignalAt || now - lastInboundSignalAt >= INBOUND_SIGNAL_COOLDOWN_MS) {
            emitInboundSignal(count, resolved.source);
            lastInboundSignalAt = now;
          }
        }

        lastPostedCount = count;
        postMessage({
          type: 'badge-count',
          instanceId: INSTANCE_ID,
          platform: PLATFORM,
          count: count
        });
      }

      publishTelemetry(count);
      return true;
    });
  }

  window.__umForceDashboardScrape = publishImmediate;

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
        lastInboundSignalAt = 0;
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
    lastInboundSignalAt = 0;
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
      'div[role="button"][aria-label*="Send" i]',
      'div[aria-label="Send"]',
      '[data-testid*="send" i]',
      'button[type="submit"]'
    ],
    chatHintSelectors: [
      '[aria-label*="Conversation" i]',
      'header h1',
      'header h2',
      '[role="heading"]'
    ]
  });
  window.__umInstallOutgoingDomReplyMonitor(INSTANCE_ID, PLATFORM, {
    conversationPanelSelectors: [
      '[data-pagelet="BizInbox"]',
      '[role="main"]'
    ],
    outgoingMessageSelectors: [
      '[data-testid*="message"][class*="outgoing" i]',
      '[class*="outgoing" i][dir="auto"]',
      '[aria-label*="You sent" i]'
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
