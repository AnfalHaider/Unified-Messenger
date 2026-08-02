# ADR-007: Free browsing and save-a-site, for Custom URL tabs only

## Status

Accepted (v4.90.0)

## Context

Ferdium (Apache-2.0) models every service as a *recipe* — a service URL, a URL-validation rule, injected
JS/CSS, and a badge extractor — and lets users add arbitrary websites as services. Our
`PlatformDefinition` registry was already most of that: id, display name, default URL, icon, accent, plus
a `generic` "Custom URL (any website)" platform and a Back/Forward/Reload/Home toolbar.

What was missing was the part the owner actually asked for: the address was a **read-only label**. A
Custom URL tab could only ever show the URL it was created with. There was no way to browse to a
different page, and no way to keep one you found.

## Decision

Add an editable address bar and a **Save site** action, gated to platforms that permit free browsing.

`PlatformDefinition.AllowsCustomUrl` derives that permission from `DefaultUrl` being empty — the same
signal `ResolveStartUrl` already uses to decide a platform is not host-restricted. Deriving it rather
than adding a second flag means the address bar and the navigation guard **cannot disagree** about which
tabs are pinned.

Real service tabs deliberately keep the read-only label. They are pinned to their own site by the
navigation guard, so an address bar there would only ever produce a blocked navigation and a dead end.

`BrowserAddressNormalizer` is strict on two points, both tested:

- **http/https only.** These tabs hold live signed-in sessions. `file:` would expose local disk to a
  page; `javascript:` and `data:` are how script is smuggled into another origin. The scheme check runs
  *before* the `https://` guess, so `file:///etc/passwd` cannot be laundered into a valid-looking URL.
  `localhost` is permitted despite having no dot — pointing a tab at a local dashboard suits a local-only
  app.
- **No search fallback.** Input that is not a URL is refused with a reason rather than handed to a search
  engine. Quietly turning a typo into a Google query would ship the owner's typing off the machine, which
  is precisely the promise this app makes.

Saving asks for confirmation first: it creates a permanent sidebar entry and its own isolated WebView2
profile.

## Consequences

- Custom URL tabs behave like a real browser; found pages can be kept.
- Adding a platform still means one `PlatformDefinition` entry — no separate recipe file format was
  introduced, because the registry already carried the fields that mattered and a new format would have
  been ceremony.
- Saved sites are ordinary instances, so LRU capping, the idle reaper, workspace scope and
  right-click → Set location all apply for free.
- Not adopted from Ferdium: the per-recipe **badge extractor**. Custom sites collect no oversight
  metrics, and inventing per-site unread scraping would need live per-site DOM tuning that cannot be done
  blind.
