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

  window.__umDraftStreamActive = false;
  window.__umDraftStreamBuffer = '';

  function dispatchFieldInput(field, data) {
    try {
      field.dispatchEvent(new InputEvent('beforeinput', { bubbles: true, cancelable: true, inputType: 'insertText', data: data }));
    } catch (error) {
      // older hosts
    }

    field.dispatchEvent(new InputEvent('input', { bubbles: true, data: data }));
    field.dispatchEvent(new Event('change', { bubbles: true }));
  }

  function appendToField(field, chunk) {
    var text = String(chunk || '');
    if (!text) {
      return false;
    }

    field.setAttribute('data-um-draft', 'true');
    field.setAttribute('data-um-draft-ts', new Date().toISOString());

    if (typeof field.value === 'string') {
      field.value = (field.value || '') + text;
      dispatchFieldInput(field, text);
      return true;
    }

    field.focus();

    try {
      if (document.execCommand('insertText', false, text)) {
        dispatchFieldInput(field, text);
        return true;
      }
    } catch (error) {
      console.warn('[UnifiedMessenger] insertText failed', error);
    }

    var selection = window.getSelection();
    if (selection && selection.rangeCount > 0) {
      var range = selection.getRangeAt(0);
      range.deleteContents();
      range.insertNode(document.createTextNode(text));
      range.collapse(false);
      selection.removeAllRanges();
      selection.addRange(range);
      dispatchFieldInput(field, text);
      return true;
    }

    field.textContent = (field.textContent || '') + text;
    dispatchFieldInput(field, text);
    return true;
  }

  window.__umResetDraftStream = function () {
    window.__umDraftStreamActive = true;
    window.__umDraftStreamBuffer = '';
    return window.__umClearDraftReply();
  };

  window.__umAppendDraftChunk = function (chunk) {
    if (!window.__umDraftStreamActive) {
      window.__umDraftStreamActive = true;
      window.__umDraftStreamBuffer = '';
    }

    var field = findComposeField();
    if (!field) {
      return { ok: false, reason: 'compose-not-found' };
    }

    var text = String(chunk || '');
    if (!text) {
      return { ok: true, reason: 'empty-chunk' };
    }

    window.__umDraftStreamBuffer += text;
    return { ok: appendToField(field, text), reason: 'chunk-appended' };
  };

  window.__umFinalizeDraftStream = function () {
    window.__umDraftStreamActive = false;
    var field = findComposeField();
    if (!field) {
      return { ok: false, reason: 'compose-not-found' };
    }

    dispatchFieldInput(field, '');
    return { ok: true, reason: 'finalized', length: window.__umDraftStreamBuffer.length };
  };
})();
