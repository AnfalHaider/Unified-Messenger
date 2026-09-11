---
id: test-injected-script-tests-do-not-execute
date: 2026-09-03
agent: claude
title: Green JS tests do not mean the injected script runs; C#-embedded scripts are not even syntax-checked
triggers: [injected script, whatsapp-adapter.js, Assert.Contains script, node --check, RatingKickoff, PageHelpers, CDP, selector migration, ReferenceError]
files: [UnifiedMessenger.Tests/InjectedScriptSyntaxTests.cs, UnifiedMessenger.Tests/GoogleSelectorManifestTests.cs, UnifiedMessenger/Services/Oversight/GoogleReviewSnapshotService.cs, UnifiedMessenger/Assets/Scripts/whatsapp-adapter.js]
cost: The A3 migration left an unbalanced brace that made the whole WhatsApp adapter throw on load (scraping completely dead) while 1940 tests passed and the build was published and installed. A6 then broke the Google rating script, which surfaced only as state:'error'.
evidence: InjectedScriptSyntaxTests remarks; GoogleSelectorManifestTests.EveryScriptThatUsesTheManifestAlsoDefinesTheHelper; both caught only by driving the live app over CDP.
status: live
---
The script tests assert on source text (`Assert.Contains("__umConfig", script)`), which a broken file passes. `InjectedScriptSyntaxTests` now runs `node --check`, but only on `Assets/Scripts/*.js`. Scripts built as C# string consts (e.g. `GoogleReviewSnapshotService.RatingKickoff`) are not parsed. A helper defined in another const (`PageHelpers`) fails at runtime only on pages where no other script ran first, so a probe on a warm tab looks fine. After changing any injected script, check it in the running app over CDP (see AGENTS.md "Need to run JS inside the app's WebView2"). Confirm its globals exist on a fresh page load, not on a tab another script already initialised.
