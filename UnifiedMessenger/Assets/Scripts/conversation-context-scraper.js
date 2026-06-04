(function () {
  'use strict';

  if (window.__umConversationContextInstalled) {
    return;
  }

  window.__umConversationContextInstalled = true;

  var PLATFORM = '__PLATFORM__';

  var profiles = {
    metabusiness: {
      messageSelectors: [
        '[data-testid*="message"]',
        '[role="row"] [dir="auto"]',
        '[aria-label*="Message" i]'
      ],
      outgoingHints: [/you sent/i, /^you$/i, /outgoing/i]
    },
    googlebusiness: {
      messageSelectors: [
        '[data-review-id]',
        '[role="article"]',
        '[aria-label*="review" i]'
      ],
      outgoingHints: [/your reply/i, /replied/i, /owner response/i]
    },
    generic: {
      messageSelectors: ['[role="article"]', '[role="row"]', 'p'],
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

  function collectMessages(profile, maxCount) {
    var limit = Math.max(1, Math.min(maxCount || 4, 12));
    var collected = [];
    var seen = {};

    for (var s = 0; s < profile.messageSelectors.length && collected.length < limit; s++) {
      try {
        var nodes = document.querySelectorAll(profile.messageSelectors[s]);
        for (var n = nodes.length - 1; n >= 0 && collected.length < limit; n--) {
          var node = nodes[n];
          if (!node || node.offsetParent === null) {
            continue;
          }

          var text = normalizeText(node.textContent || node.innerText || '');
          if (text.length < 2 || seen[text]) {
            continue;
          }

          seen[text] = true;
          collected.push({
            text: text,
            direction: isOutgoing(text, profile) ? 'outgoing' : 'incoming'
          });
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] context selector failed', profile.messageSelectors[s], error);
      }
    }

    return collected.reverse();
  }

  window.__umExtractConversationContext = function (maxMessages) {
    var profile = resolveProfile(PLATFORM);
    var messages = collectMessages(profile, maxMessages);
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
      conversationHint: messages.length > 0 ? messages[messages.length - 1].text.slice(0, 120) : ''
    };
  };
})();
