---
id: bootstrap-python-store-stub
date: 2026-09-10
agent: cursor
title: Confirm Python with --version, not path presence
triggers: [python, python3, runtime, store stub, R1]
files: []
cost: Mis-detected Python as present; Store stub exits 9009 and breaks hooks.
status: live
---

On Windows, `python`/`python3` may resolve to Microsoft Store stubs. `command -v` / Get-Command can look fine while `--version` fails. After install, prefer `python` (3.12.10 here). `python3` may still be the stub until App Execution Aliases are disabled.
