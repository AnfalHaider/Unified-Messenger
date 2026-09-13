(function () {
  'use strict';

  // Go to one WhatsApp conversation, for the owner's own "Open chat". Ported from v5's adapter-core.js
  // (__umFocusConversation) and ConversationFocusHelper.cs, with their selector candidates.
  //
  // Each call takes ONE step and says where it got to, because main calls it repeatedly while the page loads:
  //   done       the open chat's header is this customer (and the search box is cleared again)
  //   working    clicked a verified row, or typed the search, and the page is catching up
  //   not-found  the search has results and none of them is this customer
  //   no-target  nothing to search for (no real name and no phone number)
  //
  // Hard-won rules from v5, not to be "simplified":
  //   - A chat row ignores element.click(). It needs the pointer and mouse sequence a real mouse produces,
  //     dispatched on the title span, not the row: v5 clicked the right row for weeks and nothing opened.
  //   - Only a row whose visible title matches the customer's phone digits or real name is ever clicked. A
  //     search also matches message text, so "the top result" can be somebody else's chat.
  //   - Never search a bare @lid id: it is not a phone number and once opened an unrelated chat.
  //   - Apply the search once and keep it while results render; clearing and re-typing between attempts made
  //     focus depend on which retry the asynchronous filter happened to land on.
  //   - The only field written to is the chat-list search box in #side. Never the message composer.

  if (window.__umWhatsAppFocusInstalled) return;
  window.__umWhatsAppFocusInstalled = true;

  var ROWS = ['#pane-side [role="row"]', '[data-testid="chat-list"] [role="row"]', '#side [role="row"]'];
  var ROW_TITLE = ['[data-testid="cell-frame-title"] span[title]', 'span[title]'];
  var SEARCH = ['#side input[aria-label="Search or start a new chat"]', 'input[aria-label="Search or start a new chat"]', '#side input[role="textbox"]', '#side input[type="text"]'];
  var HEADER = ['#main header span[title]', '#main header [data-testid="conversation-info-header"] span', '#main header span[dir="auto"]', '[data-testid="conversation-header"] span[title]'];

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
        if (el) return el;
      } catch (e) { /* next candidate */ }
    }
    return null;
  }
  var collapse = function (s) { return String(s || '').replace(/\s+/g, ' ').trim(); };
  var digits = function (s) { return String(s || '').replace(/\D/g, ''); };

  function setSearch(input, value) {
    var desc = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
    if (desc && desc.set) desc.set.call(input, value); else input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }

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
    var isLid = bare.indexOf('@lid') >= 0;
    var at = bare.indexOf('@');
    var id = at > 0 ? bare.slice(0, at) : bare;
    var cleanName = collapse(name);
    // A real name has letters and is not WhatsApp's placeholder; an unsaved contact's "name" is its number.
    var hasName = !!cleanName && cleanName.toLowerCase() !== 'new message' && /\p{L}/u.test(cleanName);
    var number = digits(phone);
    if (number.length < 10 && !hasName && digits(cleanName).length >= 10) number = digits(cleanName);
    if (!number && !isLid) number = digits(id);
    return { name: hasName ? cleanName.toLowerCase() : '', number: number.length >= 8 ? number : '' };
  }

  /** Whether a row or header title is this customer: the same phone digits, or exactly the same name. A name is
   *  compared whole, because a substring would open "Alina" for "Ali". */
  function matches(title, t) {
    var text = collapse(title).toLowerCase();
    if (!text) return false;
    var d = digits(title);
    if (t.number && d.length >= 8 && (d.indexOf(t.number) >= 0 || t.number.indexOf(d) >= 0)) return true;
    return !!t.name && text === t.name;
  }

  function titleEl(row) { return first(ROW_TITLE, row); }
  function titleText(el) { return el ? (el.getAttribute('title') || el.textContent || '') : ''; }

  window.__umFocusWhatsApp = function (key, name, phone) {
    try {
      var t = targetOf(key, name, phone);
      if (!t.name && !t.number) return 'no-target';
      var search = first(SEARCH);

      // Arrived: the open chat is this customer. Put the list back the way the owner left it.
      var header = document.querySelector('#main') ? first(HEADER) : null;
      if (header && matches(titleText(header), t)) {
        if (search && search.value) setSearch(search, '');
        return 'done';
      }

      // A row already on screen.
      var rows = all(ROWS);
      for (var i = 0; i < rows.length; i++) {
        var el = titleEl(rows[i]);
        if (el && matches(titleText(el), t)) { realClick(el); return 'working'; }
      }

      // Search: by number when there is one (WhatsApp finds a saved contact by it too), otherwise by name.
      if (!search) return 'working';
      var term = t.number || collapse(name);
      if (collapse(search.value).toLowerCase() !== term.toLowerCase()) { setSearch(search, term); return 'working'; }
      // The filter is applied and rows are rendered, but none is this customer. Results may still be arriving,
      // so main keeps asking until its time runs out.
      return rows.length ? 'not-found' : 'working';
    } catch (e) {
      return 'working';
    }
  };
})();
