---
id: gotcha-kill-app-before-tests
date: 2026-09-10
agent: cursor
title: Running UnifiedMessenger.exe makes single-instance tests fail
triggers: [dotnet test, SecondInstanceActivator, mutex, kill, TriagePersistence]
files: [UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj]
cost: False reds that look like code breakage; delayed to CI when filters hide them.
status: live
---

Before the full suite: `Stop-Process -Name UnifiedMessenger -Force`. Gate command: `dotnet test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release`. Run the full suite before push — filters are for iteration only.
