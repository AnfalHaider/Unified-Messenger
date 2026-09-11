---
id: ui-combobox-rebuilt-in-own-selectionchanged-hangs
date: 2026-08-29
agent: claude
title: Clearing a ComboBox's Items from inside its own SelectionChanged hangs the app; a suppress flag does not help
triggers: [ComboBox, SelectionChanged, Items.Clear, hang, freeze, branch filter, BranchBox, PopulateBranchBox, re-entrancy]
files: [UnifiedMessenger/Pages/AnalyticsPage.xaml.cs, UnifiedMessenger/Pages/ReportsPage.xaml.cs]
cost: v4.99.71 shipped a branch filter on Analytics that hung the app on the first branch switch; the owner reported it immediately.
evidence: CHANGELOG v4.99.72 entry "Switching branch on Analytics hung the app"; comment at AnalyticsPage.xaml.cs near BranchBox_SelectionChanged.
status: live
---
`BranchBox_SelectionChanged` called `Refresh()`, which called `PopulateBranchBox()`, which cleared and rebuilt `BranchBox.Items`. So the ComboBox was being rebuilt from inside its own `SelectionChanged` handler. The existing `_suppressRangeChange` guard did not prevent it: `Items.Clear()` re-enters WinUI's selection machinery whatever the handler does with the event. Populate a selector once (on navigation) when its items cannot change while the page is visible, and keep the refresh path from touching its items. `ReportsPage` already did this; only Analytics had the cascade. An older CHANGELOG entry (the sidebar scope-switch startup crash, a ComboBox firing `SelectionChanged` during `InitializeComponent`) is the same family.
