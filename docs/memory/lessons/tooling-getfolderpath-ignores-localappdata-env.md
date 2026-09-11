---
id: tooling-getfolderpath-ignores-localappdata-env
date: 2026-08-28
agent: claude
title: Setting $env:LOCALAPPDATA does not give the app an empty profile
triggers: [LOCALAPPDATA, GetFolderPath, empty profile, first run, reproduce CI, user data root, fake profile, repro]
files: [UnifiedMessenger/Services/ApplicationPaths.cs, docs/remaining-work.md]
cost: An "empty-profile reproduction" of the ui-smoke failure read the owner's real 8-account profile; a fix was built on it and nearly pushed
evidence: docs/remaining-work.md §0.4 diagnosis #2; probe printed env LOCALAPPDATA=Temp\um-envprobe vs GetFolderPath(LocalApplicationData)=C:\Users\anfal\AppData\Local
status: live
---
The belief was that pointing `$env:LOCALAPPDATA` at a temp folder before launch would make the app start with no accounts, the way CI does. In fact `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` resolves through the shell known-folder API and ignores the variable. `ApplicationPaths.DefaultUserDataRoot` is built on that call, so the app kept reading the real store. The run still produced a plausible failure (exit 3, empty `sample=`), which made the invalid repro look like confirmation. Before trusting any fake-environment repro, print the path the process actually resolves. In tests the only redirect is `ApplicationPaths.UserDataRootOverrideForTests`. The shipped exe has no environment override.
