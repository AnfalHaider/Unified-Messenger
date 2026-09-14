---
id: v6-calls-must-be-caught-while-latest
date: 2026-09-14
agent: claude
title: A missed call exists in the snapshot only while it is the chat's latest message, so it must be written down on the read that sees it
triggers: [missed call, call returned, calls.json, lastCallOutcome, call_log, callback, snapshot only latest message, recordCalls]
files: [v6/core/calls.ts, v6/app/main.ts, v6/app/view-model.ts]
cost: Found while designing 4.8; a report built from the snapshot alone would forget every call the moment anyone replied.
status: live
---

The readers keep each chat's latest message only. A missed call is visible as `lastMessageType: call_log` with a non-answered `lastCallOutcome` until the next message in that chat, ours or the customer's, and then it is gone from every snapshot. So "was the call returned" cannot be computed from the snapshot afterwards: `recordCalls` in `core/calls.ts` records the call on the read that sees it and marks it returned on a later read that shows our message or call after the call time. Rules that follow: a customer writing or calling again is not a return; one reply returns every open call in that chat; a first read brings in only calls from the last 7 days so an install does not flood the list; `calls.json` keeps keys and times only, and the screens take the caller's name from the snapshot when drawing. Reads run every minute, so a reply and then a new customer message inside one minute can hide the reply; such a call stays "not returned" until we write again. Reports › Missed calls, the Missed calls fact, the digest line and the not-returned alert all read these records; the older per-day count in `history.json` still feeds the CSV and weekly figures, so the two can differ by the 7-day first-read window.
