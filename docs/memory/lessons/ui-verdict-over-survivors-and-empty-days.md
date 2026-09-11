---
id: ui-verdict-over-survivors-and-empty-days
date: 2026-08-29
agent: claude
title: A reply-time median only covers answered chats, and a day with no data must not draw as zero
triggers: [median, first response time, reply speed healthy, survivorship, awaiting, empty day, zero bar, chart, unmeasured, report verdict]
files: [UnifiedMessenger/Services/Analytics/BusinessReport.cs, UnifiedMessenger.Tests/BusinessReportTests.cs, UnifiedMessenger/Dialogs/WeeklyReportDialog.cs, UnifiedMessenger/Controls/CommandCenterPanel.xaml.cs]
cost: The weekly report said "Reply speed is healthy, median 1 min across 29 replies" six rows above "103 customers waiting"; the 7-day chart read as a collapse because six unmeasured days drew as zero bars.
evidence: d1f5673 (v4.99.67); BusinessReportTests.Build_MoreWaitingThanAnswered_DoesNotCallReplySpeedHealthy, Build_FewerWaitingThanAnswered_StillCallsReplySpeedHealthy
status: live
---
Chats still waiting have no first-response time, so the median is computed only over conversations that got a reply. A "healthy" verdict built on it describes whoever survived, not the business. `BusinessReport` now withholds the verdict when more customers are waiting (`AwaitingNow`) than were answered (`FrtSamplesThisWeek`), and names the population it covers. In the reply-time chart, a day with no measured replies used to draw an empty column, which looks pixel-for-pixel like a zero-height bar. Reply history had restarted, so six of seven days were empty. Those days now show a dash, with a tooltip saying unmeasured, not zero, and the heading states coverage ("1 of the last 7 days measured"). Before rendering any aggregate or verdict, ask which rows are missing by construction, and draw "no data" differently from "zero".
