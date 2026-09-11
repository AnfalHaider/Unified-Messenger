---
id: ui-contentdialog-misses-window-root-theme
date: 2026-08-28
agent: claude
title: A ContentDialog lives in a popup outside the window root and renders Light in dark theme unless shown via DialogHost
triggers: [ContentDialog, ShowAsync, dark theme, dialog white, RequestedTheme, popup, XamlRoot, DialogHost, new dialog]
files: [UnifiedMessenger/Services/DialogHost.cs]
cost: Every dialog rendered light over the dark app (ChangeIconDialog showed a white panel with its helper text invisible); five dialogs had never been opened by anyone to notice.
evidence: CHANGELOG v4.99.64; DialogHost.ApplyHostTheme called from ShowManagedAsync / ShowIfFreeAsync; observed on screen in dark theme.
status: live
---
This app sets its theme on the window root element, not on the application. A `ContentDialog` is hosted in a popup outside that root's visual subtree, so it does not inherit the theme. It falls back to the application theme, which is never set and therefore reads Light. Fixing `ThemeBrushResolver` does not reach it (see the AGENTS.md brush row, a separate cause). The fix is in `DialogHost`: `ApplyHostTheme` copies `XamlRoot.Content.ActualTheme` onto `dialog.RequestedTheme`. Never call `dialog.ShowAsync()` directly on a new dialog; route it through `DialogHost`, or it ships light-in-dark. A per-dialog fix would have missed the dialogs nobody has opened.
