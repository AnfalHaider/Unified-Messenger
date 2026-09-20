# Google Business Profile API: the checklist

Decided with the owner on 2026-09-19. Google blocks sign-in inside the app's own pages ("Couldn't sign you in. This
browser or app may not be secure"), so reviews will come from Google's official Business Profile API instead of
the reviews page. It costs nothing: Google lists quotas for these APIs, not prices, and no billing account is
needed. **We do this together at the end, after the rest of the roadmap and the first v6 update to v5 users.**
The owner signs in to Google Cloud; Claude does the configuring, asking before each step that changes anything.

## What the owner needs ready

- [ ] Signed in to **Google Cloud Console** with the Google account that owns project **`unified-messenger-5549a`**.
- [ ] That account, or one we add, is an **owner or manager of the three Business Profiles** (DHA-2, F-11, Men DHA-2),
      and the profiles are **verified**.
- [x] A **website** for the product: built in 6.6 (`v6/site/`), served free by Firebase Hosting at
      `https://unified-messenger-5549a.web.app` — home and privacy policy.
- [ ] An **email address on the same domain** as that website. Google's access form checks that the two match, and a
      `web.app` address cannot have email, so this needs a domain the owner buys (and then a custom domain on the same
      free hosting). Not needed for the consent screen in Testing, only for the Business Profile API access form.
- [ ] About thirty minutes for the setup, then waiting for Google's approval email (usually days to a couple of
      weeks).

## What Claude configures, with the owner's approval at each step

1. **Enable the APIs** in the project: My Business Account Management API and My Business Business Information API.
   (The reviews API, "Google My Business API" v4, becomes available to enable only after approval.)
2. **OAuth consent screen** (Google Auth Platform): app name *Unified Messenger*, support email, logo, homepage,
   privacy policy, authorised domain. Audience **External**, left in **Testing** with the owner's Google accounts
   as test users. Testing needs no Google review and allows up to 100 test users, enough for the owner's own
   profiles from day one.
3. **Scopes**: `https://www.googleapis.com/auth/business.manage` (Google offers no read-only scope for these APIs;
   its consent text reads "Manage your Business Profile on Google", and the app description must say plainly that
   the app only reads) plus `openid` and `email`, so the app can show which Google account is connected.
4. **Credentials**: one OAuth client of type **Desktop app**. Its id goes into the app's build as a local file that
   is never committed (same rule as `firebase-config.json` and `oauth-client.json`). A desktop client's secret is not
   confidential by Google's own definition; sign-in uses PKCE and a loopback redirect.
5. **Access request**: the Business Profile API contact form (`support.google.com/business/contact/api_default`,
   "Application for Basic API Access"): company name, website, contact email on that domain, **project number**,
   and a use-case description. Claude drafts the description: *a desktop app that reads a business's own reviews,
   ratings and reply status for its own locations, on the owner's PC; it posts nothing and stores nothing off the
   PC.*
6. **Confirm approval**: in the project's quotas, the Business Profile APIs change from **0** to **300 requests per
   minute** once approved. Then enable the v4 API.

## What the app does (built before then, switched off until approval)

- **Connect Google** on each Google Business account opens the owner's normal browser (Google's approved way for a
  desktop app, so the "couldn't sign you in" block does not apply), and Google hands back a token.
- The token is kept **encrypted on this PC** (Electron `safeStorage`) and never leaves it. Calls go straight from the
  PC to Google: no server of ours, no oversight data anywhere else.
- Every half hour it reads each location's reviews through `accounts.locations.reviews.list` (every review, not just
  the latest 50, with star ratings, reply status, the average rating and the total), into the same `core/reviews.ts`
  records and the same Reviews screen.
- Quota: 300 requests a minute, shared by every customer of the product. A location read every half hour uses a small
  fraction of one; roughly a few thousand locations fit before asking Google for more, which is also free.

## Before other businesses use it (later)

- [ ] Move the consent screen from Testing to **In production** and submit it for Google's review: homepage, privacy
      policy, the authorised domain verified in Search Console, and possibly a short video of the sign-in. Claude
      confirms at that point whether `business.manage` needs only the free brand review or the paid security
      assessment Google reserves for "restricted" scopes (it is believed to be the former).
- [ ] Ship the update that switches the API reader on.
