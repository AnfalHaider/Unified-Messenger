(function () {
  'use strict';

  /**
   * Unified Messenger — State-Verification Thread Status Auditor
   *
   * Lightweight background loop that inspects the *last* message in the active
   * conversation viewport. When the newest message transitions to a business /
   * owner outgoing state, broadcasts update-thread-status (RESOLVED) via WebView2.
   *
   * Design notes (2026 DOM drift):
   * - Prefer data-testid / aria-label over hashed CSS classes.
   * - WhatsApp: data-testid="msg-container" + .message-out (stable hallmarks).
   * - Meta Business Suite: volatile DOM — aria-label "You sent" + BizInbox panel.
   * - Google Business Profile: legacy chat ended Jul 2024; audit review owner replies.
   *
   * Performance: scoped MutationObserver + debounced rAF + low-frequency fallback
   * poll so background WebView tabs still converge when timers are throttled.
   */
  if (window.__umThreadStatusAuditorCore) {
    return;
  }

  window.__umThreadStatusAuditorCore = true;

  var DEFAULT_POLL_MS = 4500;
  var OBSERVER_RETRY_MS = 6000;

  var PLATFORM_PROFILES = {
    whatsapp: {
      pollMs: 4000,
      conversationRootSelectors: [
        '[data-testid="conversation-panel-messages"]',
        '#main [role="application"]',
        '#main'
      ],
      messageContainerSelectors: [
        'div[data-testid="msg-container"]'
      ],
      headerSelectors: [
        'header[data-testid="conversation-header"] span[title]',
        '[data-testid="conversation-header"] span[title]',
        '#main header span[title]'
      ],
      isOutgoingMessage: isWhatsAppOutgoing
    },
    whatsappbusiness: {
      pollMs: 4000,
      conversationRootSelectors: [
        '[data-testid="conversation-panel-messages"]',
        '#main [role="application"]',
        '#main'
      ],
      messageContainerSelectors: [
        'div[data-testid="msg-container"]'
      ],
      headerSelectors: [
        'header[data-testid="conversation-header"] span[title]',
        '[data-testid="conversation-header"] span[title]',
        '#main header span[title]'
      ],
      isOutgoingMessage: isWhatsAppOutgoing
    },
    metabusiness: {
      pollMs: 5000,
      conversationRootSelectors: [
        '[data-pagelet="BizInbox"]',
        '[data-pagelet*="inbox" i]',
        '[role="main"]',
        'main'
      ],
      messageContainerSelectors: [
        '[data-testid*="message"]',
        '[role="row"]',
        '[role="article"]'
      ],
      headerSelectors: [
        '[aria-label*="Conversation" i]',
        'header h1',
        'header h2',
        '[role="heading"]'
      ],
      isOutgoingMessage: isMetaOutgoing
    },
    googlebusiness: {
      pollMs: 6000,
      conversationRootSelectors: [
        '[role="main"]',
        'main',
        '[data-review-id]',
        '[role="article"]'
      ],
      messageContainerSelectors: [
        '[data-review-id]',
        '[role="article"]',
        '[aria-label*="review" i]'
      ],
      headerSelectors: [
        '[data-review-id]',
        'h1',
        'h2',
        '[role="heading"]'
      ],
      isOutgoingMessage: isGoogleOwnerReply
    }
  };

  function normalize(value) {
    return String(value || '').replace(/\s+/g, ' ').trim();
  }

  function looksLikeIconLigature(text) {
    var value = normalize(text);
    if (!value || value.length > 48) {
      return false;
    }

    return /^[\uE000-\uF8FF\u200B-\u200D\uFEFF]+$/.test(value) ||
      /^wds-/i.test(value);
  }

  function queryFirst(selectors, root) {
    if (!selectors || !selectors.length) {
      return null;
    }

    var scope = root || document;
    for (var i = 0; i < selectors.length; i++) {
      try {
        var node = scope.querySelector(selectors[i]);
        if (node) {
          return node;
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] auditor selector failed', selectors[i], error);
      }
    }

    return null;
  }

  function queryAll(selectors, root) {
    var scope = root || document;
    for (var i = 0; i < selectors.length; i++) {
      try {
        var nodes = scope.querySelectorAll(selectors[i]);
        if (nodes && nodes.length) {
          return nodes;
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] auditor selector failed', selectors[i], error);
      }
    }

    return null;
  }

  function resolveConversationKey(profile, root) {
    var header = queryFirst(profile.headerSelectors, root);
    var headerTitle = '';
    if (header) {
      var title = header.getAttribute && header.getAttribute('title');
      if (title) {
        headerTitle = normalize(title);
        if (headerTitle && !looksLikeIconLigature(headerTitle)) {
          // fall through to unified resolver below
        } else {
          headerTitle = '';
        }
      }

      if (!headerTitle) {
        var text = normalize(header.textContent || header.innerText || '');
        if (text && !looksLikeIconLigature(text)) {
          headerTitle = text;
        }
      }
    }

    var reviewId = '';
    var reviewNode = queryFirst(['[data-review-id]'], root);
    if (reviewNode) {
      reviewId = reviewNode.getAttribute('data-review-id') || '';
    }

    if (typeof window.__umResolveConversationKey === 'function') {
      return window.__umResolveConversationKey(snapshotPlatform(profile), {
        reviewId: reviewId,
        headerTitle: headerTitle
      });
    }

    if (reviewId) {
      return 'review:' + reviewId;
    }

    return headerTitle || '';
  }

  function snapshotPlatform(profile) {
    if (profile === PLATFORM_PROFILES.metabusiness) {
      return 'metabusiness';
    }

    if (profile === PLATFORM_PROFILES.googlebusiness) {
      return 'googlebusiness';
    }

    if (profile === PLATFORM_PROFILES.whatsappbusiness) {
      return 'whatsappbusiness';
    }

    return 'whatsapp';
  }

  function isWhatsAppOutgoing(container) {
    if (!container) {
      return false;
    }

    if (container.classList && container.classList.contains('message-out')) {
      return true;
    }

    if (container.closest && container.closest('.message-out')) {
      return true;
    }

    var aria = container.getAttribute && container.getAttribute('aria-label');
    if (aria && /you sent|message sent|outgoing/i.test(aria)) {
      return true;
    }

    var dataId = container.getAttribute && container.getAttribute('data-id');
    if (dataId && /^true_/.test(dataId)) {
      return true;
    }

    return false;
  }

  function isMetaOutgoing(container) {
    if (!container) {
      return false;
    }

    var aria = container.getAttribute && container.getAttribute('aria-label');
    if (aria && /you sent|sent by you|your message|outgoing/i.test(aria)) {
      return true;
    }

    if (container.closest) {
      var outgoingAncestor = container.closest('[class*="outgoing" i], [data-testid*="outgoing" i]');
      if (outgoingAncestor) {
        return true;
      }
    }

    var className = container.className ? String(container.className) : '';
    if (/outgoing/i.test(className)) {
      return true;
    }

    var text = normalize(container.textContent || '');
    if (/^you sent\b/i.test(text)) {
      return true;
    }

    return false;
  }

  function isGoogleOwnerReply(container) {
    if (!container) {
      return false;
    }

    var aria = container.getAttribute && container.getAttribute('aria-label');
    if (aria && /your reply|owner response|replied by owner|business reply/i.test(aria)) {
      return true;
    }

    var text = normalize(container.textContent || '');
    if (/your reply|owner response|replied to this review|response from the owner/i.test(text)) {
      return true;
    }

    if (container.querySelector &&
      container.querySelector('[aria-label*="your reply" i], [aria-label*="owner" i]')) {
      return true;
    }

    return false;
  }

  function findConversationRoot(profile) {
    return queryFirst(profile.conversationRootSelectors);
  }

  function inspectActiveThread(profile) {
    var root = findConversationRoot(profile);
    if (!root) {
      return null;
    }

    var containers = queryAll(profile.messageContainerSelectors, root);
    if (!containers || !containers.length) {
      return null;
    }

    var newest = containers[containers.length - 1];
    var conversationKey = resolveConversationKey(profile, root);
    if (!conversationKey) {
      return null;
    }

    return {
      conversationKey: conversationKey,
      customerName: conversationKey.replace(/^review:/, '') || 'Customer',
      isOutgoing: profile.isOutgoingMessage(newest),
      signature: conversationKey + '|' + (profile.isOutgoingMessage(newest) ? 'out' : 'in')
    };
  }

  function broadcastResolved(instanceId, snapshot) {
    window.__umPostMessage({
      type: 'update-thread-status',
      instanceId: instanceId,
      status: 'RESOLVED',
      conversationKey: snapshot.conversationKey,
      customerName: snapshot.customerName,
      source: 'thread-status-auditor',
      timestamp: new Date().toISOString()
    });
  }

  function resolveProfile(platform, overrideProfile) {
    if (overrideProfile) {
      return overrideProfile;
    }

    var key = String(platform || 'generic').toLowerCase();
    return PLATFORM_PROFILES[key] || PLATFORM_PROFILES.whatsapp;
  }

  window.__umInstallThreadStatusAuditor = function (instanceId, platform, overrideProfile) {
    var installKey = instanceId + '|thread-status-auditor';
    window.__umThreadStatusAuditorInstalls = window.__umThreadStatusAuditorInstalls || Object.create(null);
    if (window.__umThreadStatusAuditorInstalls[installKey]) {
      return;
    }

    window.__umThreadStatusAuditorInstalls[installKey] = true;

    var profile = resolveProfile(platform, overrideProfile);
    var threadState = {
      conversationKey: '',
      lastWasOutgoing: false
    };

    var rafScheduled = false;
    var observer = null;
    var pollTimer = null;
    var observerRetryTimer = null;

    function runVerification() {
      rafScheduled = false;

      try {
        var snapshot = inspectActiveThread(profile);
        if (!snapshot || !snapshot.conversationKey) {
          return;
        }

        snapshot.platform = platform;

        if (snapshot.conversationKey !== threadState.conversationKey) {
          threadState.conversationKey = snapshot.conversationKey;
          threadState.lastWasOutgoing = snapshot.isOutgoing;
          return;
        }

        if (snapshot.isOutgoing && !threadState.lastWasOutgoing) {
          broadcastResolved(instanceId, snapshot);
        }

        threadState.lastWasOutgoing = snapshot.isOutgoing;
      } catch (error) {
        console.warn('[UnifiedMessenger] thread-status-auditor verification failed', error);
      }
    }

    function scheduleVerification() {
      if (rafScheduled) {
        return;
      }

      rafScheduled = true;
      if (typeof requestAnimationFrame === 'function') {
        requestAnimationFrame(runVerification);
      } else {
        setTimeout(runVerification, 16);
      }
    }

    function attachScopedObserver() {
      if (observer) {
        observer.disconnect();
        observer = null;
      }

      var root = findConversationRoot(profile);
      if (!root) {
        return false;
      }

      observer = new MutationObserver(function () {
        scheduleVerification();
      });

      observer.observe(root, {
        childList: true,
        subtree: true,
        characterData: true
      });

      return true;
    }

    function ensureObserver() {
      if (attachScopedObserver()) {
        if (observerRetryTimer) {
          clearInterval(observerRetryTimer);
          observerRetryTimer = null;
        }

        return;
      }

      if (!observerRetryTimer) {
        observerRetryTimer = setInterval(function () {
          attachScopedObserver();
        }, OBSERVER_RETRY_MS);
      }
    }

    ensureObserver();
    scheduleVerification();

    pollTimer = setInterval(function () {
      ensureObserver();
      runVerification();
    }, profile.pollMs || DEFAULT_POLL_MS);

    window.__umRegisterDisposable(function () {
      if (observer) {
        observer.disconnect();
        observer = null;
      }

      if (pollTimer) {
        clearInterval(pollTimer);
        pollTimer = null;
      }

      if (observerRetryTimer) {
        clearInterval(observerRetryTimer);
        observerRetryTimer = null;
      }

      delete window.__umThreadStatusAuditorInstalls[installKey];
    });
  };

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';

  if (INSTANCE_ID && INSTANCE_ID.indexOf('__INSTANCE') === -1) {
    window.__umInstallThreadStatusAuditor(INSTANCE_ID, PLATFORM);
  }
})();
