---
id: gotcha-publish-platform-x64
date: 2026-09-10
agent: cursor
title: Publish without -p:Platform=x64 ships a stale installer binary
triggers: [publish, installer, Platform=x64, stale, ISCC]
files: [installer.iss, UnifiedMessenger/UnifiedMessenger.csproj, .github/workflows/build.yml]
cost: App installs and runs but shows old code; releases look fine until version check.
status: live
---

Always `dotnet publish ... -p:Platform=x64`. Installer reads `bin\x64\Release\...\win-x64\publish`. Plain publish writes `bin\Release\...\publish`. Verify installed `FileVersion` after every install.
