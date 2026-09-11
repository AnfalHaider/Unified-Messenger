---
id: ui-one-label-two-populations
date: 2026-08-29
agent: claude
title: One label over two populations reads as a wrong number even when both figures are right
triggers: [label, coverage, all accounts, ChannelScope, SLA met, notification badge, unread count, donut, excluded accounts, scope line]
files: [UnifiedMessenger/Services/Analytics/ChannelScope.cs, UnifiedMessenger/Pages/AnalyticsPage.xaml.cs, UnifiedMessenger/Presenters/NotificationFeedPresenter.cs]
cost: An Analytics donut captioned "across your accounts" was drawn from 5 of 8 accounts (all three Google accounts silently missing); "SLA met" showed two different figures on one page; the rail badge read 21 beside a panel saying "No notifications yet."; the Reviews page showed at most eight as the Unanswered total.
evidence: 89bd2ab (v4.99.73, ChannelScope), d1f5673 (badge vs panel); CHANGELOG.md "The Reviews page showed the loaded reply queue's length under Unanswered — at most eight"; ChannelScope.cs doc comment names the defect class the v4.99.46-47 audit fixed three times
status: live
---
The WhatsApp IndexedDB pipeline feeds Analytics and the business report, and both captioned their figures as covering every account. In the notification case, the badge is `NotificationHub.TotalUnreadCount` (unread messages across unmuted accounts), while the panel lists the alerts the hub raised. The Reviews page did the same with a page of data: it showed the loaded queue's length, capped at eight, under "Unanswered", while the sidebar badge used the real count. When a figure is computed over a page, say "of the N loaded". Each number was right; placed side by side under one noun, they read as one quantity disagreeing with itself. The fixes change wording, not counts. A single `ChannelScope` helper feeds both the page and the export, because two surfaces each computing "which accounts is this about" is how they drift. The scope sentence names what is excluded, and it still shows "covers all N accounts" on good days, so its absence can't be read as coverage. Before a new label, badge, or KPI ships, check that everything it sits beside counts the same population.
