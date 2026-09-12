---
id: v6-electron-cookies-are-plaintext
date: 2026-09-13
agent: claude
title: Electron stores cookies unencrypted and drops encrypted ones it cannot read
triggers: [cookies, import session, WebView2, os_crypt, encrypted_key, Local State, Instagram logged out, Google signed out, DPAPI, v10, encrypted_value]
files: []
cost: Two failed imports of Instagram and Google logins; the Local State key "being replaced" was a red herring.
status: live
---

Electron leaves cookie encryption off by default: it writes the plaintext `value` column and silently discards any `encrypted_value` it cannot decrypt. Copying a WebView2 profile (which encrypts: `v10` plus AES-256-GCM under a DPAPI-wrapped key in `Local State`) therefore loses every login cookie, whether the key is copied or the cookies are re-encrypted with v6's key. What works: decrypt with the source key, strip the 32-byte SHA-256 host prefix on cookie DB version 24 and later, write the text to `value`, empty `encrypted_value`. WhatsApp survives a plain profile copy because its session lives in IndexedDB, not cookies. Consider the `EnableCookieEncryption` fuse before shipping, since logins otherwise sit in plaintext on disk.
