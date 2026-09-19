---
id: v6-a-failed-read-must-record-its-attempt
date: 2026-09-19
agent: claude
title: A reader whose schedule keys off the last success retries a failing account on every tick; record the attempt, and leave a sign-in page alone
triggers: [google reviews, retry, backoff, reviews-failed, signed out, accountchooser, navigation loop, schedule, capturedAt, read every tick]
files: [v6/app/main.ts, v6/channels/google/index.ts]
cost: Seen on the owner's PC on the first install of the Google reader: a failing profile was read twice in three minutes, each time sending its page to Google Search and back.
status: live
---

The Google reader was due when `now - reviews[id].capturedAt` passed 30 minutes, and `capturedAt` is written only by a successful read. A profile that failed — signed out, or a page that never answered — therefore stayed due forever, and the 5-second timer started it again as soon as the previous pass ended. For WhatsApp that would cost a wasted script call; for Google it moved the page to business.google.com and back each time, which on a signed-out profile also buries Google's sign-in page under redirects. Keep a separate in-memory `reviewsTriedAt` per account, written before every attempt, and let a failure wait `REVIEWS_RETRY_MS` (10 minutes). The same shape applies to any read that navigates or costs the page anything.

Two related rules from the same install: when the page is already on `accounts.google.com`, the profile is signed out — mark it, log it, and do **not** navigate, so the owner finds the sign-in page where they left it; and log the page's `host + pathname` on a failure (never the query, which can carry an address), because "no-rating" alone did not say the page was on Google's "Choose an account" screen (`/v3/signin/accountchooser`). The first live read also showed the roadmap's claim "Google profiles are signed in" had never been checked on v6: logins do not carry over from v5's WebView2, so every account needs a v6 sign-in.
