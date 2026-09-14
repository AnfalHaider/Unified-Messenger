---
id: v6-reports-follow-the-title-bar-scope
date: 2026-09-14
agent: claude
title: The title bar's location filter lives in the renderer, so anything main builds must be told it; counts must come from the whole queue
triggers: [location filter, branch, scope, All locations, reports not filtered, title bar counts, queueByLocation, set-scope, export by location]
files: [v6/ui/App.tsx, v6/app/main.ts, v6/app/view-model.ts, v6/ui/charts.tsx]
cost: The owner found Reports ignoring the chosen branch; the title bar's own counts would have been wrong past 60 waiting.
status: live
---

The scope buttons (All locations, then each location) were React state in `App.tsx`, passed only to the line and the dock. Reports, the weekly report and every export are built in the main process, so they silently covered every location whatever was chosen. The renderer now sends `setScope` on every change; main keeps `scope`, passes it in the view-model context, and `reportAccounts(config, scope)` / `inScope` decide which accounts and waiting chats a report covers; export file names carry the location. Anything new that main computes for a screen with the scope bar must read `ctx.scope`. The title bar counted each location from `state.queue`, which is cut to 60 rows (lesson `v6-screen-slice-hides-newest-waits`); it now uses `queueByLocation`, counted from the whole queue. Also fixed in passing: the line's tokens were keyed by customer name while the selection is keyed by conversation, so the selected token never highlighted; and reply times imported from v5 reach back before the day records begin, so the coverage sentence names both dates.
