---
id: v6-google-console-work-needs-the-window-in-front
date: 2026-09-21
agent: claude
title: Console work through the browser pane needs the app window in front; a hidden window renders at innerWidth 0 and every click by coordinate is lost
triggers: [google cloud console, browser pane, screenshot timed out, innerWidth 0, click coordinates, consent screen, data access, scopes, oauth branding]
files: [docs/revamp/google-api-checklist.md]
evidence: 2026-09-21 the scopes dialog was filled correctly three times (business.manage added to the table, Update pressed) and each Save was lost; window.innerWidth read 0, then 1024, then 1280, then a collapsed viewport, while Branding saved fine earlier when the pane was rendering
cost: Three rounds on one two-click form, after the rest of the console work went through first time.
status: live
---

The Google Cloud console is a heavy Angular app, and the browser pane only renders it while Claude's window is actually in front. When the window is behind another, `computer{action:"screenshot"}` times out and `window.innerWidth` reads **0**, so every position computed from a `getBoundingClientRect()` is null or wrong and clicks land nowhere. Work that is one form-fill and a Save then fails silently: the form shows the right values, and the Save never happens.

What this means in practice:

- **Read state with `get_page_text` or `javascript_tool`**, which keep working when rendering has stopped; only *clicking* needs the window in front.
- **Verify a save by reloading the page and reading the values back**, never by the absence of an error. Branding (app name, home page, privacy policy, authorised domains, developer contact) survived a reload; Data Access did not, and the difference was only whether the pane was rendering.
- **Scale matters too:** the pane's coordinate frame is not the page's viewport. Convert with `800 / window.innerWidth`, and re-read it each time — it changed between 1024 and 1280 in one session.
- When the window cannot be brought forward, write down exactly which two clicks are left and hand them back, rather than retrying a form that cannot save.
