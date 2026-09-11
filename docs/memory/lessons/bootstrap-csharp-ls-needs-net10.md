---
id: bootstrap-csharp-ls-needs-net10
date: 2026-09-10
agent: cursor
title: csharp-ls 0.27 requires .NET 10 SDK even for net8 projects
triggers: [csharp-ls, LSP, language server, dotnet sdk, net10]
files: [UnifiedMessenger.sln]
cost: Proposed csharp-ls while only .NET 8 was installed; tool install would fail or mislead.
status: live
---

Current `csharp-ls` NuGet (0.27.0) needs the .NET 10 SDK on the machine. Project TFM can remain `net8.0-windows10.0.19041.0`. Pin: csharp-ls 0.27.0 `+d48ab7781176d2d369ceeb31d986503c2ad493e2`. Diagnose loads the solution; WinUI metadata warnings may appear until NuGet restore succeeds.
