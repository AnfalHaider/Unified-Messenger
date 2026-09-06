(function () {
  'use strict';

  // Instagram oversight reader (A13).
  //
  // WHY THIS READS RELAY AND NOT LIGHTSPEED. Instagram ships the same LightSpeed/MSYS store as
  // messenger.com - require('LSDatabaseSingleton') resolves, with the same 358 tables - and on the feed
  // page it is EMPTY: threads, messages and contacts all zero, measured on a live account with six unread
  // DMs. The first pass measured that store, found nothing, and concluded the channel was countable only.
  // That conclusion did not follow. Instagram prefetches the DM mailbox into its RELAY store on the feed,
  // to draw its own Messages badge, and that prefetch is the whole opportunity: it is the client's own
  // request, already made, for its own reasons. We read what is already there.
  //
  // WHAT IS DELIBERATELY NOT HERE. No navigation, no query, no cursor following. The inbox connection
  // reports has_next_page = true, so a deeper backlog exists and could be paged - but issuing our own
  // query is a different act from reading one the client already made, and opening a thread would fire a
  // read receipt at a real customer. Both stay out. See docs/scraper-inventory/instagram.md.

  if (window.__umInstagramAdapterInstalled) {
    return;
  }

  window.__umInstagramAdapterInstalled = true;

  var THREAD_TYPE = 'XFBIGDirectViewerThread';
  var USER_TYPE = 'XDTUserDict';
  var BADGE_TYPE = 'XDTNotificationBadgeCount';

  // Ordered candidates, same idea as the selector manifest: the environment module name is the one thing
  // here that a Meta refactor is most likely to move.
  var ENVIRONMENT_MODULES = [
    'PolarisRelayEnvironment',
    'IGRelayEnvironment',
    'RelayEnvironment'
  ];

  function resolveSource() {
    if (typeof require !== 'function') {
      return { source: null, stage: 'no-require' };
    }

    for (var i = 0; i < ENVIRONMENT_MODULES.length; i++) {
      var name = ENVIRONMENT_MODULES[i];
      try {
        var mod = require(name);
        var env = mod && (mod.default || mod);
        if (!env || typeof env.getStore !== 'function') {
          continue;
        }

        var store = env.getStore();
        var source = store && typeof store.getSource === 'function' ? store.getSource() : null;
        if (source && typeof source.getRecordIDs === 'function') {
          return { source: source, stage: 'done', via: name };
        }
      } catch (error) {
        // Keep trying the next candidate. A module that is absent throws rather than returning null.
      }
    }

    return { source: null, stage: 'no-relay-environment' };
  }

  // Cut without splitting a surrogate pair. A raw slice through an emoji leaves a lone surrogate, and
  // System.Text.Json then throws on that property - which once dropped a real conversation from every
  // single scan. Same rule as window.__umTruncate, restated because adapter-core is not injected here.
  function safeTruncate(value, max) {
    var text = String(value == null ? '' : value);
    if (text.length <= max) {
      return text;
    }

    var cut = text.slice(0, max);
    var last = cut.charCodeAt(cut.length - 1);
    if (last >= 0xd800 && last <= 0xdbff) {
      cut = cut.slice(0, -1);
    }

    return cut;
  }

  function readResolver(source, record, field) {
    var ref = record && record[field];
    if (!ref || !ref.__ref) {
      return null;
    }

    var resolved = source.get(ref.__ref);
    return resolved ? resolved.__resolverValue : null;
  }

  function readUsername(source, record) {
    var users = record && record.users;
    var refs = users && users.__refs;
    if (!refs || !refs.length) {
      return '';
    }

    var user = source.get(refs[0]);
    return user && user.__typename === USER_TYPE && user.username ? String(user.username) : '';
  }

  function readBadge(source, ids) {
    var badge = null;
    for (var i = 0; i < ids.length; i++) {
      var record = source.get(ids[i]);
      if (record && record.__typename === BADGE_TYPE) {
        badge = record;
        break;
      }
    }

    if (!badge) {
      return null;
    }

    var counts = badge.activity_badge_counts;
    var detail = counts && counts.__ref ? source.get(counts.__ref) : null;

    return {
      total: typeof badge.total_count === 'number' ? badge.total_count : null,
      comments: detail && typeof detail.comments === 'number' ? detail.comments : null,
      likes: detail && typeof detail.likes === 'number' ? detail.likes : null,
      relationships: detail && typeof detail.relationships === 'number' ? detail.relationships : null
    };
  }

  // ─── Focus a conversation WITHOUT opening it ────────────────────────────────────────────────
  //
  // Clicking an Instagram row in the needs-a-reply queue lands the owner on Direct with that
  // conversation filtered to the top of the list — and stops there. It never clicks the thread.
  //
  // THAT IS THE WHOLE DESIGN. Opening an Instagram thread marks it read and fires a "Seen" to the
  // customer, which cannot be withdrawn and destroys the very signal this app measures. So the app
  // takes the owner to the doorstep and lets them decide to step through it.
  //
  // Typing into the SEARCH box is not the banned interaction. The prohibition is on synthesising
  // input into a message composer or clicking send; a search field neither sends anything nor is
  // visible to the customer. The two are kept apart deliberately, and a test asserts this script
  // touches no composer and clicks no thread row.

  var INBOX_PATH = '/direct/inbox';
  var THREAD_PATH = '/direct/t/';

  var SEARCH_INPUTS = [
    'input[placeholder="Search"]',
    'input[aria-label="Search input"]',
    'input[placeholder*="Search" i]',
    'input[aria-label*="Search" i]'
  ];

  function firstMatch(selectors) {
    for (var i = 0; i < selectors.length; i++) {
      try {
        var node = document.querySelector(selectors[i]);
        if (node) {
          return node;
        }
      } catch (error) {
        // Bad selector: try the next candidate rather than abandoning the search.
      }
    }
    return null;
  }

  function onInbox() {
    return String(location.pathname || '').indexOf(INBOX_PATH) === 0;
  }

  function insideThread() {
    return String(location.pathname || '').indexOf(THREAD_PATH) >= 0;
  }

  // React owns the input's value, so assigning .value directly updates the DOM and leaves React's
  // state stale — the list would not filter. Going through the native setter and then dispatching a
  // bubbling 'input' event is what makes React observe the change.
  function typeInto(input, text) {
    var descriptor = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
    if (descriptor && descriptor.set) {
      descriptor.set.call(input, text);
    } else {
      input.value = text;
    }

    input.dispatchEvent(new Event('input', { bubbles: true }));
  }

  // Instagram display names routinely carry emoji and decorative symbols — "MahnoorKhan🦋" is a real one
  // from the owner's own inbox. Two things go wrong if they are typed verbatim: Instagram's own search
  // finds nothing, and any readback that looks for the raw string back in the page fails even when the
  // right row is on screen. Search on the letters, which is what a person would type.
  function searchableName(name) {
    var text = String(name || '');
    var stripped = '';

    for (var i = 0; i < text.length; i++) {
      var ch = text[i];
      var code = text.charCodeAt(i);

      // Drop surrogate pairs (emoji live above the BMP) and anything that is not a letter, digit or
      // separator. Keeping accented letters matters: the owner's customers are named in three scripts.
      if (code >= 0xd800 && code <= 0xdfff) {
        i++;
        stripped += ' ';
        continue;
      }

      stripped += /[\p{L}\p{N}\s'.-]/u.test(ch) ? ch : ' ';
    }

    return stripped.replace(/\s+/g, ' ').trim();
  }

  window.__umFocusConversation = function (platform, key, name) {
    try {
      var query = searchableName(name) || String(name || '').trim();
      if (!query) {
        // Nothing to search for. Landing on the inbox is still the right outcome, but there is no
        // filtering to claim, so this reports failure and the caller falls back to "account opened".
        return false;
      }

      if (insideThread()) {
        // Already inside somebody's conversation. Do NOT stay - back out to the list, because leaving
        // the owner in a thread they did not choose is the outcome this whole path exists to avoid.
        location.assign('https://www.instagram.com/direct/inbox/');
        return false;
      }

      if (!onInbox()) {
        location.assign('https://www.instagram.com/direct/inbox/');
        return false;
      }

      var input = firstMatch(SEARCH_INPUTS);
      if (!input) {
        return false;
      }

      if (input.value !== query) {
        typeInto(input, query);
      }

      // Deliberately returns without clicking anything. The readback below decides whether the filter
      // actually took, from the DOM rather than from this function's own optimism.
      return true;
    } catch (error) {
      return false;
    }
  };

  // Independent readback for the focus above. Asserts the OPPOSITE of the WhatsApp one: WhatsApp's
  // proves a conversation is open, this proves we are still on the list and have not opened one.
  window.__umIgFocusReadback = function () {
    try {
      if (insideThread() || !onInbox()) {
        return false;
      }

      var query = '';
      var input = firstMatch(SEARCH_INPUTS);
      if (input) {
        query = String(input.value || '').trim().toLowerCase();
      }

      // Proves what this operation actually promises: the owner is on the Direct list, no conversation
      // has been opened, and the list is filtered by our query.
      //
      // It deliberately does NOT require a matching row. Whether Instagram finds that customer is
      // Instagram's answer, not ours — a name may be unsearchable, or the thread may have moved to
      // Requests. Demanding a hit made the first version fail sixteen times on a name containing an
      // emoji and drop the owner on a page it had itself just loaded, reporting failure for a
      // navigation that had worked. If nothing matches, Instagram says so on screen, which is the
      // honest outcome and one the owner can act on.
      return query.length > 0;
    } catch (error) {
      return false;
    }
  };

  // ─── Preview harvest, on the inbox route only ───────────────────────────────────────────────
  //
  // Runs ONLY after the owner has clicked an Instagram customer and the app has navigated that
  // account to Direct. Never on a background cycle: the passive read on the feed stays passive, and
  // an account the owner is not opening is never navigated.
  //
  // Reads the RENDERED LIST, not the Relay store. The feed's prefetch carries no snippet field at
  // all, and whether the inbox route adds one has not been measured — whereas the list visibly
  // shows "You sent a photo · 58m" on screen, so the DOM is the source we know exists. Each row is
  // an anchor to its own thread; reading that anchor's href is not clicking it.

  var ROW_LINKS = [
    'a[href^="/direct/t/"]',
    'div[role="listitem"] a[href*="/direct/t/"]'
  ];

  function rowAnchors() {
    for (var i = 0; i < ROW_LINKS.length; i++) {
      try {
        var found = document.querySelectorAll(ROW_LINKS[i]);
        if (found && found.length) {
          return Array.prototype.slice.call(found);
        }
      } catch (error) {
        // Try the next candidate.
      }
    }
    return [];
  }

  function threadIdFrom(href) {
    var match = String(href || '').match(/\/direct\/t\/(\d+)/);
    return match ? match[1] : '';
  }

  window.__umHarvestInstagramInbox = function () {
    var out = { diag: { stage: 'starting' }, rows: [] };

    try {
      if (!onInbox() || insideThread()) {
        // Only ever harvests from the list. If something navigated into a conversation, this reports
        // nothing rather than scraping a thread the owner did not ask to open.
        out.diag.stage = 'not-on-inbox';
        return JSON.stringify(out);
      }

      var anchors = rowAnchors();
      out.diag.anchors = anchors.length;

      for (var i = 0; i < anchors.length; i++) {
        var anchor = anchors[i];
        var lines = String(anchor.innerText || '')
          .split('\n')
          .map(function (line) { return line.trim(); })
          .filter(function (line) { return line.length > 0; });

        if (!lines.length) {
          continue;
        }

        // Line 0 is the display name. The preview is the next line that is not purely a timestamp —
        // Instagram renders "Raja sent a video." and "· 5h" as separate runs, and a naive "line 1"
        // read picks up the age on rows whose preview is empty.
        var preview = '';
        for (var j = 1; j < lines.length; j++) {
          if (!/^[·•]?\s*\d+\s*(m|h|d|w|min|hour|day|week)/i.test(lines[j])) {
            preview = lines[j];
            break;
          }
        }

        out.rows.push({
          threadId: threadIdFrom(anchor.getAttribute('href')),
          name: safeTruncate(lines[0], 120),
          preview: safeTruncate(preview, 240)
        });
      }

      out.diag.stage = out.rows.length > 0 ? 'done' : 'empty';
    } catch (error) {
      out.diag.stage = 'error';
      out.diag.message = String(error && error.message).slice(0, 120);
    }

    return JSON.stringify(out);
  };

  window.__umReadInstagramThreads = function () {
    var out = { diag: { stage: 'starting' }, conversations: [], badge: null };

    try {
      var resolved = resolveSource();
      if (!resolved.source) {
        out.diag.stage = resolved.stage;
        return JSON.stringify(out);
      }

      var source = resolved.source;
      var ids = source.getRecordIDs();
      out.diag.via = resolved.via;
      out.diag.records = ids.length;

      for (var i = 0; i < ids.length; i++) {
        var record = source.get(ids[i]);
        if (!record || record.__typename !== THREAD_TYPE) {
          continue;
        }

        // marked_as_unread is the manual "Mark as unread" flag, NOT the unread state. It read false on
        // all 15 threads of an account whose own badge said 6, so a reader trusting it reports every
        // account permanently caught up. The resolver is the real signal.
        var unread = readResolver(source, record, '$r:client__is_unread') === true;
        var timestamp = Number(record.last_activity_timestamp_ms);

        out.conversations.push({
          key: String(record.thread_key || record.id || ''),
          name: safeTruncate(record.thread_title || '', 120),
          username: safeTruncate(readUsername(source, record), 60),
          unread: unread ? 1 : 0,
          // Unread means the owner has not OPENED it, which is a lower bound on "awaiting a reply": a
          // thread they read and did not answer is still awaiting and reads as read here. The surface
          // says "at least N" for exactly this reason.
          awaiting: unread,
          lastActivityMs: isFinite(timestamp) ? timestamp : 0,
          subtype: String(record.thread_subtype || '')
        });
      }

      out.badge = readBadge(source, ids);

      // The client's own unread-thread count, from the tab title: "(6) Instagram". An INDEPENDENT
      // readback of the same fact the resolver reports, and the reason it is here is a measured defect,
      // not caution.
      //
      // Seconds after the app warms an account, the resolver returns true for EVERY thread - observed
      // live at 15 of 15 on an account whose badge said 2, sixty-five seconds after launch, settling to
      // 2 of 15 shortly afterwards. Read state has not synced yet, and nothing in the record says so:
      // __resolverValueMayBeInvalid is false and __resolverError is unset, so the store looks settled
      // while reporting the opposite of the truth. A scan landing in that window would put thirteen
      // people in the owner's needs-a-reply queue who are not waiting, and fire a threshold toast about
      // them.
      //
      // The C# side discards the scan when unread exceeds this. Exceeds, not differs: the badge counts
      // every unread thread while this reads the top 15 of Primary, so an account with 20 unread
      // legitimately reports 15 against a badge of 20. Over-reporting is the direction that invents
      // waiting customers.
      // MUST tolerate the capped form. Instagram writes "(9+) Instagram" once the count passes nine, and
      // a digits-only pattern returns null there — which the C# side then reads as a badge of zero and
      // rejects the whole account. Measured live: depilex_f11_islamabad showed "(9+)" with 15 threads
      // genuinely unread, and the first version of this guard discarded every one of them. A busy account
      // is exactly the account that must not be silently dropped.
      var titleMatch = String(document.title || '').match(/^\((\d+)(\+?)\)/);
      out.unreadBadge = titleMatch ? Number(titleMatch[1]) : 0;
      out.unreadBadgeCapped = titleMatch ? titleMatch[2] === '+' : false;

      out.diag.stage = out.conversations.length > 0 ? 'done' : 'empty';
      out.diag.count = out.conversations.length;
    } catch (error) {
      out.diag.stage = 'error';
      out.diag.message = String(error && error.message).slice(0, 120);
    }

    return JSON.stringify(out);
  };
})();
