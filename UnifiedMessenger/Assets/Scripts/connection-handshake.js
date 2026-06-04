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
        'img[alt*="logo" i][src*="googleusercontent"]'
      ],
      loggedOut: [
        'input[type="email"]',
        '#identifierId',
        '[data-action="sign in" i]',
        'form[action*="accounts.google.com"]'
      ]
    },
    metabusiness: {
      loggedIn: [
        '[data-testid="inbox_thread_list"]',
        '[aria-label*="Inbox" i]',
        '[role="navigation"] a[href*="inbox"]',
        '[data-pagelet="BizInbox"]'
      ],
      loggedOut: [
        'input[name="email"]',
        '#email',
        '[data-testid="login_form"]',
        'button[name="login"]'
      ]
    },
    whatsapp: {
      loggedIn: [
        '#pane-side',
        '[data-testid="chat-list"]',
        '[aria-label="Chat list"]'
      ],
      loggedOut: [
        '[data-testid="qrcode"]',
        'canvas[aria-label*="QR" i]',
        '[data-ref] div[data-testid="intro-text"]'
      ]
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
      ]
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
      ]
    },
    discord: {
      loggedIn: [
        '[class*="guilds"]',
        '[aria-label="Servers sidebar"]',
        '[class*="sidebar"] [role="tree"]'
      ],
      loggedOut: [
        'form[class*="authBox"]',
        'input[name="email"]',
        'button[type="submit"]'
      ]
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
      ]
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
      ]
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
      ]
    },
    generic: {
      loggedIn: ['main', '[role="main"]', 'nav', 'header'],
      loggedOut: ['input[type="password"]', 'input[type="email"]', 'form[action*="login" i]']
    }
  };

  function resolveProfile(platform) {
    var key = String(platform || 'generic').toLowerCase();
    return profiles[key] || profiles.generic;
  }

  function anySelectorMatches(selectors) {
    for (var i = 0; i < selectors.length; i++) {
      try {
        if (document.querySelector(selectors[i])) {
          return true;
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] selector failed', selectors[i], error);
      }
    }

    return false;
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

    if (anySelectorMatches(profile.loggedIn)) {
      publishStatus(instanceId, platform, 'Connected', 'Signed-in UI detected');
      return 'Connected';
    }

    if (anySelectorMatches(profile.loggedOut)) {
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

    var lastStatus = null;
    var evaluate = function () {
      var next = evaluateConnection(instanceId, platform);
      if (next !== lastStatus) {
        lastStatus = next;
      }
    };

    evaluate();

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
    });
  };
})();
