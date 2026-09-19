---
id: v6-firestore-emulator-needs-java-21
date: 2026-09-19
agent: claude
title: firebase-tools 15's Firestore emulator refuses Java 17; a portable JDK 21 in %USERPROFILE%\.jdks is used instead
triggers: [firebase emulator, firestore emulator, java 21, rules:test, firebase-tools, JAVA_HOME, temurin, tar, git bash]
files: [v6/scripts/rules-run.mjs, .github/workflows/v6.yml]
cost: One failed run; the fix changed nothing system-wide.
evidence: 2026-09-19 "firebase-tools no longer supports Java version before 21" with Temurin 17.0.20.1 on PATH; passes with Temurin 21.0.12.1 from %USERPROFILE%\.jdks
status: live
---

The owner's PC has Temurin 17 on PATH, and `firebase emulators:exec` stops with "firebase-tools no longer supports Java version before 21". Rather than install a second system JDK, a portable Temurin 21 zip (from the Adoptium API, SHA-256 checked against the API's own checksum) is unpacked into `%USERPROFILE%\.jdks`; `%USERPROFILE%` is not under the agent shell's AppData redirection. `scripts/rules-run.mjs` uses `java` when it is 21+, else JAVA_HOME, else the newest 21+ found in `.jdks`, and puts it first on PATH for the emulator only. CI uses `actions/setup-java` with 21. Two traps on the way: Git Bash's `tar` reads `C:\...` as a remote host ("Cannot connect to C: resolve failed"), so unpack with `%SystemRoot%\System32\tar.exe`; and `spawnSync` with `shell: true` and an argument array drops the quotes around `"node --test cloud/rules.spec.ts"`, so pass the whole command as one string.
