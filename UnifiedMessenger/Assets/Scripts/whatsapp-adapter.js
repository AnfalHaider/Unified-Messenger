(function () {
  'use strict';

  if (window.__unifiedMessengerAdapterInstalled) {
    return;
  }

  window.__unifiedMessengerAdapterInstalled = true;

  var INSTANCE_ID = '__INSTANCE_ID__';
  var PLATFORM = '__PLATFORM__';
  var ADAPTER_ID = 'whatsapp';
  var lastPostedCount = -1;
  var dbCache = null;
  var pollTimer = null;
  var chatSnapshots = Object.create(null);
  var snapshotsInitialized = false;

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function isEligibleChat(chat) {
    if (!chat || chat.unreadCount <= 0 || chat.archive) {
      return false;
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

    var rows = document.querySelectorAll('#pane-side [role="row"], #side [role="row"]');
    for (var i = 0; i < rows.length; i++) {
      var row = rows[i];
      var rowText = row.textContent || '';
      if (rowText.indexOf(chatTitle) < 0) {
        continue;
      }

      var subtitle = row.querySelector('span[dir="auto"]:last-of-type, div._ak8k span, span[data-testid="last-msg-status"]');
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
        body: normalized.body
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
    var badges = document.querySelectorAll('span[data-testid="icon-unread"]');

    badges.forEach(function (badge) {
      total += window.__umSafeParseInt(badge.textContent);
    });

    if (total > 0) {
      return total;
    }

    var chatRows = document.querySelectorAll('#pane-side [role="row"], #side [role="row"]');
    chatRows.forEach(function (row) {
      var badgeSpan = row.querySelector('span[aria-label]');
      if (!badgeSpan) {
        return;
      }

      var label = badgeSpan.getAttribute('aria-label') || '';
      if (/unread/i.test(label)) {
        total += window.__umSafeParseInt(label);
      }
    });

    return total;
  }

  function publishBadgeCount() {
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

  window.__unifiedMessengerPublishBadge = publishBadgeCount;

  function observeDom() {
    var root = document.body || document.documentElement;
    if (!root) {
      return;
    }

    var observer = new MutationObserver(function () {
      publishBadgeCount();
    });

    observer.observe(root, {
      childList: true,
      subtree: true,
      characterData: true,
      attributes: true,
      attributeFilter: ['aria-label', 'data-testid', 'title']
    });
  }

  function startPolling() {
    if (pollTimer) {
      return;
    }

    publishBadgeCount();
    pollTimer = window.setInterval(publishBadgeCount, 2500);
  }

  window.__umInstallNotificationInterceptor(INSTANCE_ID, PLATFORM);
  window.__umInstallOutgoingMessageMonitor(INSTANCE_ID, PLATFORM, {
    composeSelectors: [
      'div[contenteditable="true"][data-tab="10"]',
      'footer div[contenteditable="true"]'
    ],
    sendSelectors: [
      'span[data-testid="send"]',
      'button[aria-label*="Send"]'
    ],
    chatHintSelectors: [
      'header [data-testid="conversation-info-header-chat-title"]',
      'span[data-testid="conversation-info-header-chat-title"]'
    ]
  });
  window.__umPublishReady(INSTANCE_ID, PLATFORM, ADAPTER_ID);
  window.__umStartHeartbeat(INSTANCE_ID, PLATFORM, ADAPTER_ID, 30000);
  observeDom();
  startPolling();

  window.addEventListener('load', publishBadgeCount);
})();
