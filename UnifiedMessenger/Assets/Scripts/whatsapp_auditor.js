(function () {
  'use strict';

  /**
   * @deprecated Use thread-status-auditor.js — retained for backward compatibility.
   */
  if (window.__umWhatsAppAuditorInstalled) {
    return;
  }

  window.__umWhatsAppAuditorInstalled = true;

  if (typeof window.__umInstallThreadStatusAuditor === 'function') {
    window.__umInstallThreadStatusAuditor(__INSTANCE_ID__, __PLATFORM__);
    return;
  }

  console.warn('[UnifiedMessenger] whatsapp_auditor.js requires thread-status-auditor.js');
})();
