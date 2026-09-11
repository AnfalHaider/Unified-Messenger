---
id: gotcha-semaphore-is-not-a-throttle
date: 2026-08-21
agent: claude
title: A SemaphoreSlim blocks only concurrent passes; a Loaded-handler scrape re-fired on every dashboard redraw hit each Google account six times at startup
triggers: [SemaphoreSlim, throttle, Loaded handler, rescrape, Google reviews scrape, rate limit, freshness floor, startup traffic, reload panel, force resync]
files: [UnifiedMessenger/Services/Oversight/GoogleReviewSnapshotService.cs, UnifiedMessenger.Tests/ReviewScrapeThrottleTests.cs]
cost: Six scrapes per Google account in the two minutes after launch: real, rate-limitable traffic against the owner's own Google account.
evidence: c34d5b8 (measured 6 -> 1 per account); ReviewScrapeThrottleTests including "The freshness floor must stay below the panel's 5-minute auto-refresh, or that timer becomes a no-op."
status: live
---
The reviews panel (then `ReviewHealthPanel`) started a scrape from `Loaded`, and the dashboard rebuilt it on every alert-monitor tick and adapter-health change. The existing semaphore did nothing, because a three-second pass is not concurrent with the next one. Put the throttle in the service, not the caller, so the next caller inherits it. Stamp the attempt, not the result: throttling on a cached success leaves a failing account scraped on every reload. Use two tiers, 45s after a failed attempt and 4 min after a success, because a flat floor left the card empty after every cold start. Owner-initiated Re-sync bypasses it with `force`. Pin the floor below any auto-refresh timer it sits under.
