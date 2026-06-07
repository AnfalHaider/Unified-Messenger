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
  var VIEW_CONTEXT = {
    Unknown: 'unknown',
    LocationsDirectory: 'locations-directory',
    DeepData: 'deep-data'
  };
  var lastViewContext = VIEW_CONTEXT.Unknown;
  var navigationCooldownUntil = 0;
  var NAVIGATION_COOLDOWN_MS = 12000;

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

  function parseCountFromAriaLabel(label) {
    if (!label) {
      return 0;
    }

    var unreplied = label.match(/(\d+)\s*(?:unreplied|pending|need(?:s)?\s+(?:a\s+)?reply)/i);
    if (unreplied) {
      return window.__umSafeParseInt(unreplied[1]);
    }

    var generic = label.match(/(\d+)/);
    return generic ? window.__umSafeParseInt(generic[1]) : 0;
  }

  function scanUnrepliedCounts() {
    var totalUnreplied = 0;
    var locationBreakdown = [];
    var seenLabels = Object.create(null);

    var ariaNodes = document.querySelectorAll(
      '[aria-label*="review" i], [aria-label*="unreplied" i], [aria-label*="reply" i], [data-review-id]'
    );
    for (var a = 0; a < ariaNodes.length; a++) {
      var ariaLabel = ariaNodes[a].getAttribute('aria-label') || '';
      var ariaCount = parseCountFromAriaLabel(ariaLabel);
      if (ariaCount > 0) {
        totalUnreplied = Math.max(totalUnreplied, ariaCount);
      }
    }

    walkTextNodes(document.body, function (textNode) {
      var value = (textNode.textContent || '').trim();
      if (!value) {
        return;
      }

      if (/unreplied/i.test(value) || /needs reply/i.test(value) || /awaiting response/i.test(value)) {
        var count = findAdjacentCount(textNode);
        if (count > 0) {
          totalUnreplied = Math.max(totalUnreplied, count);
          var label = value.replace(/\d+/g, '').trim() || 'Location';
          if (!seenLabels[label]) {
            seenLabels[label] = true;
            locationBreakdown.push({ label: label, count: count });
          }
        }
      }

      if (/^reviews?$/i.test(value) || /manage reviews?/i.test(value)) {
        var reviewCount = findAdjacentCount(textNode);
        if (reviewCount > 0) {
          totalUnreplied = Math.max(totalUnreplied, reviewCount);
          if (!seenLabels.Reviews) {
            seenLabels.Reviews = true;
            locationBreakdown.push({ label: 'Reviews', count: reviewCount });
          }
        }
      }
    });

    var ratingMatch = window.__umFindTextMatch &&
      window.__umFindTextMatch(/(\d+(?:\.\d+)?)\s*(?:stars?|★)\s*(?:average|overall)?/i);
    if (ratingMatch) {
      locationBreakdown.push({
        label: 'Aggregate rating',
        count: window.__umSafeParseInt(ratingMatch[1])
      });
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
      var reviewId = node.getAttribute('data-review-id');
      if (!reviewId) {
        continue;
      }

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

  function isLocationsDirectoryView() {
    try {
      var href = (location.href || '').toLowerCase();
      var pathMatch =
        /business\.google\.com\/locations\b/.test(href) ||
        /\/locations\/?($|\?|#)/.test(href) ||
        /businessprofilemanager.*\/locations\b/.test(href);

      if (!pathMatch) {
        return false;
      }

      var hasLocationCards =
        document.querySelectorAll(
          '[data-location-id], [data-merchant-id], a[href*="/location/"], a[href*="/reviews"], [role="row"]'
        ).length > 0;

      var bodyText = document.body ? document.body.innerText || '' : '';
      var hasDeepReviewDom =
        document.querySelectorAll(
          '[data-review-id], [aria-label*="unreplied" i][aria-label*="review" i], [role="article"]'
        ).length > 0 &&
        /unreplied|needs reply|awaiting/i.test(bodyText);

      return hasLocationCards && !hasDeepReviewDom;
    } catch (error) {
      console.warn('[UnifiedMessenger] locations directory detection failed', error);
      return false;
    }
  }

  function isDeepDataView() {
    try {
      var href = (location.href || '').toLowerCase();
      if (/business\.google\.com\/(reviews|messaging)\b/.test(href)) {
        return true;
      }

      var unrepliedScan = scanUnrepliedCounts();
      if (unrepliedScan && ((unrepliedScan.totalUnreplied || 0) > 0 || (unrepliedScan.locations || []).length > 0)) {
        return true;
      }

      return scanReviewAlerts().length > 0;
    } catch (error) {
      console.warn('[UnifiedMessenger] deep data detection failed', error);
      return false;
    }
  }

  function resolveViewContext() {
    if (isDeepDataView()) {
      return VIEW_CONTEXT.DeepData;
    }

    if (isLocationsDirectoryView()) {
      return VIEW_CONTEXT.LocationsDirectory;
    }

    return VIEW_CONTEXT.Unknown;
  }

  function publishViewContextStatus(viewState, detail) {
    postMessage({
      type: 'dashboard-scrape-status',
      instanceId: INSTANCE_ID,
      platform: PLATFORM,
      success: true,
      context: 'google-view-context',
      viewState: viewState || VIEW_CONTEXT.Unknown,
      detail: detail || '',
      timestampUtc: new Date().toISOString()
    });
  }

  function tryNavigateFromLocationsDirectory() {
    if (Date.now() < navigationCooldownUntil) {
      return false;
    }

    navigationCooldownUntil = Date.now() + NAVIGATION_COOLDOWN_MS;

    var candidates = [
      'a[href*="/reviews" i]',
      'a[aria-label*="review" i]',
      'a[aria-label*="See your profile" i]',
      'button[aria-label*="See your profile" i]',
      'a[href*="business.google.com"][href*="review"]',
      '[role="link"][href*="/messaging" i]',
      'a[href*="/messaging" i]'
    ];

    for (var i = 0; i < candidates.length; i++) {
      var node = window.__umQueryVisible
        ? window.__umQueryVisible(candidates[i])
        : document.querySelector(candidates[i]);

      if (!node || typeof node.click !== 'function') {
        continue;
      }

      try {
        node.click();
        publishViewContextStatus(
          VIEW_CONTEXT.LocationsDirectory,
          'Connected · awaiting view context'
        );
        return true;
      } catch (clickError) {
        console.warn('[UnifiedMessenger] safe navigation click failed', clickError);
      }
    }

    return false;
  }

  function ensureScrapeViewContext() {
    var context = resolveViewContext();
    lastViewContext = context;

    if (context === VIEW_CONTEXT.LocationsDirectory) {
      publishViewContextStatus(
        VIEW_CONTEXT.LocationsDirectory,
        'Connected · awaiting view context'
      );
      tryNavigateFromLocationsDirectory();
      return false;
    }

    if (context === VIEW_CONTEXT.DeepData) {
      publishViewContextStatus(VIEW_CONTEXT.DeepData, '');
      return true;
    }

    publishViewContextStatus(VIEW_CONTEXT.Unknown, 'Connected · awaiting view context');
    return true;
  }

  function publishImmediate() {
    publishScheduled = false;

    if (!ensureScrapeViewContext()) {
      return;
    }

    window.__umRunSafeScrape(INSTANCE_ID, PLATFORM, 'google-dashboard', function () {
      var scan = scanUnrepliedCounts() || { totalUnreplied: 0, locations: [] };
      var unreplied = scan.totalUnreplied || 0;
      var alerts = scanReviewAlerts() || [];

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
          locations: scan.locations || [],
          timestampUtc: new Date().toISOString()
        });
      } else {
        postMessage({
          type: 'google-review-snapshot',
          instanceId: INSTANCE_ID,
          platform: PLATFORM,
          unrepliedCount: unreplied,
          locations: scan.locations || [],
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
        lastViewContext = VIEW_CONTEXT.Unknown;
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
