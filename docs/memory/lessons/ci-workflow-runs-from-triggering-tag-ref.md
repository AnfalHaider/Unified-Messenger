---
id: ci-workflow-runs-from-triggering-tag-ref
date: 2026-08-27
agent: claude
title: Re-running a tag's CI run uses the workflow file at the tag, not the fix pushed after it
triggers: [github actions, re-run, release notes, tag, workflow change, gh release edit, build.yml]
files: [.github/workflows/build.yml, CHANGELOG.md]
cost: Told the owner a re-run would fix v4.99.47's release notes in place; it could not, so that release shipped with boilerplate notes and needed a hand edit plus a retraction.
evidence: Run #207 was triggered by tag v4.99.47 (commit 4e6ce9b), but the CHANGELOG-notes change in build.yml's "Create or update" release step is commit a317d5e, pushed after the tag.
status: live
---
GitHub Actions runs the workflow YAML **from the ref that triggered the run**. Re-running a tag's run replays the workflow as it was at that tag. The agent believed a re-run of the v4.99.47 run would take the new `gh release edit --notes-file` path and pull notes from `CHANGELOG.md`. It would only have produced the old boilerplate again. A workflow change first takes effect on the next tag. To fix an already-published release, edit its description by hand (paste the `## vX.Y.Z` CHANGELOG section); never move a published tag. If a release step must behave differently for a tag, land the workflow change before creating that tag.
