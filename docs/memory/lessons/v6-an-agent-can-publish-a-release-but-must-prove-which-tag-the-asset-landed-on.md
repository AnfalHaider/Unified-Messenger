---
id: v6-an-agent-can-publish-a-release-but-must-prove-which-tag-the-asset-landed-on
date: 2026-09-24
agent: claude
supersedes: [v6-release-asset-needs-the-owners-hand]
title: An agent can publish a release and attach the installer through gh; what the update check cannot catch is the right installer on the wrong tag
triggers: [GitHub release, release asset, gh, gh auth login, device code, winget, publish v6.2.0, wrong tag, readRelease, isLatest, clobber, one-time code]
files: [v6/core/update.ts, v6/scripts/release-notes.mjs, docs/revamp/roadmap.md]
cost: The 6.2.0 installer sat on the v6.1.3 release for 40 minutes, so "6.1.3" was a 6.2.0 download. Nobody fetched it - the asset had 0 downloads when it was found.
evidence: 2026-09-24 /releases/latest still answered v6.1.3 after the release was said to be up, and its asset had changed from 144,601,066 to 144,605,339 bytes - the exact size of the 6.2.0 build
status: live
---

The old lesson said attaching the Setup needs the owner's hand. That is no longer true, and the way it became
untrue matters more than the fact:

- `winget install --id GitHub.cli --scope user` installs `gh` with no elevation. Run it through
  `Win32_Process`, or it lands in the agent shell's private `LOCALAPPDATA` and the owner's terminal never
  sees it.
- The **owner** signs in, and the agent never touches a token. `gh auth login --web` is a device flow: gh
  prints a one-time code, the owner approves it in their own browser, and the token goes to the Windows
  keyring. Afterwards `gh release create ... --latest` and `gh release upload ... --clobber` do the whole job,
  144 MB asset included.
- gh's login lives in the owner's real `%APPDATA%\GitHub CLI`, so **every** later `gh` call has to go through
  `Win32_Process` too. From the sandboxed shell it reports "not logged into any GitHub hosts", which reads
  exactly like a failed sign-in. Diagnose it by printing `whoami` and `$env:APPDATA` from inside the launched
  script before believing either story.
- Do not start the sign-in in a console window and tell the owner to find it: they could not, and a window
  nobody is looking at is a flow nobody finishes. Redirect gh's output to a file
  (`cmd /c "echo. | gh.exe auth login --web > out.txt 2>&1"`, where `echo.` answers its "Press Enter" prompt),
  then read the one-time code out of the file and put it in front of the owner directly.

**The part that actually cost something.** `core/update.ts` is careful about a release: a draft, a
pre-release, a missing `UnifiedMessenger6Setup.exe` and a download that is not GitHub's all return `null`, so
a half-finished release offers nobody anything. It has **no defence at all** against a perfectly formed asset
on the *wrong tag*. The 6.2.0 installer was uploaded to the v6.1.3 release, and every copy on 6.1.2 or older
would have been told "6.1.3 is available", handed a 6.2.0 installer, and shown the docked-page notes. The
release looked healthy from every angle the code checks.

So after publishing, verify per tag and against the bytes, not against "is there an asset":

- fetch `/releases/latest` and confirm `tag_name` is the tag just cut;
- run `readRelease` on that real JSON for the previous version (should offer the new one) **and** for the new
  version (should offer nothing);
- compare the asset's `size` with the local build's, for the new tag *and* for the tag below it. Equal sizes
  on two tags is the signature of this mistake.

Two smaller things: `gh release view --json isLatest` is not a field, so "is it latest" is answered by
`/releases/latest` and nothing else; and a publish script should check `gh auth status` and publish **nothing**
if it fails, rather than creating the release and then failing on the upload - that guard is what kept this
from becoming a second wrong release. See [[v6-release-notes-must-not-arrive-wrapped]] for the body's shape.
