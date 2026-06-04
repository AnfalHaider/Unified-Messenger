(function () {
  'use strict';

  if (window.__umConnectionHandshakeInstalled) {
    return;
  }

  window.__umConnectionHandshakeInstalled = true;

  var profiles = {
    googlebusiness: {
      loggedIn: [
        '[data-merchant-id]',
        '[aria-label*="Business Profile" i]',
        '[href*="business.google.com"][aria-current]',
        'img[alt*="logo" i][src*="googleusercontent"]',
        '[role="main"]',
        'a[href*="/reviews" i]',
        'a[href*="/messaging" i]'
      ],
      loggedOut: [
        'input[type="email"]',
        '#identifierId',
        '[data-action="sign in" i]',
        'form[action*="accounts.google.com"]',
        'button[jsname*="login" i]'
      ],
      urlLoggedIn: ['business.google.com/locations', 'business.google.com/reviews', 'business.google.com/messaging']
    },
    metabusiness: {
      loggedIn: [
        '[data-testid="inbox_thread_list"]',
        '[aria-label*="Inbox" i]',
        '[role="navigation"] a[href*="inbox"]',
        '[data-pagelet="BizInbox"]',
        '[role="main"] [role="row"]',
        'div[aria-label*="Conversation" i]'
      ],
      loggedOut: [
        'input[name="email"]',
        '#email',
        '[data-testid="login_form"]',
        'button[name="login"]',
        'input[type="password"][name="pass"]'
      ],
      urlLoggedIn: ['business.facebook.com', 'facebook.com/latest/inbox']
    },
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
    discord: {
      loggedIn: [
        '[aria-label="Servers sidebar"]',
        '[class*="guilds"]',
        '[class*="sidebar"] [role="tree"]',
        'nav[aria-label*="Servers" i]',
        '[data-list-id="guildsnav"]'
      ],
      loggedOut: [
        'form[class*="authBox"]',
        'input[name="email"]',
        'button[type="submit"]',
        '[class*="authBox"] h1'
      ],
      urlLoggedIn: ['discord.com/channels', 'discord.com/app']
    },
    telegram: {
      loggedIn: [
        '.chatlist',
        '#column-left .chatlist',
        '.tabs-tab[data-tab="0"].active'
      ],
      loggedOut: [
        '#auth-pages',
        '.auth-form',
        'input[type="tel"]'
      ],
      urlLoggedIn: ['web.telegram.org']
    },
    slack: {
      loggedIn: [
        '[data-qa="channel_sidebar"]',
        '.p-client_container',
        '[aria-label="Channels"]'
      ],
      loggedOut: [
        '[data-qa="signin_form"]',
        'input[name="email"]',
        '#signin_btn'
      ],
      urlLoggedIn: ['app.slack.com/client']
    },
    messenger: {
      loggedIn: [
        '[role="navigation"]',
        '[aria-label="Chats"]',
        '[data-pagelet="MWThreadList"]'
      ],
      loggedOut: [
        'input[name="email"]',
        '#email',
        'button[name="login"]'
      ],
      urlLoggedIn: ['messenger.com']
    },
    teams: {
      loggedIn: [
        '[data-tid="experience-layout"]',
        '[aria-label*="Chat" i]',
        '.ui-tree__item'
      ],
      loggedOut: [
        'input[type="email"]',
        '#i0116',
        '#idSIButton9'
      ],
      urlLoggedIn: ['teams.microsoft.com']
    },
    signal: {
      loggedIn: [
        '.module-left-pane',
        '.conversation-list',
        '.inbox'
      ],
      loggedOut: [
        '.module-sign-in',
        'input[type="tel"]'
      ],
      urlLoggedIn: []
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
      publishStatus(instanceId, platform, 'Connected', 'Signed-in UI detected');
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
