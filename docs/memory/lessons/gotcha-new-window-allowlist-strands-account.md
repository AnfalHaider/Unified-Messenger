---
id: gotcha-new-window-allowlist-strands-account
date: 2026-08-28
agent: claude
title: Routing new-window links by the navigation allowlist replaced the scraped session, because the allowlist spans whole registrable domains plus google.com
triggers: [NewWindowRequested, allowlist, external link, same host, same site, registrable domain, stranded account, back button, WebViewNavigationGuard]
files: [UnifiedMessenger/Services/Session/WebViewNavigationGuard.cs, UnifiedMessenger.Tests/ExternalLinkAndDownloadTests.cs]
cost: S1 — a help link or a customer-sent google.com link replaced a WhatsApp account's page with no back control, and oversight for that account stopped until a manual Refresh WebView.
evidence: CHANGELOG v4.99.66; ResolveNewWindowAction and its 14 cases in ExternalLinkAndDownloadTests.
status: live
---
`HandleNewWindowRequested` replaced the frame for any allow-listed host. The navigation allowlist is built from each platform's whole registrable domain plus OAuth hosts, so from WhatsApp Web it covered all of `whatsapp.com` and `google.com`. `MainWindow` also hides back/forward for exactly the WhatsApp family. The allowlist answers "may this frame ever load this host", not "should a new-window intent replace the scraped page". Route by same host (not same site, which keeps `faq.whatsapp.com` in-frame, the stranding case). OAuth hosts still replace the frame, or the session cookie lands in the default browser. Anything else goes to the owner's browser. The logic is now a pure function, because the old handler needed a live `CoreWebView2` and had no coverage. The live repro was blocked because screenshots mask WebView2 content.
