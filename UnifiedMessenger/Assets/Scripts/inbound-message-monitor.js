(function () {
  'use strict';

  if (window.__umInboundMonitorInstalled) {
    return;
  }

  window.__umInboundMonitorInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';
  var NOTIFICATIONS_MUTED = __NOTIFICATIONS_MUTED__;

  var noisePatterns = [
    /messages and calls are end-to-end encrypted/i,
    /security code changed/i,
    /you joined using this community/i,
    /^this business uses/i,
    /^hi,? welcome to/i,
    /automated message/i,
    /tap to learn more/i,
    /^message yourself$/i,
    /^waiting for this message/i,
    /^\+\d[\d\s-]{6,}$/,
    /^online$/i,
    /^typing\.\.\.$/i,
    /more_vert/i,
    /flag as inappropriate/i,
    /report review/i,
    /share review/i,
    /copy link/i,
    /write a reply/i,
    /reply publicly/i,
    /sort by/i,
    /filter reviews/i
  ];

  var greetingOnlyPatterns = [
    /^(assalam+u?a?l?a?ikum|salam|salaam|hi|hello|hey|good\s+(morning|afternoon|evening))[\s!.?]*$/i
  ];

  var promoSpamPatterns = [
    /custom foldable promo cards/i,
    /perfect for packaging/i,
    /mini campaign/i,
    /we create custom/i,
    /bulk (order|pricing|discount)/i,
    /limited time offer/i,
    /click here to (buy|order|subscribe)/i,
    /unsubscribe/i,
    /promotional (message|offer)/i
  ];

  var timestampTokenPattern = /^\d{1,2}:\d{2}(\s?[AP]M)?$/i;
  var inlineTimestampPattern = /\b\d{1,2}:\d{2}(\s?[AP]M)?\b/gi;
  var senderPrefixPattern = /^[\w\s.+@]{1,48}:\s*/i;

  var profiles = {
    metabusiness: {
      headerSelectors: [
        '[aria-label*="Conversation" i]',
        'header h1',
        'header h2',
        '[role="heading"]'
      ],
      messageSelectors: [
        '[data-testid*="message"]',
        '[role="row"] [dir="auto"]',
        '[aria-label*="Message" i]'
      ],
      outgoingHints: [/you sent/i, /^you$/i, /outgoing/i]
    },
    googlebusiness: {
      headerSelectors: [
        '[data-review-id]',
        'h1',
        'h2',
        '[role="heading"]'
      ],
      messageSelectors: [
        '[data-review-id] [data-review-text]',
        '[data-review-id] .review-text',
        '[data-review-id] [aria-label*="review" i]'
      ],
      outgoingHints: [/your reply/i, /replied/i, /owner response/i]
    },
    whatsapp: {
      headerSelectors: [
        'header[data-testid="conversation-header"] span[title]',
        '[data-testid="conversation-header"] span[title]',
        '#main header span[title]'
      ],
      messageSelectors: [
        'div.message-in span.selectable-text',
        '[data-testid="conversation-panel-messages"] div.message-in span.selectable-text',
        '[data-testid*="msg-container"] span.selectable-text',
        'span.selectable-text.copyable-text'
      ],
      outgoingHints: [/^you:/i, /^you$/i, /message-out/i, /outgoing/i, /you sent/i]
    },
    whatsappbusiness: {
      headerSelectors: [
        'header[data-testid="conversation-header"] span[title]',
        '[data-testid="conversation-header"] span[title]',
        '#main header span[title]'
      ],
      messageSelectors: [
        'div.message-in span.selectable-text',
        '[data-testid="conversation-panel-messages"] div.message-in span.selectable-text',
        '[data-testid*="msg-container"] span.selectable-text',
        'span.selectable-text.copyable-text'
      ],
      outgoingHints: [/^you:/i, /^you$/i, /message-out/i, /outgoing/i, /you sent/i]
    },
    generic: {
      headerSelectors: ['header h1', 'header h2', '[role="heading"]'],
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

  function stripTimestampTokens(text) {
    if (!text) {
      return '';
    }

    return text.split(/\s+/).filter(function (token) {
      return token && !timestampTokenPattern.test(token);
    }).join(' ');
  }

  function cleanForInference(text) {
    var normalized = normalizeText(text);
    if (!normalized) {
      return '';
    }

    normalized = normalized.replace(inlineTimestampPattern, ' ');
    normalized = normalizeText(normalized);
    normalized = normalized.replace(senderPrefixPattern, '').trim();
    normalized = stripTimestampTokens(normalized);

    if (normalized.length < 2) {
      return '';
    }

    for (var i = 0; i < noisePatterns.length; i++) {
      if (noisePatterns[i].test(normalized)) {
        return '';
      }
    }

    for (var g = 0; g < greetingOnlyPatterns.length; g++) {
      if (greetingOnlyPatterns[g].test(normalized)) {
        return '';
      }
    }

    for (var p = 0; p < promoSpamPatterns.length; p++) {
      if (promoSpamPatterns[p].test(normalized)) {
        return '';
      }
    }

    return normalized;
  }

  function queryFirst(selectors) {
    if (!selectors || !selectors.length) {
      return null;
    }

    for (var i = 0; i < selectors.length; i++) {
      try {
        var node = document.querySelector(selectors[i]);
        if (node) {
          return node;
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] inbound selector failed', selectors[i], error);
      }
    }

    return null;
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

  function resolveGoogleReviewId() {
    var node = queryFirst(['[data-review-id]']);
    if (!node) {
      return '';
    }

    return node.getAttribute('data-review-id') || '';
  }

  function resolveWhatsAppJid(profile) {
    var header = queryFirst(profile.headerSelectors);
    if (header) {
      var dataId = header.getAttribute && header.getAttribute('data-id');
      if (dataId) {
        var match = String(dataId).match(/(\d+@[^_\s]+)/);
        if (match) {
          return match[1];
        }
      }
    }

    var rows = document.querySelectorAll(
      '#pane-side [role="row"][aria-selected="true"], #side [role="row"][aria-selected="true"]'
    );
    for (var i = 0; i < rows.length; i++) {
      var rowId = rows[i].getAttribute && rows[i].getAttribute('data-id');
      if (rowId && rowId.indexOf('@') >= 0) {
        return rowId;
      }
    }

    return '';
  }

  function resolveHeaderTitle(profile) {
    var header = queryFirst(profile.headerSelectors);
    if (!header) {
      return '';
    }

    var title = header.getAttribute && header.getAttribute('title');
    if (title) {
      return normalizeText(title);
    }

    return normalizeText(header.textContent || header.innerText || '');
  }

  function resolveConversationKey(profile, platformKey) {
    var reviewId = platformKey === 'googlebusiness' ? resolveGoogleReviewId() : '';
    var chatJid = (platformKey === 'whatsapp' || platformKey === 'whatsappbusiness')
      ? resolveWhatsAppJid(profile)
      : '';
    var headerTitle = resolveHeaderTitle(profile);

    if (typeof window.__umResolveConversationKey === 'function') {
      return window.__umResolveConversationKey(PLATFORM, {
        reviewId: reviewId,
        chatJid: chatJid,
        headerTitle: headerTitle,
        customerName: headerTitle.split(/[·•|-]/)[0].trim()
      });
    }

    if (reviewId) {
      return 'review:' + reviewId;
    }

    if (chatJid) {
      return chatJid;
    }

    return headerTitle || 'unknown';
  }

  function resolveInboundMessage(profile) {
    var message = queryVisibleText(profile.messageSelectors);
    if (!message || isOutgoing(message.text, profile)) {
      return null;
    }

    return message;
  }

  function publishSelection() {
    if (NOTIFICATIONS_MUTED) {
      return;
    }

    debounceTimer = null;
    var profile = resolveProfile(PLATFORM);
    var platformKey = String(PLATFORM || 'generic').toLowerCase();
    var inbound = resolveInboundMessage(profile);
    if (!inbound) {
      return;
    }

    var cleanedText = cleanForInference(inbound.text);
    if (!cleanedText || cleanedText.length < 8) {
      return;
    }

    var headerTitle = resolveHeaderTitle(profile);
    var identity = typeof window.__umResolvePlatformConversationIdentity === 'function'
      ? window.__umResolvePlatformConversationIdentity(PLATFORM, {
          headerTitle: headerTitle,
          messagePreview: cleanedText,
          reviewId: platformKey === 'googlebusiness' ? resolveGoogleReviewId() : '',
          chatJid: (platformKey === 'whatsapp' || platformKey === 'whatsappbusiness')
            ? resolveWhatsAppJid(profile)
            : ''
        })
      : null;

    var conversationKey = identity
      ? identity.conversationKey
      : resolveConversationKey(profile, platformKey);
    if (platformKey === 'googlebusiness' && conversationKey.indexOf('review:') !== 0) {
      return;
    }

    var customerName = identity
      ? identity.customerName
      : headerTitle.split(/[·•|-]/)[0].trim() ||
        conversationKey.replace(/^review:/, '') ||
        'Customer';
    var signature = cleanedText + '|' + conversationKey;
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
      messageText: cleanedText,
      customerName: customerName,
      conversationKey: conversationKey,
      conversationHint: conversationKey,
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
