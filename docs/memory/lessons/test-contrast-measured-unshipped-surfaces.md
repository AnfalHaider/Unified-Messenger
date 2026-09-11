---
id: test-contrast-measured-unshipped-surfaces
date: 2026-08-28
agent: claude
title: The contrast tests measured hard-coded surfaces the app does not ship, hiding two AA failures
triggers: [WCAG, contrast ratio, StatusContrastTests, WcagContrast, surface colour, sunken surface, Tokens.xaml, AA 4.5, light theme]
files: [UnifiedMessenger.Tests/WcagContrast.cs, UnifiedMessenger.Tests/StatusContrastTests.cs, UnifiedMessenger/Themes/Tokens.xaml]
cost: Two status colours drawn as text shipped below AA on the light sunken surface (UmStatusMuted 4.15:1, UmStatusDanger 4.34:1) while the contrast suite was green.
evidence: CHANGELOG v4.99.60 (commit 490530b); comment in WcagContrast.cs about the old "#FFFFFF" / "#2D2D30" / "#1E1E1E" surfaces; test EveryStatusColourIsReadableOnEverySurfaceOfItsOwnTheme.
status: live
---
`WcagContrast` hard-coded `#2D2D30`/`#1E1E1E` for dark and only `#FFFFFF` for light. Neither dark value appears in `Tokens.xaml`, and white is the most forgiving light surface. The sunken surface was never measured, so two real failures sat at full opacity. The tests also held Muted to the 3:1 dot bar, but `ReviewDesk.UrgencyBrush` uses it as text. Surfaces are now read from `Tokens.xaml` for every surface of each theme. A related trap: an assertion that status colours survive 0.65 opacity failed everywhere. No XAML element pairs a status foreground with `Opacity`, so it was withdrawn. Check that a pattern actually occurs before pinning it.
