(function () {
  'use strict';

  if (window.__umConnectionHandshakeInstalled) {
    return;
  }

  window.__umConnectionHandshakeInstalled = true;

  // Each profile's loggedOut list is evaluated BEFORE its loggedIn list. See evaluateConnection.
  //
  // urlLoggedIn is deliberately empty for every real platform. It used to hold
  // ['web.whatsapp.com'] for WhatsApp, which meant an account parked on the QR sign-in screen
  // reported "Connected - Signed in", because being on the host was treated as proof of a session.
  // A URL can only ever say which client is loaded, never whether anyone is signed into it.
  var profiles = {
    whatsapp: {
      loggedIn: [
        '#pane-side',
        '[data-testid="chat-list"]',
        '[aria-label="Chat list"]',
        '[data-testid="chat-list-search"]',
        '#side'
      ],
      loggedOut: [
        '[data-testid="qrcode"]',
        'canvas[aria-label*="QR" i]',
        '[data-ref] div[data-testid="intro-text"]',
        '[data-testid="link-device-phone-number-code-screen-instructions"]'
      ],
      urlLoggedIn: []
    },
    instagram: {
      // Measured live 2026-09-04: a signed-in account always renders the Direct link in the
      // primary nav, and the profile/settings link. The feed itself is NOT a sign-in signal -
      // instagram.com serves a logged-out feed of public content too.
      loggedIn: [
        'a[href="/direct/inbox/"]',
        'a[href*="/accounts/edit"]',
        'nav a[href="/explore/"]',
        'svg[aria-label="New post"]'
      ],
      loggedOut: [
        'input[name="username"]',
        'input[name="password"]',
        'form#loginForm',
        'a[href^="/accounts/signup"]'
      ],
      urlLoggedIn: []
    },
    messenger: {
      loggedIn: [
        '[aria-label="Chats"]',
        '[role="navigation"] [href*="/t/"]',
        '[data-testid="mwthreadlist-item"]'
      ],
      loggedOut: [
        'input[name="pass"]',
        'input#pass',
        'input[name="email"]',
        '[data-testid="royal_login_form"]'
      ],
      urlLoggedIn: []
    },
    googlebusiness: {
      loggedIn: [
        '[aria-label*="review" i]',
        'a[href*="business.google.com"]',
        '[role="main"] [data-review-id]'
      ],
      loggedOut: [
        'input[type="email"][name="identifier"]',
        'input[name="Passwd"]',
        '#identifierId'
      ],
      // The only place a URL IS proof: Google redirects an unauthenticated session to its own
      // sign-in host, so landing there is a positive logged-out signal rather than an inference.
      urlLoggedOut: ['accounts.google.com/signin', 'accounts.google.com/servicelogin'],
      urlLoggedIn: []
    },
    generic: {
      loggedIn: ['main', '[role="main"]', 'nav', 'header'],
      loggedOut: ['input[type="password"]', 'input[type="email"]', 'form[action*="login" i]'],
      urlLoggedIn: []
    }
  };

  // whatsappbusiness runs the identical web client, so it shares WhatsApp's anchors outright
  // rather than falling through to generic, whose 'nav, header' test would match the QR screen.
  profiles.whatsappbusiness = profiles.whatsapp;

  function resolveProfile(platform) {
    var key = String(platform || 'generic').toLowerCase();
    return profiles[key] || profiles.generic;
  }

  function isVisible(element) {
    return !!(element && (element.offsetParent !== null || element.getClientRects().length > 0));
  }

  function anySelectorMatches(selectors) {
    for (var i = 0; i < selectors.length; i++) {
      try {
        var node = document.querySelector(selectors[i]);
        if (isVisible(node)) {
          return true;
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] selector failed', selectors[i], error);
      }
    }

    return false;
  }

  function urlHintsLoggedIn(profile) {
    if (!profile.urlLoggedIn || !profile.urlLoggedIn.length) {
      return false;
    }

    var href = String(window.location.href || '').toLowerCase();
    for (var i = 0; i < profile.urlLoggedIn.length; i++) {
      if (href.indexOf(profile.urlLoggedIn[i]) >= 0) {
        return true;
      }
    }

    return false;
  }

  function bodyContainsAuthPrompt() {
    var text = (document.body && document.body.innerText) || '';
    return /\b(sign in|log in|continue with google|scan.*qr|link.*device)\b/i.test(text);
  }

  function publishStatus(instanceId, platform, status, detail) {
    window.__umPostMessage({
      type: 'connection-status',
      instanceId: instanceId,
      platform: platform,
      status: status,
      detail: detail || '',
      timestampUtc: new Date().toISOString()
    });
  }

  function urlHintsLoggedOut(profile) {
    if (!profile.urlLoggedOut || !profile.urlLoggedOut.length) {
      return false;
    }

    var href = String(window.location.href || '').toLowerCase();
    for (var i = 0; i < profile.urlLoggedOut.length; i++) {
      if (href.indexOf(profile.urlLoggedOut[i]) >= 0) {
        return true;
      }
    }

    return false;
  }

  // ORDER MATTERS, and it is the reverse of what it was.
  //
  // Logged-out is tested first because the two states are not symmetric: sign-in markup is
  // specific and unambiguous (a QR canvas, a password field), whereas "signed in" markup is
  // generic and a login page carries plenty of it - the generic profile's own logged-in test is
  // 'main, nav, header', which most sign-in pages satisfy. Asking "signed in?" first therefore
  // answers yes on a sign-in screen and never reaches the question that would have said no.
  //
  // The cost of guessing wrong in each direction is also asymmetric. A false "signed out" shows
  // the owner an account they can fix. A false "signed in" lets the scraper run against a page
  // with no data, find nothing, and report a quiet account - the exact false calm this gate exists
  // to prevent.
  function evaluateConnection(instanceId, platform) {
    var profile = resolveProfile(platform);

    if (anySelectorMatches(profile.loggedOut) || urlHintsLoggedOut(profile)) {
      publishStatus(instanceId, platform, 'LoggedOut', 'Sign-in screen');
      return 'LoggedOut';
    }

    if (anySelectorMatches(profile.loggedIn) || urlHintsLoggedIn(profile)) {
      publishStatus(instanceId, platform, 'Connected', 'Signed in');
      return 'Connected';
    }

    // bodyContainsAuthPrompt is a weak, text-based heuristic, so it only runs once the two
    // structural tests have both declined to answer. It can never override a positive match.
    if (bodyContainsAuthPrompt()) {
      publishStatus(instanceId, platform, 'LoggedOut', 'Sign-in wording on the page');
      return 'LoggedOut';
    }

    publishStatus(instanceId, platform, 'Initializing', 'Waiting for inbox or sign-in UI');
    return 'Initializing';
  }

  window.__umStartConnectionHandshake = function (instanceId, platform) {
    if (!instanceId) {
      return;
    }

    publishStatus(instanceId, platform, 'Initializing', 'Navigation completed');

    if (window.__umConnectionObserver) {
      window.__umConnectionObserver.disconnect();
      window.__umConnectionObserver = null;
    }

    if (window.__umConnectionPollTimer) {
      clearInterval(window.__umConnectionPollTimer);
      window.__umConnectionPollTimer = null;
    }

    var lastStatus = null;
    var evaluate = function () {
      var next = evaluateConnection(instanceId, platform);
      if (next !== lastStatus) {
        lastStatus = next;
      }
    };

    evaluate();

    var scheduleEvaluate = function (delayMs) {
      window.setTimeout(evaluate, delayMs);
    };

    scheduleEvaluate(400);
    scheduleEvaluate(1200);
    scheduleEvaluate(3000);

    window.__umConnectionPollTimer = window.setInterval(evaluate, 2500);

    window.__umConnectionObserver = new MutationObserver(function () {
      evaluate();
    });

    var root = document.documentElement || document.body;
    if (root) {
      window.__umConnectionObserver.observe(root, {
        childList: true,
        subtree: true,
        attributes: true
      });
    }

    window.__umRegisterDisposable(function () {
      if (window.__umConnectionObserver) {
        window.__umConnectionObserver.disconnect();
        window.__umConnectionObserver = null;
      }

      if (window.__umConnectionPollTimer) {
        clearInterval(window.__umConnectionPollTimer);
        window.__umConnectionPollTimer = null;
      }
    });
  };
})();
