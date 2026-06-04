(function () {
  'use strict';

  if (window.__umAiDraftInjectInstalled) {
    return;
  }

  window.__umAiDraftInjectInstalled = true;

  var composeSelectors = [
    '[role="textbox"][contenteditable="true"]',
    'div[contenteditable="true"][aria-label*="Reply" i]',
    'div[contenteditable="true"][aria-label*="Message" i]',
    'textarea[aria-label*="Reply" i]',
    'textarea[aria-label*="Message" i]',
    'textarea',
    '[contenteditable="true"]'
  ];

  function findComposeField() {
    for (var i = 0; i < composeSelectors.length; i++) {
      try {
        var nodes = document.querySelectorAll(composeSelectors[i]);
        for (var n = 0; n < nodes.length; n++) {
          var node = nodes[n];
          if (!node || node.offsetParent === null) {
            continue;
          }

          return node;
        }
      } catch (error) {
        console.warn('[UnifiedMessenger] compose selector failed', composeSelectors[i], error);
      }
    }

    return null;
  }

  function setFieldValue(field, text) {
    if (!field) {
      return false;
    }

    var value = String(text || '').trim();
    if (!value) {
      return false;
    }

    field.setAttribute('data-um-draft', 'true');
    field.setAttribute('data-um-draft-ts', new Date().toISOString());

    if (typeof field.value === 'string') {
      field.value = value;
      field.dispatchEvent(new Event('input', { bubbles: true }));
      field.dispatchEvent(new Event('change', { bubbles: true }));
      return true;
    }

    field.textContent = value;
    field.dispatchEvent(new InputEvent('input', { bubbles: true, data: value }));
    return true;
  }

  window.__umInjectDraftReply = function (text) {
    var field = findComposeField();
    if (!field) {
      return { ok: false, reason: 'compose-not-found' };
    }

    return { ok: setFieldValue(field, text), reason: 'filled' };
  };

  window.__umClearDraftReply = function () {
    var field = findComposeField();
    if (!field) {
      return { ok: false, reason: 'compose-not-found' };
    }

    field.removeAttribute('data-um-draft');
    field.removeAttribute('data-um-draft-ts');

    if (typeof field.value === 'string') {
      field.value = '';
      field.dispatchEvent(new Event('input', { bubbles: true }));
      return { ok: true, reason: 'cleared' };
    }

    field.textContent = '';
    field.dispatchEvent(new InputEvent('input', { bubbles: true, data: '' }));
    return { ok: true, reason: 'cleared' };
  };
})();
