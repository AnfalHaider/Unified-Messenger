(function () {
  'use strict';

  if (window.__umConnectionHandshakeInstalled) {
    return;
  }

  window.__umConnectionHandshakeInstalled = true;

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
      urlLoggedIn: ['web.whatsapp.com']
    },
    generic: {
      loggedIn: ['main', '[role="main"]', 'nav', 'header'],
      loggedOut: ['input[type="password"]', 'input[type="email"]', 'form[action*="login" i]'],
      urlLoggedIn: []
    }
  };

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

  function evaluateConnection(instanceId, platform) {
    var profile = resolveProfile(platform);

    if (anySelectorMatches(profile.loggedIn) || urlHintsLoggedIn(profile)) {
      publishStatus(instanceId, platform, 'Connected', 'Signed in');
      return 'Connected';
    }

    if (anySelectorMatches(profile.loggedOut) || bodyContainsAuthPrompt()) {
      publishStatus(instanceId, platform, 'LoggedOut', 'Sign-in UI detected');
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
