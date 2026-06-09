(function () {
  'use strict';

  if (window.__umConversationContextInstalled) {
    return;
  }

  window.__umConversationContextInstalled = true;

  var PLATFORM = __PLATFORM__;

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
    /^typing\.\.\.$/i
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
      messageSelectors: [
        '[data-testid*="message"] [dir="auto"]',
        '[role="row"] [dir="auto"]'
      ],
      incomingContainerSelectors: [
        '[data-testid*="message"]:not([aria-label*="you sent" i])',
        '[role="row"]:not([aria-label*="you sent" i])'
      ],
      outgoingHints: [/you sent/i, /^you$/i, /outgoing/i, /sent by you/i]
    },
    googlebusiness: {
      messageSelectors: [
        '[data-review-id]',
        '[role="article"]'
      ],
      incomingContainerSelectors: [
        '[data-review-id]',
        '[role="article"]:not([aria-label*="your reply" i])'
      ],
      outgoingHints: [/your reply/i, /replied/i, /owner response/i]
    },
    whatsapp: {
      messageSelectors: [
        'div.message-in span.selectable-text',
        'div.message-in span.copyable-text'
      ],
      incomingContainerSelectors: [
        'div.message-in[data-testid="msg-container"]',
        'div.message-in'
      ],
      outgoingHints: [/^you:/i, /^you$/i, /message-out/i, /outgoing/i, /you sent/i]
    },
    whatsappbusiness: {
      messageSelectors: [
        'div.message-in span.selectable-text',
        'div.message-in span.copyable-text'
      ],
      incomingContainerSelectors: [
        'div.message-in[data-testid="msg-container"]',
        'div.message-in'
      ],
      outgoingHints: [/^you:/i, /^you$/i, /message-out/i, /outgoing/i, /you sent/i]
    },
    generic: {
      messageSelectors: ['[role="article"]', '[role="row"] p', 'p'],
      incomingContainerSelectors: ['[role="article"]', '[role="row"]'],
      outgoingHints: [/you$/i, /sent/i]
    }
  };

  function resolveProfile(platform) {
    var key = String(platform || 'generic').toLowerCase();
    return profiles[key] || profiles.generic;
  }

  function normalizeText(value) {
    return String(value || '').replace(/\s+/g, ' ').trim();
  }

  function stripTimestamps(text) {
    var cleaned = String(text || '').replace(inlineTimestampPattern, ' ');
    var tokens = cleaned.split(/\s+/).filter(function (token) {
      return token && !timestampTokenPattern.test(token);
    });
    return normalizeText(tokens.join(' '));
  }

  function stripLeadingGreetings(text) {
    var normalized = normalizeText(text);
    var greetingPrefix = /^(assalam+u?a?l?a?ikum|salam|salaam|hi|hello|hey|good\s+(morning|afternoon|evening))[\s!.?,]*/i;
    while (normalized) {
      var match = normalized.match(greetingPrefix);
      if (!match) {
        break;
      }
      normalized = normalizeText(normalized.slice(match[0].length));
    }
    return normalized;
  }

  function cleanMessageText(text) {
    var normalized = stripTimestamps(normalizeText(text));
    if (!normalized) {
      return '';
    }

    normalized = normalized.replace(senderPrefixPattern, '');
    normalized = stripTimestamps(normalized);
    normalized = stripLeadingGreetings(normalized);

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

    return normalized;
  }

  function isPromoSpam(text) {
    var normalized = normalizeText(text);
    if (!normalized) {
      return false;
    }

    for (var i = 0; i < promoSpamPatterns.length; i++) {
      if (promoSpamPatterns[i].test(normalized)) {
        return true;
      }
    }

    return false;
  }

  function isNoise(text) {
    return !cleanMessageText(text);
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

  function extractTextFromContainer(container, profile) {
    if (!container) {
      return '';
    }

    var textNode = container.querySelector
      ? container.querySelector('span.selectable-text, span.copyable-text, [dir="auto"]')
      : null;
    var raw = textNode
      ? (textNode.textContent || textNode.innerText || '')
      : (container.textContent || container.innerText || '');

    return cleanMessageText(raw);
  }

  function collectIncomingContainers(profile, maxCount) {
    var limit = Math.max(1, Math.min(maxCount || 4, 12));
    var collected = [];
    var seen = {};
    var selectors = profile.incomingContainerSelectors || profile.messageSelectors;

    for (var s = 0; s < selectors.length && collected.length < limit; s++) {
      try {
        var nodes = document.querySelectorAll(selectors[s]);
        for (var n = nodes.length - 1; n >= 0 && collected.length < limit; n--) {
          var node = nodes[n];
          if (!node || node.offsetParent === null) {
            continue;
          }

          if (node.classList && node.classList.contains('message-out')) {
            continue;
          }

          var text = extractTextFromContainer(node, profile);
          if (text.length < 2 || seen[text] || isOutgoing(text, profile)) {
            continue;
          }

          seen[text] = true;
          collected.push({
            text: text,
            direction: 'incoming'
          });
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] context selector failed', selectors[s], error);
      }
    }

    if (collected.length >= limit) {
      return collected.reverse();
    }

    for (var m = 0; m < profile.messageSelectors.length && collected.length < limit; m++) {
      try {
        var messageNodes = document.querySelectorAll(profile.messageSelectors[m]);
        for (var j = messageNodes.length - 1; j >= 0 && collected.length < limit; j--) {
          var messageNode = messageNodes[j];
          if (!messageNode || messageNode.offsetParent === null) {
            continue;
          }

          var parentOutgoing = messageNode.closest && messageNode.closest('.message-out');
          if (parentOutgoing) {
            continue;
          }

          var messageText = cleanMessageText(messageNode.textContent || messageNode.innerText || '');
          if (messageText.length < 2 || seen[messageText]) {
            continue;
          }

          var direction = isOutgoing(messageText, profile) ? 'outgoing' : 'incoming';
          if (direction === 'outgoing') {
            continue;
          }

          seen[messageText] = true;
          collected.push({
            text: messageText,
            direction: direction
          });
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] context fallback selector failed', profile.messageSelectors[m], error);
      }
    }

    return collected.reverse();
  }

  window.__umExtractConversationContext = function (maxMessages) {
    var profile = resolveProfile(PLATFORM);
    var messages = collectIncomingContainers(profile, maxMessages);
    var lastIncoming = null;

    for (var i = messages.length - 1; i >= 0; i--) {
      if (messages[i].direction === 'incoming') {
        lastIncoming = messages[i].text;
        break;
      }
    }

    return {
      ok: messages.length > 0,
      platform: PLATFORM,
      messages: messages,
      lastIncomingMessage: lastIncoming || '',
      conversationHint: lastIncoming ? lastIncoming.slice(0, 120) : '',
      isPromoSpam: isPromoSpam(lastIncoming || '')
    };
  };
})();
