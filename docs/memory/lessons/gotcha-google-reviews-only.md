---
id: gotcha-google-reviews-only
date: 2026-09-10
agent: cursor
title: Google channel is reviews and Q&A only forever
triggers: [google, business messages, reviews, awaiting reply]
files: [docs/MASTER-PLAN.md]
cost: Weeks of wrong plumbing for a dead Google chat product.
status: live
---

Google Business Messages shut down July 2024; historic chats deleted. Do not add awaiting-reply or message-count metrics for Google. Rating/lifetime totals come from Search merchant view via `GoogleReviewSnapshotService.ProfileRating`.
