// The messages of one WhatsApp chat, for Suggest a reply: only when the owner presses it, only the chat that is open,
// and only what WhatsApp has already loaded for it. Read from WhatsApp's own in-memory chat, the same place the
// store bridge reads; nothing is clicked, opened or loaded.
//
// window.__umChatMessages(key, max) -> JSON { state, messages: [{ fromMe, text, t }] }, oldest first.
(function () {
  if (window.__umChatMessagesReady) return;
  window.__umChatMessagesReady = true;

  // A photo's `body` is its thumbnail, not words, so media is named and only its caption is text.
  var LABEL = {
    image: '[photo]', video: '[video]', ptt: '[voice message]', audio: '[audio]', document: '[document]',
    sticker: '[sticker]', call_log: '[call]', location: '[location]', vcard: '[contact card]',
  };
  // System lines that nobody wrote: security codes changing, group events, deleted messages.
  var SKIP = { revoked: 1, e2e_notification: 1, notification_template: 1, gp2: 1, protocol: 1, ciphertext: 1 };

  window.__umChatMessages = function (key, max) {
    try {
      if (typeof window.require !== 'function') return JSON.stringify({ state: 'no-store' });
      var chat = window.require('WAWebChatCollection').ChatCollection.get(key);
      if (!chat || !chat.msgs) return JSON.stringify({ state: 'no-chat' });
      var list = typeof chat.msgs.getModelsArray === 'function' ? chat.msgs.getModelsArray() : (chat.msgs._models || []);
      var out = [];
      list.slice(-max).forEach(function (m) {
        try {
          var type = String(m.type || '');
          if (SKIP[type]) return;
          var text = String((type === 'chat' ? m.body : m.caption) || '').replace(/\s+/g, ' ').trim();
          var label = LABEL[type] || '';
          if (!text && !label) return;
          out.push({ fromMe: !!(m.id && m.id.fromMe), text: window.__umTruncate((label ? label + (text ? ' ' : '') : '') + text, 1000), t: m.t || 0 });
        } catch (e) { /* one odd message costs only itself */ }
      });
      return JSON.stringify({ state: 'done', messages: out });
    } catch (e) {
      return JSON.stringify({ state: 'error' });
    }
  };
})();
