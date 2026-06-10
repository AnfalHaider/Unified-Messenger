(function () {
  'use strict';

  if (window.__umVoiceMonitorInstalled) {
    return;
  }

  window.__umVoiceMonitorInstalled = true;

  var INSTANCE_ID = __INSTANCE_ID__;
  var PLATFORM = __PLATFORM__;
  var VOICE_ENABLED = __ENABLE_VOICE_NOTES__;
  var MAX_DURATION_SECONDS = __VOICE_NOTE_MAX_SECONDS__;
  var MAX_AUDIO_BYTES = 1572864;

  var debounceMs = 1500;
  var debounceTimer = null;
  var observer = null;
  var processing = false;
  var processedSignatures = Object.create(null);
  var maxSignatureEntries = 64;

  function normalizeText(value) {
    return String(value || '').replace(/\s+/g, ' ').trim();
  }

  function postMessage(payload) {
    window.__umPostMessage(payload);
  }

  function resolveConversationKey(headerTitle) {
    if (typeof window.__umResolvePlatformConversationIdentity === 'function') {
      var identity = window.__umResolvePlatformConversationIdentity(PLATFORM, {
        headerTitle: headerTitle,
        messagePreview: '',
        chatJid: ''
      });
      if (identity && identity.conversationKey) {
        return identity.conversationKey;
      }
    }

    return headerTitle || '';
  }

  function extractHeaderTitle() {
    if (typeof window.__umWhatsAppExtractChatHeader === 'function') {
      var header = window.__umWhatsAppExtractChatHeader();
      if (header && header.title) {
        return header.title;
      }
    }

    var titleNode = document.querySelector(
      'header [data-testid="conversation-info-header-chat-title"], ' +
      'span[data-testid="conversation-info-header-chat-title"], ' +
      '#main header span[title]'
    );

    return titleNode
      ? normalizeText(titleNode.getAttribute('title') || titleNode.textContent || '')
      : '';
  }

  function isIncomingVoiceContainer(node) {
    if (!node || node.nodeType !== 1) {
      return false;
    }

    var container = node.matches('div[data-testid="msg-container"], div.message-in')
      ? node
      : node.closest('div[data-testid="msg-container"], div.message-in');

    if (!container) {
      return false;
    }

    if (!container.classList.contains('message-in') &&
        !container.querySelector('.message-in')) {
      return false;
    }

    var hasVoiceMarker = container.querySelector(
      'audio[src^="blob:"], [data-testid="audio-play"], [data-icon="ptt"], [data-icon="audio-play"], [data-icon="audio-download"]'
    );

    if (!hasVoiceMarker) {
      return false;
    }

    var textNode = container.querySelector('span.selectable-text, span.copyable-text');
    var text = textNode ? normalizeText(textNode.textContent || '') : '';
    return text.length < 4;
  }

  function resolveAudioElement(container) {
    return container.querySelector('audio[src^="blob:"]');
  }

  function parseDurationSeconds(container, audio) {
    if (audio && isFinite(audio.duration) && audio.duration > 0) {
      return Math.round(audio.duration);
    }

    var durationNode = container.querySelector('[data-testid="audio-duration"], span[aria-label*=":"]');
    if (!durationNode) {
      return 0;
    }

    var label = normalizeText(durationNode.getAttribute('aria-label') || durationNode.textContent || '');
    var match = label.match(/(\d{1,2}):(\d{2})/);
    if (!match) {
      return 0;
    }

    return (parseInt(match[1], 10) * 60) + parseInt(match[2], 10);
  }

  function arrayBufferToBase64(buffer) {
    var bytes = new Uint8Array(buffer);
    var chunkSize = 0x8000;
    var binary = '';

    for (var i = 0; i < bytes.length; i += chunkSize) {
      binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
    }

    return btoa(binary);
  }

  function rememberSignature(signature) {
    processedSignatures[signature] = Date.now();
    var keys = Object.keys(processedSignatures);
    if (keys.length <= maxSignatureEntries) {
      return;
    }

    keys.sort(function (a, b) {
      return processedSignatures[a] - processedSignatures[b];
    });

    for (var i = 0; i < keys.length - maxSignatureEntries; i++) {
      delete processedSignatures[keys[i]];
    }
  }

  function fetchVoicePayload(container) {
    return new Promise(function (resolve) {
      var audio = resolveAudioElement(container);
      if (!audio || !audio.src || audio.src.indexOf('blob:') !== 0) {
        resolve(null);
        return;
      }

      var durationSeconds = parseDurationSeconds(container, audio);
      if (durationSeconds > MAX_DURATION_SECONDS) {
        resolve(null);
        return;
      }

      fetch(audio.src)
        .then(function (response) {
          return response.blob();
        })
        .then(function (blob) {
          if (!blob || blob.size <= 0 || blob.size > MAX_AUDIO_BYTES) {
            resolve(null);
            return;
          }

          return blob.arrayBuffer().then(function (buffer) {
            resolve({
              audioBase64: arrayBufferToBase64(buffer),
              mimeType: blob.type || 'audio/ogg',
              durationSeconds: durationSeconds
            });
          });
        })
        .catch(function () {
          resolve(null);
        });
    });
  }

  function publishVoicePayload(container, payload) {
    var headerTitle = extractHeaderTitle();
    var conversationKey = resolveConversationKey(headerTitle);
    if (!conversationKey) {
      return;
    }

    var signature = conversationKey + '|' + payload.audioBase64.slice(0, 48);
    if (processedSignatures[signature]) {
      return;
    }

    rememberSignature(signature);

    var message = {
      type: 'whatsapp-voice-payload',
      instanceId: INSTANCE_ID,
      platform: PLATFORM,
      conversationKey: conversationKey,
      customerName: headerTitle || 'Customer',
      durationSeconds: payload.durationSeconds,
      mimeType: payload.mimeType,
      audioBase64: payload.audioBase64,
      timestampUtc: new Date().toISOString()
    };

    if (typeof window.__umWhatsAppExtractChatHeader === 'function') {
      var header = window.__umWhatsAppExtractChatHeader();
      if (header) {
        message.verifiedBusinessName = header.verifiedBusinessName || '';
        message.profilePhoneNumber = header.profilePhoneNumber || '';
        message.contactPhoneNumber = header.contactPhoneNumber || '';
      }
    }

    if (typeof window.__umWhatsAppScrapeSidebarLabels === 'function') {
      var labels = window.__umWhatsAppScrapeSidebarLabels(headerTitle);
      if (labels && labels.length) {
        message.businessLabels = labels;
      }
    }

    postMessage(message);
  }

  function scanForVoiceNotes() {
    if (!VOICE_ENABLED || processing) {
      return;
    }

    var panel = document.querySelector('[data-testid="conversation-panel-messages"]') ||
      document.querySelector('#main');
    if (!panel) {
      return;
    }

    var containers = panel.querySelectorAll('div[data-testid="msg-container"], div.message-in');
    if (!containers.length) {
      return;
    }

    for (var i = containers.length - 1; i >= 0; i--) {
      var container = containers[i];
      if (!isIncomingVoiceContainer(container)) {
        continue;
      }

      processing = true;
      fetchVoicePayload(container)
        .then(function (payload) {
          if (payload) {
            publishVoicePayload(container, payload);
          }
        })
        .finally(function () {
          processing = false;
        });
      break;
    }
  }

  function scheduleScan() {
    if (!VOICE_ENABLED) {
      return;
    }

    if (debounceTimer) {
      window.clearTimeout(debounceTimer);
    }

    debounceTimer = window.setTimeout(scanForVoiceNotes, debounceMs);
  }

  window.__umInstallVoiceNoteMonitor = function () {
    if (!VOICE_ENABLED) {
      return;
    }

    scheduleScan();

    if (observer) {
      observer.disconnect();
      observer = null;
    }

    var root = document.querySelector('#main') ||
      document.querySelector('[data-testid="conversation-panel-messages"]') ||
      document.documentElement;
    if (!root) {
      return;
    }

    observer = new MutationObserver(function () {
      scheduleScan();
    });

    observer.observe(root, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['src', 'data-testid', 'data-icon', 'class']
    });

    document.addEventListener('click', scheduleScan, true);

    window.__umRegisterDisposable(function () {
      if (observer) {
        observer.disconnect();
        observer = null;
      }

      document.removeEventListener('click', scheduleScan, true);
      if (debounceTimer) {
        window.clearTimeout(debounceTimer);
        debounceTimer = null;
      }

      delete window.__umVoiceMonitorInstalled;
      delete window.__umInstallVoiceNoteMonitor;
    });
  };
})();
