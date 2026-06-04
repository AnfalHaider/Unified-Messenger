(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';
  var ADAPTER_ID = 'googlebusiness';
  var lastPostedUnreplied = -1;
  var knownReviewKeys = Object.create(null);
  var knownReviewOrder = [];
  var maxKnownReviewKeys = 80;
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

  function rememberReviewKey(key) {
    if (knownReviewKeys[key]) {
      return false;
    }

    knownReviewKeys[key] = true;
    knownReviewOrder.push(key);

    while (knownReviewOrder.length > maxKnownReviewKeys) {
      var oldest = knownReviewOrder.shift();
      delete knownReviewKeys[oldest];
    }

    return true;
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

  function findAdjacentCount(textNode) {
    var parent = textNode.parentElement;
    if (!parent) {
      return 0;
    }

    var container = parent.parentElement || parent;
    var siblings = container.children || [];
    for (var i = 0; i < siblings.length; i++) {
      var sibling = siblings[i];
      var text = (sibling.textContent || '').trim();
      if (/^\d+$/.test(text)) {
        return window.__umSafeParseInt(text);
      }

      var badge = sibling.querySelector &&
        sibling.querySelector('[aria-label*="unreplied" i], [aria-label*="review" i]');
      if (badge) {
        var badgeText = (badge.textContent || badge.getAttribute('aria-label') || '').trim();
        var match = badgeText.match(/(\d+)/);
        if (match) {
          return window.__umSafeParseInt(match[1]);
        }
      }
    }

    var inline = (parent.textContent || '').match(/(\d+)\s*(?:unreplied|pending|new)/i);
    return inline ? window.__umSafeParseInt(inline[1]) : 0;
  }

  function scanUnrepliedCounts() {
    var totalUnreplied = 0;
    var locationBreakdown = [];

    walkTextNodes(document.body, function (textNode) {
      var value = (textNode.textContent || '').trim();
      if (!value) {
        return;
      }

      if (/unreplied/i.test(value) || /needs reply/i.test(value) || /awaiting response/i.test(value)) {
        var count = findAdjacentCount(textNode);
        if (count > 0) {
          totalUnreplied += count;
          locationBreakdown.push({
            label: value.replace(/\d+/g, '').trim() || 'Location',
            count: count
          });
        }
      }

      if (/^reviews?$/i.test(value) || /manage reviews?/i.test(value)) {
        var reviewCount = findAdjacentCount(textNode);
        if (reviewCount > 0) {
          totalUnreplied += reviewCount;
          locationBreakdown.push({
            label: 'Reviews',
            count: reviewCount
          });
        }
      }
    });

    var ariaNodes = document.querySelectorAll(
      '[aria-label*="review" i], [aria-label*="unreplied" i], [data-review-id]'
    );
    for (var a = 0; a < ariaNodes.length; a++) {
      var aria = ariaNodes[a].getAttribute('aria-label') || '';
      var ariaMatch = aria.match(/(\d+)/);
      if (ariaMatch) {
        totalUnreplied = Math.max(totalUnreplied, window.__umSafeParseInt(ariaMatch[1]));
      }
    }

    return {
      totalUnreplied: totalUnreplied,
      locations: locationBreakdown
    };
  }

  function scanReviewAlerts() {
    var alerts = [];
    var reviewNodes = document.querySelectorAll(
      '[aria-label*="review" i], [role="listitem"], [role="article"], [data-review-id], [data-rating]'
    );

    for (var i = 0; i < reviewNodes.length; i++) {
      var node = reviewNodes[i];
      var text = (node.textContent || '').trim();
      if (!text || text.length < 8) {
        continue;
      }

      if (!/star|rating|review|replied|unreplied|customer/i.test(text)) {
        continue;
      }

      if (/replied|responded|your reply/i.test(text) && !/unreplied|needs reply|awaiting/i.test(text)) {
        continue;
      }

      var ratingMatch = text.match(/(\d)\s*(?:star|★|out of)/i);
      var reviewerMatch = text.match(/^([A-Za-z0-9 .'-]{2,40})/);
      var snippet = text.length > 160 ? text.slice(0, 157) + '...' : text;
      var reviewId = node.getAttribute('data-review-id') ||
        node.getAttribute('id') ||
        (reviewerMatch ? reviewerMatch[1] : 'review') + '|' + snippet.slice(0, 40);

      alerts.push({
        reviewId: reviewId,
        reviewerName: reviewerMatch ? reviewerMatch[1].trim() : 'Customer',
        snippet: snippet,
        rating: ratingMatch ? window.__umSafeParseInt(ratingMatch[1]) : 0,
        locationLabel: 'Google Business'
      });
    }

    return alerts.slice(0, 12);
  }

  // Human-in-the-loop: fills the visible reply field only; never clicks Post/Send.
  window.__umSubmitReviewReply = function (reviewId, replyText) {
    if (!replyText) {
      return false;
    }

    var targets = document.querySelectorAll(
      'textarea, [contenteditable="true"], [role="textbox"], input[type="text"]'
    );

    for (var i = 0; i < targets.length; i++) {
      var target = targets[i];
      if (target.offsetParent === null) {
        continue;
      }

      if (typeof target.value === 'string') {
        target.value = replyText;
        target.dispatchEvent(new Event('input', { bubbles: true }));
      } else {
        target.textContent = replyText;
        target.dispatchEvent(new InputEvent('input', { bubbles: true, data: replyText }));
      }

      target.focus();
      return true;
    }

    return false;
  };

  function publishImmediate() {
    publishScheduled = false;
    var scan = scanUnrepliedCounts();
    var unreplied = scan.totalUnreplied;
    var alerts = scanReviewAlerts();

    if (unreplied !== lastPostedUnreplied) {
      lastPostedUnreplied = unreplied;

      postMessage({
        type: 'badge-count',
        instanceId: INSTANCE_ID,
        platform: PLATFORM,
        count: unreplied
      });

      postMessage({
        type: 'google-review-snapshot',
        instanceId: INSTANCE_ID,
        platform: PLATFORM,
        unrepliedCount: unreplied,
        locations: scan.locations,
        timestampUtc: new Date().toISOString()
      });
    }

    for (var r = 0; r < alerts.length; r++) {
      var alert = alerts[r];
      var key = alert.reviewId + '|' + alert.snippet;
      if (!rememberReviewKey(key)) {
        continue;
      }

      postMessage({
        type: 'google-review-alert',
        instanceId: INSTANCE_ID,
        platform: PLATFORM,
        reviewId: alert.reviewId,
        reviewerName: alert.reviewerName,
        snippet: alert.snippet,
        rating: alert.rating,
        locationLabel: alert.locationLabel,
        timestampUtc: new Date().toISOString()
      });

      var previewTitle = alert.reviewerName + ' · review';
      var normalized = window.__umNormalizePreview(previewTitle, alert.snippet);
      if (window.__umShouldEmitPreview(INSTANCE_ID, normalized.title, normalized.body)) {
        window.__umForwardPreview(INSTANCE_ID, PLATFORM, normalized.title, {
          body: normalized.body
        });
      }
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

  function observeShell() {
    if (!document.body || domObserver) {
      return;
    }

    domObserver = new MutationObserver(function () {
      schedulePublish();
    });

    domObserver.observe(document.body, {
      childList: true,
      subtree: true,
      characterData: true,
      attributes: true,
      attributeFilter: ['aria-label', 'data-review-id', 'class']
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
        lastPostedUnreplied = -1;
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
    }, 20000);
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
    publishScheduled = false;
    lastPostedUnreplied = -1;
    knownReviewKeys = Object.create(null);
    knownReviewOrder = [];
  }

  window.__umAdapterDispose = disposeAdapter;
  if (window.__umRegisterDisposable) {
    window.__umRegisterDisposable(disposeAdapter);
  }

  window.__umInstallNotificationInterceptor(INSTANCE_ID, PLATFORM);
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  hookSpaNavigation();
  observeShell();
  startPolling();

  document.addEventListener('visibilitychange', onVisibilityChange);
})();
