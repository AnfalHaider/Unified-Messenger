---
id: bootstrap-reject-firecrawl-paid
date: 2026-09-10
agent: cursor
title: Firecrawl fails the free-only test (R4)
triggers: [firecrawl, paid, API key, .env, R4]
files: [.env, .gitignore]
cost: Billing/API dependency; key already present in gitignored .env.
status: live
---

Reject Firecrawl/Crawl4AI agent tooling: if the vendor turns off the free tier, the workflow dies. `.env` holds `FIRECRAWL_API_KEY` (gitignored). Owner should rotate the key; agents must never print or commit it.
