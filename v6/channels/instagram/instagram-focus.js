(function () {
  'use strict';

  // Take the owner to one Instagram conversation's doorstep: Direct, with the list filtered to that customer.
  // Ported from v5.0.0 (commit 0127ef8). It STOPS there, by design: opening a thread marks it read and sends
  // the customer a "Seen" that cannot be withdrawn, and it clears the unread that is this channel's only sign
  // someone is waiting. The owner decides whether to step through.
  //
  // Typing into Direct's search sends nothing and is invisible to the customer. This file must never click,
  // never touch a message box and never navigate to a thread; tests read the file to hold it to that.
  //
  // One step per call, like the WhatsApp focus: done | working | no-target.

  if (window.__umInstagramFocusInstalled) return;
  window.__umInstagramFocusInstalled = true;

  var INBOX_PATH = '/direct/inbox';
  var THREAD_PATH = '/direct/t/';
  var SEARCH = ['input[placeholder="Search"]', 'input[aria-label="Search input"]', 'input[placeholder*="Search" i]', 'input[aria-label*="Search" i]'];

  function first(candidates) {
    for (var i = 0; i < candidates.length; i++) {
      try { var el = document.querySelector(candidates[i]); if (el) return el; } catch (e) { /* next candidate */ }
    }
    return null;
  }

  // Display names carry emoji and symbols that Instagram's search does not match. Search on the letters.
  function searchable(name) {
    var text = String(name || ''), out = '';
    for (var i = 0; i < text.length; i++) {
      var code = text.charCodeAt(i);
      if (code >= 0xd800 && code <= 0xdfff) { i++; out += ' '; continue; }
      out += /[\p{L}\p{N}\s'.-]/u.test(text[i]) ? text[i] : ' ';
    }
    return out.replace(/\s+/g, ' ').trim();
  }

  function typeInto(input, value) {
    var desc = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
    if (desc && desc.set) desc.set.call(input, value); else input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }

  window.__umFocusInstagram = function (name) {
    try {
      var query = searchable(name);
      if (!query) return 'no-target';
      var path = String(location.pathname || '');
      // Inside somebody's thread, or anywhere but Direct: go to the list, never stay in a thread nobody chose.
      if (path.indexOf(THREAD_PATH) >= 0 || path.indexOf(INBOX_PATH) < 0) {
        location.assign(location.origin + '/direct/inbox/');
        return 'working';
      }
      var input = first(SEARCH);
      if (!input) return 'working';
      if (input.value !== query) { typeInto(input, query); return 'working'; }
      // On Direct, no thread open, list filtered by this customer. Whether Instagram finds them is its answer.
      return 'done';
    } catch (e) {
      return 'working';
    }
  };
})();
