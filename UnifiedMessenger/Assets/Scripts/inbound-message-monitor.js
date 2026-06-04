(function () {
  'use strict';

  if (window.__umInboundMonitorInstalled) {
    return;
  }

  window.__umInboundMonitorInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';

  var profiles = {
    metabusiness: {
      threadSelectors: [
        '[data-testid="inbox_thread_list"] [role="row"]',
        '[aria-label*="Conversation" i][role="row"]',
        '[role="listitem"]'
      ],
      messageSelectors: [
        '[data-testid*="message"]',
        '[role="row"] [dir="auto"]',
        '[aria-label*="Message" i]'
      ],
      outgoingHints: [/you sent/i, /^you$/i, /outgoing/i]
    },
    googlebusiness: {
      threadSelectors: [
        '[data-review-id]',
        '[role="listitem"]',
        '[role="article"]'
      ],
      messageSelectors: [
        '[data-review-id]',
        '[role="article"]',
        '[aria-label*="review" i]'
      ],
      outgoingHints: [/your reply/i, /replied/i, /owner response/i]
    },
    generic: {
      threadSelectors: ['[role="listitem"]', '[role="row"]'],
      messageSelectors: ['[role="article"]', '[role="row"]', 'p'],
      outgoingHints: [/you$/i, /sent/i]
    }
  };

  var debounceMs = 1200;
  var lastSignature = '';
  var lastPostedAt = 0;
  var debounceTimer = null;
  var observer = null;

  function resolveProfile(platform) {
    var key = String(platform || 'generic').toLowerCase();
    return profiles[key] || profiles.generic;
  }

  function normalizeText(value) {
    return String(value || '').replace(/\s+/g, ' ').trim();
  }

  function isOutgoing(text, profile) {
    var normalized = normalizeText(text);
    if (!normalized) {
      return true;
    }

    for (var i = 0; i < profile.outgoingHints.length; i++) {
      if (profile.outgoingHints[i].test(normalized)) {
        return true;
      }
    }

    return false;
  }

  function queryVisibleText(selectors) {
    for (var i = 0; i < selectors.length; i++) {
      try {
        var nodes = document.querySelectorAll(selectors[i]);
        for (var n = nodes.length - 1; n >= 0; n--) {
          var node = nodes[n];
          if (!node || node.offsetParent === null) {
            continue;
          }

          var text = normalizeText(node.textContent || node.innerText || '');
          if (text.length >= 8) {
            return { node: node, text: text };
          }
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] inbound selector failed', selectors[i], error);
      }
    }

    return null;
  }

  function resolveConversationHint(profile) {
    var thread = queryVisibleText(profile.threadSelectors);
    if (!thread) {
      return '';
    }

    return thread.text.length > 80 ? thread.text.slice(0, 77) + '...' : thread.text;
  }

  function resolveInboundMessage(profile) {
    var message = queryVisibleText(profile.messageSelectors);
    if (!message || isOutgoing(message.text, profile)) {
      return null;
    }

    return message;
  }

  function publishSelection() {
    debounceTimer = null;
    var profile = resolveProfile(PLATFORM);
    var inbound = resolveInboundMessage(profile);
    if (!inbound) {
      return;
    }

    var conversationHint = resolveConversationHint(profile);
    var customerName = conversationHint.split(/[·•|-]/)[0].trim() || 'Customer';
    var signature = inbound.text + '|' + conversationHint;
    var now = Date.now();

    if (signature === lastSignature && now - lastPostedAt < 15000) {
      return;
    }

    lastSignature = signature;
    lastPostedAt = now;

    window.__umPostMessage({
      type: 'inbound-message-selected',
      instanceId: INSTANCE_ID,
      platform: PLATFORM,
      messageText: inbound.text,
      customerName: customerName,
      conversationHint: conversationHint,
      timestampUtc: new Date().toISOString()
    });
  }

  function schedulePublish() {
    if (debounceTimer) {
      window.clearTimeout(debounceTimer);
    }

    debounceTimer = window.setTimeout(publishSelection, debounceMs);
  }

  window.__umStartInboundMessageMonitor = function () {
    if (observer) {
      observer.disconnect();
      observer = null;
    }

    schedulePublish();

    observer = new MutationObserver(function () {
      schedulePublish();
    });

    var root = document.documentElement || document.body;
    if (root) {
      observer.observe(root, {
        childList: true,
        subtree: true,
        characterData: true,
        attributes: true
      });
    }

    document.addEventListener('click', schedulePublish, true);

    window.__umRegisterDisposable(function () {
      if (observer) {
        observer.disconnect();
        observer = null;
      }

      document.removeEventListener('click', schedulePublish, true);
      if (debounceTimer) {
        window.clearTimeout(debounceTimer);
        debounceTimer = null;
      }

      delete window.__umInboundMonitorInstalled;
      delete window.__umStartInboundMessageMonitor;
    });
  };
})();
