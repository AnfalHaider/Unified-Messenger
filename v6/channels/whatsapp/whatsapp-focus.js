(function () {
  'use strict';

  // Go to one WhatsApp conversation, for the owner's own "Open chat". The row click and its rules are v5's
  // (adapter-core.js __umFocusConversation, ConversationFocusHelper.cs); the store path replaces v5's search.
  //
  // Each call takes ONE step and says where it got to, because main calls it repeatedly while the page loads:
  //   done       the open chat's header is this customer
  //   working    asked the page to open it, and the page is catching up
  //   not-found  the chat is neither on screen nor in WhatsApp's store
  //   no-target  nothing to recognise the customer by (no real name and no phone number)
  //
  // Measured on the owner's live pages, 2026-09-13, and not to be "simplified":
  //   - A chat row ignores element.click(). It needs the pointer and mouse sequence a real mouse produces,
  //     dispatched on the title span, not the row (v5 clicked the right row for weeks and nothing opened).
  //   - Typing into the chat-list search no longer filters: setting the <input>'s value and firing "input", which
  //     worked in v5, left the list unchanged. So a chat that is not on screen is opened through WhatsApp's own
  //     command, Cmd.openChatBottom({ chat }), with the chat taken from its ChatCollection by key. That is the
  //     action a click on the row performs, and it reaches every chat in the store, not only the 20 drawn.
  //   - Only a row whose title is exactly the customer's name, or carries their number, is clicked; a substring
  //     would open "Alina" for "Ali". Arrival is judged by the open chat's header, never by the click.
  //   - Nothing here types into any field. It opens a conversation; it never touches the message box.

  if (window.__umWhatsAppFocusInstalled) return;
  window.__umWhatsAppFocusInstalled = true;

  var ROWS = ['#pane-side [role="row"]', '[data-testid="chat-list"] [role="row"]', '#side [role="row"]'];
  var ROW_TITLE = ['[data-testid="cell-frame-title"] span[title]', 'span[title]'];
  // Index 1 matched on the live page; the others are v5's candidates for other builds.
  var HEADER = ['#main header span[title]', '#main header [data-testid="conversation-info-header"] span', '#main header span[dir="auto"]', '[data-testid="conversation-header"] span[title]'];
  var COMMAND_EVERY_MS = 3000;

  function all(candidates) {
    for (var i = 0; i < candidates.length; i++) {
      try {
        var found = document.querySelectorAll(candidates[i]);
        if (found.length) return Array.prototype.slice.call(found);
      } catch (e) { /* a bad selector costs itself, not the step */ }
    }
    return [];
  }
  function first(candidates, root) {
    for (var i = 0; i < candidates.length; i++) {
      try {
        var el = (root || document).querySelector(candidates[i]);
        if (el && (el.getAttribute('title') || el.textContent)) return el;
      } catch (e) { /* next candidate */ }
    }
    return null;
  }
  var collapse = function (s) { return String(s || '').replace(/\s+/g, ' ').trim(); };
  var digits = function (s) { return String(s || '').replace(/\D/g, ''); };
  var titleText = function (el) { return el ? (el.getAttribute('title') || el.textContent || '') : ''; };

  function realClick(el) {
    try { el.scrollIntoView({ block: 'center' }); } catch (e) { /* not fatal */ }
    var base = { bubbles: true, cancelable: true, view: window, button: 0, detail: 1 };
    var seq = [['pointerdown', 1], ['mousedown', 1], ['pointerup', 0], ['mouseup', 0], ['click', 0]];
    for (var i = 0; i < seq.length; i++) {
      var opts = Object.assign({}, base, { buttons: seq[i][1] });
      var ev = seq[i][0].indexOf('pointer') === 0 && typeof window.PointerEvent === 'function'
        ? new PointerEvent(seq[i][0], Object.assign({ pointerId: 1, isPrimary: true, pointerType: 'mouse' }, opts))
        : new MouseEvent(seq[i][0], opts);
      el.dispatchEvent(ev);
    }
  }

  function targetOf(key, name, phone) {
    var bare = String(key || '').toLowerCase();
    var at = bare.indexOf('@');
    var cleanName = collapse(name);
    // A real name has letters and is not WhatsApp's placeholder; an unsaved contact's "name" is its number.
    var hasName = !!cleanName && cleanName.toLowerCase() !== 'new message' && /\p{L}/u.test(cleanName);
    var number = digits(phone);
    if (number.length < 10 && !hasName && digits(cleanName).length >= 10) number = digits(cleanName);
    // A bare @lid id is not a phone number; matching on it once opened an unrelated chat.
    if (!number && bare.indexOf('@lid') < 0 && at > 0) number = digits(bare.slice(0, at));
    return { name: hasName ? cleanName.toLowerCase() : '', number: number.length >= 8 ? number : '' };
  }

  /** Whether a row or header title is this customer: exactly the same name, or the same phone digits. */
  function matches(title, t) {
    var text = collapse(title).toLowerCase();
    if (!text) return false;
    var d = digits(title);
    if (t.number && d.length >= 8 && (d.indexOf(t.number) >= 0 || t.number.indexOf(d) >= 0)) return true;
    return !!t.name && text === t.name;
  }

  /** WhatsApp's own open-chat command, for a chat in its store. Null when this build does not have it. */
  function openFromStore(key) {
    if (typeof window.require !== 'function') return null;
    try {
      var chat = window.require('WAWebChatCollection').ChatCollection.get(key);
      var cmd = window.require('WAWebCmd').Cmd;
      if (!cmd || typeof cmd.openChatBottom !== 'function') return null;
      if (!chat) return false;
      cmd.openChatBottom({ chat: chat });
      return true;
    } catch (e) {
      return null;
    }
  }

  window.__umFocusWhatsApp = function (key, name, phone) {
    try {
      var t = targetOf(key, name, phone);
      if (!t.name && !t.number) return 'no-target';

      if (document.querySelector('#main') && matches(titleText(first(HEADER)), t)) return 'done';

      var rows = all(ROWS);
      for (var i = 0; i < rows.length; i++) {
        var el = first(ROW_TITLE, rows[i]);
        if (el && matches(titleText(el), t)) { realClick(el); return 'working'; }
      }

      // Not on screen. Ask WhatsApp to open it, but not on every step: the command is asynchronous.
      var last = window.__umFocusCommand;
      if (last && last.key === key && Date.now() - last.at < COMMAND_EVERY_MS) return 'working';
      var opened = openFromStore(key);
      window.__umFocusCommand = { key: key, at: Date.now() };
      return opened === false ? 'not-found' : 'working';
    } catch (e) {
      return 'working';
    }
  };
})();
