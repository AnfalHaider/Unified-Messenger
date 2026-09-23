---
id: v6-a-missing-script-looks-exactly-like-a-blocked-installer
date: 2026-09-23
agent: claude
title: A deleted helper script and a Smart App Control block are indistinguishable from the outside — no log, no install, ReturnValue 0
triggers: [Win32_Process, ReturnValue 0, no inno.log, install did nothing, Smart App Control, um-scratch, missing script, silent failure]
files: [v6/scripts/dist.mjs]
cost: Ten minutes hunting a Code Integrity block that never happened, after the scratch folder holding the install script had gone.
status: live
---

Installing 6.1.3 did nothing: no `inno.log`, no new version, the old copy still running, and
`Invoke-CimMethod Win32_Process Create` returned **0**. That is the exact signature of a Smart App Control
block, which had happened twice that week — so the first move was to go looking for CodeIntegrity events.
There were none. `D:\um-scratch` had been deleted, so `powershell -File <missing>` started, found nothing to
run, and exited.

`Win32_Process Create` reports that **a process started**, never that it did anything — the existing lesson
`v6-win32-process-quoting-fails-silently` says this about quoting, and it is just as true of a path that no
longer exists.

Two habits that turn ten minutes into ten seconds:

- **Have the script write its own report, and treat a missing report as "it did not run"** rather than as a
  symptom of the thing you were expecting. The install script now records `setup exists:` and the counts
  before and after, so an empty run is obvious.
- **`Test-Path` the script in the same call that launches it.** One extra word in the command line, and the
  difference between "blocked by Windows" and "not there" is answered before the hunt starts.

The general shape: when a failure matches a recent, memorable cause, check the cheap alternative first. A
familiar explanation is the easiest one to confirm and the easiest one to be wrong about.
