// Google reviews, read from the two pages that carry them. Ported from v5's GoogleReviewSnapshotService, whose
// every rule was found against the owner's live profiles; the comments say what each one guards against.
//
// Read-only: the only clicks are Google's own "rows per page" control and the "More" expander on a review's text,
// both of which change what the page shows and nothing else. Nothing is ever typed, posted or replied.
//
// window.__umGoogleReviews()  -> JSON { state, cards: [{ reviewer, text, stars, age, replied }], more }
// window.__umGoogleProfile()  -> JSON { state, text, aria }   (parsed by core/reviews.ts parseProfile)
(function () {
  if (window.__umGoogleReady) return;
  window.__umGoogleReady = true;

  var sleep = function (ms) { return new Promise(function (r) { setTimeout(r, ms); }); };
  var PUA = /[-]/g;
  var AGE = /^(a|an|\d+)\s+(second|minute|hour|day|week|month|year)s?\s+ago$/i;
  var buttons = function (re) {
    return [].slice.call(document.querySelectorAll('button')).filter(function (b) { return re.test((b.innerText || '').trim()); });
  };

  // Google shows 10 rows by default and offers 10, 25 or 50. Once per page load, choose the largest. The flag is
  // set only after the control is found: set on entry, a read before Google rendered it pinned the page to 10.
  function bumpRows() {
    if (window.__umGRows) return false;
    var rb = document.querySelector('[aria-label="Number of rows per page"]');
    if (!rb) return false;
    window.__umGRows = true;
    try {
      var opener = rb.querySelector('[jsname="LgbsSe"]') || rb.querySelector('[data-value]');
      if (opener) opener.click();
      setTimeout(function () {
        try {
          var o = [].slice.call(rb.querySelectorAll('[data-value]'));
          var max = o.reduce(function (a, c) { return +(c.getAttribute('data-value') || 0) > +(a.getAttribute('data-value') || 0) ? c : a; }, o[0]);
          if (max) max.click();
        } catch (e) { /* stays at the default page size */ }
      }, 250);
    } catch (e) { return false; }
    return true;
  }

  // A review's card is the LARGEST ancestor of its Reply or Edit button that still holds only that one action
  // button; one more step up is the list holding every review. (The smallest-with-some-text rule cut long reviews.)
  function cardOf(btn) {
    var n = btn.parentElement, best = null;
    for (var i = 0; i < 10 && n; i++) {
      var acts = [].slice.call(n.querySelectorAll('button')).filter(function (b) { return /(^|\b)(reply|edit)\b/i.test((b.innerText || '').trim()); }).length;
      if (acts > 1) break;
      if (((n.innerText || '').trim()).length >= 25) best = n;
      n = n.parentElement;
    }
    return best;
  }

  // THE RATING IS IN THE COLOUR, NOT THE GLYPH. Every review draws five identical icon-font stars and colours the
  // filled ones; counting codepoints reported every review as five stars, so one-star reviews were shown as praise.
  // The filled stars come first and the first is always filled, so the rating is the leading run of the first
  // star's own colour, whatever colour Google uses for it.
  function starsOf(card) {
    try {
      var els = [].slice.call(card.querySelectorAll('*')).filter(function (el) { return el.children.length === 0 && /[-]/.test(el.textContent || ''); });
      if (els.length < 5) return 0;
      var cols = els.slice(0, 5).map(function (el) { return getComputedStyle(el).color; });
      var n = 0;
      for (var i = 0; i < cols.length && cols[i] === cols[0]; i++) n++;
      return n >= 1 && n <= 5 ? n : 0;
    } catch (e) { return 0; }
  }

  // Long reviews end in "... More" until expanded. Once per page load, and only for unanswered reviews: expanding
  // rewrites the text a later check compares, so doing it again made a page look new and count itself twice.
  function expand(replyButtons) {
    if (window.__umGExpanded) return false;
    window.__umGExpanded = true;
    var clicked = 0;
    replyButtons.slice(0, 25).forEach(function (btn) {
      var card = cardOf(btn);
      if (!card) return;
      [].slice.call(card.querySelectorAll('*')).forEach(function (el) {
        if (el.children.length === 0 && /^(more|read more)$/i.test((el.textContent || '').trim())) { try { el.click(); clicked++; } catch (e) { /* stays short */ } }
      });
    });
    return clicked > 0;
  }

  // The card's lines are: location name, location address, reviewer, stars-and-age, text, then the actions. The
  // age line is the anchor: the reviewer is the line above it, the text everything below.
  function readCard(btn, replied) {
    var card = cardOf(btn);
    var lines = ((card && card.innerText) || '').split('\n').map(function (s) { return s.trim(); }).filter(Boolean);
    var isAction = function (l) { return /^(reply|edit|share|delete|report|more_vert|more|less|read more|show (more|less)|\(owner\))$/i.test(l); };
    var at = -1;
    for (var i = 0; i < lines.length; i++) if (AGE.test(lines[i].replace(PUA, '').trim())) { at = i; break; }
    var age = '', reviewer = '', text = '';
    if (at >= 0) {
      age = lines[at].replace(PUA, '').trim();
      reviewer = at > 0 ? lines[at - 1] : '';
      // An answered review's card also holds the owner's reply; its text is not needed and is not read.
      if (!replied) text = lines.slice(at + 1).filter(function (l) { return !isAction(l); }).join(' ');
    }
    text = text.replace(/\s*(\.\.\.|…)\s*More$/i, '…').replace(PUA, '').trim();
    return { reviewer: reviewer.slice(0, 60), text: text.slice(0, 1200), stars: card ? starsOf(card) : 0, age: age, replied: replied };
  }

  function nextButton() {
    var all = [].slice.call(document.querySelectorAll('button,[role="button"]'));
    var byIcon = all.filter(function (e) { return /(^|\b)navigate_next(\b|$)/.test((e.innerText || '').trim()); });
    if (byIcon.length) return byIcon[0];
    var byAria = all.filter(function (e) { return /^next$/i.test(e.getAttribute('aria-label') || ''); });
    return byAria.length ? byAria[0] : null;
  }

  window.__umGoogleReviews = async function () {
    try {
      if (/(^|\.)accounts\.google\.com$/i.test(location.host)) return JSON.stringify({ state: 'signed-out' });
      if (!/\/reviews(\/|$)/.test(location.pathname)) return JSON.stringify({ state: 'notreviews' });
      // Up to about twenty seconds for the list to render, the page size to change and long reviews to expand.
      for (var tries = 0; tries < 40; tries++) {
        if (bumpRows()) { await sleep(1500); continue; }
        var replies = buttons(/(^|\b)reply\b/i), edits = buttons(/\bedit\b/i);
        if (replies.length + edits.length === 0) { await sleep(500); continue; }
        if (expand(replies)) { await sleep(800); continue; }
        var cards = replies.map(function (b) { return readCard(b, false); }).concat(edits.map(function (b) { return readCard(b, true); }));
        var next = nextButton();
        return JSON.stringify({ state: 'done', cards: cards, more: !!(next && !next.disabled && next.getAttribute('aria-disabled') !== 'true') });
      }
      return JSON.stringify({ state: 'empty' });
    } catch (e) {
      return JSON.stringify({ state: 'error' });
    }
  };

  // The Search merchant view. Its text and its "Rated … out of 5" labels go back whole, to be parsed in core where
  // the rules are tested; it waits until a review count is on the page, or gives up after about fifteen seconds.
  window.__umGoogleProfile = async function () {
    try {
      if (/(^|\.)accounts\.google\.com$/i.test(location.host)) return JSON.stringify({ state: 'signed-out' });
      for (var tries = 0; tries < 30; tries++) {
        var text = (document.body && document.body.innerText) || '';
        if (/Google\s+reviews|[0-5][.,]\d[^\d(]{0,12}\(\d/i.test(text)) {
          var aria = [].slice.call(document.querySelectorAll('[aria-label]')).map(function (e) { return e.getAttribute('aria-label') || ''; })
            .filter(function (l) { return /Rated\s/i.test(l); }).slice(0, 40);
          return JSON.stringify({ state: 'done', text: text.slice(0, 40000), aria: aria, host: location.host });
        }
        await sleep(500);
      }
      return JSON.stringify({ state: 'no-rating', host: location.host });
    } catch (e) {
      return JSON.stringify({ state: 'error' });
    }
  };
})();
