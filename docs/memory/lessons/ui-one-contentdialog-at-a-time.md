---
id: ui-one-contentdialog-at-a-time
date: 2026-08-17
agent: claude
title: WinUI allows one ContentDialog at a time; unserialised ShowAsync calls crashed the app or silently swallowed prompts
triggers: [ContentDialog, ShowAsync, 0x80000019, COMException, DialogHost, startup prompts, onboarding, fire-and-forget, dialog swallowed, HasCompletedWorkspaceOnboarding]
files: [UnifiedMessenger/Services/DialogHost.cs, UnifiedMessenger/Services/Shell/ShellController.cs, UnifiedMessenger/Dialogs/SettingsRecoveryDialog.cs]
cost: An unhandled COMException 0x80000019 reached the global handler on the owner's machine (31 unguarded call sites). Separately, colliding startup prompts silently dropped the onboarding wizard, whose finally block then marked it completed for good.
evidence: 9cdb3b6 (DialogHost introduced, all call sites routed through it); c4bef4a F-DURA-03 (`_ = MaybeShowWorkspaceOnboardingAsync(); _ = MaybePromptPinToTaskbarAsync();` raced; now sequenced recovery -> onboarding -> pin).
status: live
---
A second `ContentDialog.ShowAsync` while one is open throws. Sites that catch and log do not crash, but the prompt never appears, which is worse when the dialog's `finally` records "done". The collision is most likely after a settings reset, when every "show once" flag is due in the same session. Never call `ShowAsync` directly or fire-and-forget a dialog. Go through `DialogHost`, which queues rather than drops and bounds the wait at two minutes, so a nested show becomes a logged failure instead of a frozen window. When diagnosing a dialog that "never appeared", log the dialog's result after the await. Its absence distinguishes "still open" from "never shown".
