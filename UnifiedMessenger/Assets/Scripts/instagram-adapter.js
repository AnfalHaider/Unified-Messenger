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
