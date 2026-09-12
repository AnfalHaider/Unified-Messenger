---
id: v6-agent-shell-redirects-appdata-and-installs
date: 2026-09-13
agent: claude
title: Electron started from the agent shell uses a sandbox copy of %APPDATA%, and installers run there land in a private copy too
triggers: [agent shell, MSIX, APPDATA, Roaming, unified-messenger-v6, install, Start Menu, Win32_Process, launch app, npm start]
files: [v6/scripts/dist.mjs]
cost: A day of v6 runs wrote to a data folder the owner's app never saw; the real folder did not exist yet.
status: live
---

The MSIX redirection covers Roaming as well as Local AppData. `electron .` or a Setup.exe started from Claude's shell reads and writes `...\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\...`. To run or install v6 for the owner, start it through `Invoke-CimMethod -ClassName Win32_Process -MethodName Create`, and read its files the same way. Self-tests may run inside the sandbox, whose copy is disposable, but never judge the owner's logins or data from there.
