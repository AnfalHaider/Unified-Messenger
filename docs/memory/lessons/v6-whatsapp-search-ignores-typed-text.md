---
id: v6-whatsapp-search-ignores-typed-text
date: 2026-09-13
agent: claude
title: WhatsApp's chat-list search no longer reacts to a set value, so a chat off screen is opened through Cmd.openChatBottom
triggers: [open chat, focus, focus conversation, search, not-found, __umFocusConversation, openChatBottom, WAWebCmd, ChatCollection, typed value, React input, Instagram search, filter did not apply]
files: [v6/channels/whatsapp/whatsapp-focus.js, v6/channels/instagram/instagram-focus.js]
cost: The first 3.2 build found every drawn chat and no other; a probe that only checked the input's value would have passed it.
status: live
---

Measured on the owner's live WhatsApp pages (2026-09-13): setting the chat-list search `<input>`'s value through the native setter and firing a bubbling `input` event, which v5 relied on, leaves the list exactly as it was (same 20 rows, by name or by number). Only the 20 or so drawn rows can be clicked. A chat that is not drawn opens through WhatsApp's own command, the same action a row click performs: `require('WAWebCmd').Cmd.openChatBottom({ chat })`, with `chat = require('WAWebChatCollection').ChatCollection.get(conversationKey)`. The live signature destructures `{ chat, chatEntryPoint, threadId }`; passing a bare chat does not work. The open chat's header was found by `#main header [data-testid="conversation-info-header"] span`. Instagram Direct's search does react, but it once ignored text set after the box was cleared while focused, so focus it before typing. The general rule: to prove a page reacted to something written into a field, measure the page (rows, results, a header), never the field's own value.
