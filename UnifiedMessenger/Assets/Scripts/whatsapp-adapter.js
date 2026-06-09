(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = __INSTANCE_ID__;
  var PLATFORM = __PLATFORM__;
  var ADAPTER_ID = 'whatsapp';
  var lastPostedCount = -1;
  var dbCache = null;
  var pollTimer = null;
  var domObserver = null;
  var publishScheduled = false;
  var chatSnapshots = Object.create(null);
  var snapshotsInitialized = false;
  var lastUrl = location.href;
  var spaNotify = null;
  var historyHooked = false;
  var originalPushState = null;
  var originalReplaceState = null;

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function isEligibleChat(chat) {
    if (!chat || chat.unreadCount <= 0 || chat.archive) {
      return false;
    }

    if (window.__umShouldIncludeMutedBadges && window.__umShouldIncludeMutedBadges()) {
      return true;
    }

    if (window.__umIncludeMutedBadges) {
      return true;
    }

    return chat.muteExpiration === 0 && !chat.isAutoMuted;
  }

  function getChatKey(chat) {
    if (!chat.id) {
      return null;
    }

    if (typeof chat.id === 'string') {
      return chat.id;
    }

    if (chat.id._serialized) {
      return chat.id._serialized;
    }

    try {
      return JSON.stringify(chat.id);
    } catch (error) {
      return null;
    }
  }

  function getChatTitle(chat) {
    return chat.name ||
      chat.formattedTitle ||
      (chat.contact && chat.contact.name) ||
      'New message';
  }

  function getChatPreviewBody(chat, delta) {
    if (chat.lastMessage && typeof chat.lastMessage.body === 'string' && chat.lastMessage.body) {
      return chat.lastMessage.body;
    }

    if (chat.lastMessage && typeof chat.lastMessage.text === 'string' && chat.lastMessage.text) {
      return chat.lastMessage.text;
    }

    var domPreview = getPreviewFromDom(getChatTitle(chat));
    if (domPreview) {
      return domPreview;
    }

    return delta === 1 ? 'New message' : delta + ' new messages';
  }

  function getPreviewFromDom(chatTitle) {
    if (!chatTitle) {
      return '';
    }

    var rows = document.querySelectorAll(
      '#pane-side [role="row"], #side [role="row"], [data-testid="chat-list"] [role="row"]'
    );
    for (var i = 0; i < rows.length; i++) {
      var row = rows[i];
      var rowText = row.textContent || '';
      if (rowText.indexOf(chatTitle) < 0) {
        continue;
      }

      var subtitle = row.querySelector(
        'span[dir="auto"]:last-of-type, div._ak8k span, span[data-testid="last-msg-status"], span[data-testid="last-msg-text"]'
      );
      if (subtitle && subtitle.textContent) {
        return subtitle.textContent.trim();
      }
    }

    return '';
  }

  function scanForNewPreviews(chats) {
    for (var i = 0; i < chats.length; i++) {
      var chat = chats[i];
      if (!isEligibleChat(chat)) {
        continue;
      }

      var chatKey = getChatKey(chat);
      if (!chatKey) {
        continue;
      }

      var previousUnread = chatSnapshots[chatKey];
      chatSnapshots[chatKey] = chat.unreadCount;

      if (!snapshotsInitialized) {
        continue;
      }

      if (previousUnread === undefined || chat.unreadCount <= previousUnread) {
        continue;
      }

      var delta = chat.unreadCount - (previousUnread || 0);
      var title = getChatTitle(chat);
      var body = getChatPreviewBody(chat, delta);
      var normalized = window.__umNormalizePreview(title, body);

      if (!window.__umShouldEmitPreview(INSTANCE_ID, normalized.title, normalized.body)) {
        continue;
      }

      postMessage({
        type: 'notification-preview',
        instanceId: INSTANCE_ID,
        platform: PLATFORM,
        title: normalized.title,
        body: normalized.body,
        conversationKey: chatKey,
        customerName: getChatTitle(chat)
      });
    }

    snapshotsInitialized = true;
  }

  function readChatsFromIndexedDb(callback) {
    if (!window.indexedDB) {
      callback(null);
      return;
    }

    function readFromDb(db) {
      try {
        if (!db.objectStoreNames.contains('chat')) {
          callback(null);
          return;
        }

        var unreadCount = 0;
        var txn = db.transaction('chat', 'readonly');
        var store = txn.objectStore('chat');
        var query = store.getAll();

        query.onsuccess = function (event) {
          var chats = event.target.result || [];
          for (var j = 0; j < chats.length; j++) {
            if (isEligibleChat(chats[j])) {
              unreadCount += chats[j].unreadCount;
            }
          }

          callback({ unreadCount: unreadCount, chats: chats });
        };

        query.onerror = function () {
          callback(null);
        };
      } catch (error) {
        callback(null);
      }
    }

    if (dbCache) {
      readFromDb(dbCache);
      return;
    }

    if (!indexedDB.databases) {
      callback(null);
      return;
    }

    indexedDB.databases().then(function (databases) {
      var hasModelStorage = databases.some(function (db) {
        return db.name === 'model-storage';
      });

      if (!hasModelStorage) {
        callback(null);
        return;
      }

      var request = indexedDB.open('model-storage');
      request.onsuccess = function () {
        dbCache = request.result;
        dbCache.onversionchange = function () {
          dbCache.close();
          dbCache = null;
          snapshotsInitialized = false;
          chatSnapshots = Object.create(null);
        };
        readFromDb(dbCache);
      };
      request.onerror = function () {
        callback(null);
      };
    }).catch(function () {
      callback(null);
    });
  }

  function countFromDomBadges() {
    var total = 0;
    var selectors = [
      'span[data-testid="icon-unread"]',
      'span[data-testid="unread-count"]',
      'span[aria-label*="unread"]'
    ];

    selectors.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (badge) {
        if (window.__umIsDomBadgeMuted && window.__umIsDomBadgeMuted(badge)) {
          return;
        }

        var label = badge.getAttribute('aria-label') || '';
        total += /unread/i.test(label)
          ? window.__umSafeParseInt(label)
          : window.__umSafeParseInt(badge.textContent);
      });
    });

    if (total > 0) {
      return total;
    }

    var chatRows = document.querySelectorAll(
      '#pane-side [role="row"], #side [role="row"], [data-testid="chat-list"] [role="row"]'
    );
    chatRows.forEach(function (row) {
      if (window.__umIsDomBadgeMuted && window.__umIsDomBadgeMuted(row)) {
        return;
      }

      var badgeSpan = row.querySelector('span[aria-label*="unread"], span[data-testid="icon-unread"]');
      if (!badgeSpan) {
        return;
      }

      var label = badgeSpan.getAttribute('aria-label') || badgeSpan.textContent || '';
      if (/unread/i.test(label)) {
        total += window.__umSafeParseInt(label);
      }
    });

    return total;
  }

  function publishBadgeCountImmediate() {
    publishScheduled = false;

    readChatsFromIndexedDb(function (result) {
      if (result && result.chats) {
        scanForNewPreviews(result.chats);

        var count = result.unreadCount;
        if (count !== lastPostedCount) {
          lastPostedCount = count;
          postMessage({
            type: 'badge-count',
            instanceId: INSTANCE_ID,
            platform: PLATFORM,
            count: count
          });
        }

        return;
      }

      var domCount = countFromDomBadges();
      var count = domCount > 0 ? domCount : window.__umCountFromTitle();
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
    });
  }

  function schedulePublishBadgeCount() {
    if (publishScheduled) {
      return;
    }

    publishScheduled = true;
    window.setTimeout(publishBadgeCountImmediate, 150);
  }

  window.__unifiedMessengerPublishBadge = publishBadgeCountImmediate;

  function observeDom() {
    var root = document.body || document.documentElement;
    if (!root || domObserver) {
      return;
    }

    domObserver = new MutationObserver(function () {
      schedulePublishBadgeCount();
    });

    domObserver.observe(root, {
      childList: true,
      subtree: true,
      characterData: true,
      attributes: true,
      attributeFilter: ['aria-label', 'data-testid', 'title', 'class']
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
        lastPostedCount = -1;
        snapshotsInitialized = false;
      }

      schedulePublishBadgeCount();
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
      publishBadgeCountImmediate();
    }
  }

  function startPolling() {
    if (pollTimer) {
      return;
    }

    publishBadgeCountImmediate();
    pollTimer = window.setInterval(function () {
      if (!document.hidden) {
        publishBadgeCountImmediate();
      }
    }, 5000);
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
    window.removeEventListener('load', publishBadgeCountImmediate);
    publishScheduled = false;

    if (dbCache) {
      try {
        dbCache.close();
      } catch (error) {
        // ignore
      }

      dbCache = null;
    }

    chatSnapshots = Object.create(null);
    snapshotsInitialized = false;
    lastPostedCount = -1;
  }

  function resolveLastMessageTimestamp(chat) {
    if (chat && chat.lastMessage && chat.lastMessage.timestamp) {
      try {
        return new Date(chat.lastMessage.timestamp * 1000).toISOString();
      } catch (error) {
        return null;
      }
    }

    if (chat && chat.t) {
      try {
        return new Date(chat.t * 1000).toISOString();
      } catch (error) {
        return null;
      }
    }

    return null;
  }

  window.__umCollectBackfillCandidates = function (maxChats) {
    maxChats = maxChats || 20;

    return new Promise(function (resolve) {
      readChatsFromIndexedDb(function (result) {
        if (!result || !result.chats) {
          resolve({ ok: false, candidates: [] });
          return;
        }

        var candidates = [];
        for (var i = 0; i < result.chats.length && candidates.length < maxChats; i++) {
          var chat = result.chats[i];
          if (!isEligibleChat(chat)) {
            continue;
          }

          var chatKey = getChatKey(chat);
          if (!chatKey) {
            continue;
          }

          var title = getChatTitle(chat);
          var body = getChatPreviewBody(chat, chat.unreadCount);
          if (!body || body.length < 8) {
            continue;
          }

          candidates.push({
            chatKey: chatKey,
            title: title,
            lastMessageBody: body,
            lastMessageTimestamp: resolveLastMessageTimestamp(chat),
            unreadCount: chat.unreadCount
          });
        }

        resolve({ ok: candidates.length > 0, candidates: candidates });
      });
    });
  };

  window.__umCommitInboundBaseline = function () {
    readChatsFromIndexedDb(function (result) {
      if (result && result.chats) {
        for (var i = 0; i < result.chats.length; i++) {
          var chat = result.chats[i];
          var chatKey = getChatKey(chat);
          if (!chatKey) {
            continue;
          }

          chatSnapshots[chatKey] = chat.unreadCount;
        }
      }

      snapshotsInitialized = true;
    });

    return { ok: true };
  };

  window.__umAdapterDispose = disposeAdapter;
  if (window.__umRegisterDisposable) {
    window.__umRegisterDisposable(disposeAdapter);
  }

  window.__umInstallNotificationInterceptor(INSTANCE_ID, PLATFORM);
  window.__umInstallOutgoingMessageMonitor(INSTANCE_ID, PLATFORM, {
    composeSelectors: [
      'div[contenteditable="true"][data-tab="10"]',
      'footer div[contenteditable="true"]',
      'div[contenteditable="true"][data-lexical-editor="true"]'
    ],
    sendSelectors: [
      'span[data-testid="send"]',
      'button[aria-label*="Send"]',
      'button[data-testid="compose-btn-send"]'
    ],
    chatHintSelectors: [
      'header [data-testid="conversation-info-header-chat-title"]',
      'span[data-testid="conversation-info-header-chat-title"]',
      'header span[dir="auto"]'
    ]
  });
  window.__umInstallOutgoingDomReplyMonitor(INSTANCE_ID, PLATFORM, {
    conversationPanelSelectors: [
      '[data-testid="conversation-panel-messages"]',
      '#main'
    ],
    outgoingMessageSelectors: [
      'div.message-out',
      '[data-testid*="msg-out"]'
    ],
    chatHintSelectors: [
      'header [data-testid="conversation-info-header-chat-title"]',
      'span[data-testid="conversation-info-header-chat-title"]',
      'header span[dir="auto"]'
    ]
  });
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  hookSpaNavigation();
  observeDom();
  startPolling();

  document.addEventListener('visibilitychange', onVisibilityChange);
  window.addEventListener('load', publishBadgeCountImmediate);
})();
