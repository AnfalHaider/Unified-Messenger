---
id: v6-business-manage-is-non-sensitive
date: 2026-09-21
agent: claude
title: Google files business.manage as a non-sensitive scope, so listing it costs no verification and no user cap
triggers: [business.manage, scopes, consent screen, data access, restricted scope, sensitive scope, verification, user cap, google business profile api]
files: [docs/revamp/google-api-checklist.md, v6/core/google-api.ts]
evidence: 2026-09-21 the console's Data Access page lists openid, userinfo.email, userinfo.profile and business.manage all under "Your non-sensitive scopes", with the sensitive and restricted tables empty, after a save and a reload
cost: None, but the assumption in the other direction had been written into the plan twice and would have shaped the decision about Testing vs In production.
status: live
---

`https://www.googleapis.com/auth/business.manage` reads like a restricted scope — its consent line is "See, edit, create and delete your Google business listings" — and both the roadmap and the API checklist assumed it was. It is not: the Google Auth Platform console sorts scopes into non-sensitive, sensitive and restricted itself, and it puts `business.manage` in **non-sensitive**, alongside `openid` and the two `userinfo` ones.

What follows from that: listing it on the consent screen brings no verification requirement and no 100-user cap, and the app can stay **In production** (which it must, because Testing only admits named test users and would block a customer signing in to their own workspace). The real gate on reading reviews is the separate Business Profile API **access application**, which leaves the quota at 0 until Google grants it.

The general point: do not infer a scope's tier from how its consent sentence reads. Add it in the console and read back which of the three tables it lands in.
