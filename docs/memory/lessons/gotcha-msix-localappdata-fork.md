---
id: gotcha-msix-localappdata-fork
date: 2026-09-10
agent: cursor
title: Agent shells inside MSIX fork LocalAppData invisibly
triggers: [LOCALAPPDATA, MSIX, container, accounts gone, Claude Code]
files: []
cost: Hours debugging "accounts gone" while tooling and Start Menu apps used different stores.
status: live
---

Never write under `%LOCALAPPDATA%\UnifiedMessenger` from an MSIX-containerised agent shell. Reads of the "real" path can be redirected. Deploy copies from a process outside the container.
