---
id: v6-user-agent-product-name-breaks-whatsapp
date: 2026-09-13
agent: claude
title: A product name with a space slipped past the user-agent filter and WhatsApp stopped building its stores
triggers: [user agent, setUserAgent, productName, WhatsApp no-store, update your browser, installed app reads nothing, packaged]
files: [v6/app/main.ts]
cost: The first installed build read nothing from WhatsApp.
status: live
---

Electron inserts `<app name>/<version>` before `Chrome/` and `Electron/<v>` before `Safari/`. A token regex such as `/ (name|Electron)\/\S+/` misses a name that contains a space ("Unified Messenger/6.0.0"); WhatsApp Web then treats the browser as unsupported and never builds its stores (reader stage `no-store` forever, and no QR code). Cut everything between `(KHTML, like Gecko) ` and `Chrome/`, and remove ` Electron/\S+`. Re-check whenever `name` or `productName` changes.
