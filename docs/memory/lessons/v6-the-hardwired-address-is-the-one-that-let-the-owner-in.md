---
id: v6-the-hardwired-address-is-the-one-that-let-the-owner-in
date: 2026-09-22
agent: claude
title: On the owner's own PC the gate opened on the hardwired address, 140 ms before the database could answer
triggers: [gate, PRODUCT_OWNER_EMAIL, product-owner-address, admission order, locked out, owners marker, 6.1.0 install]
files: [v6/core/admission.ts, v6/app/main.ts]
cost: None. This is the lesson that a safety valve is only proved by watching it carry the load.
status: live
---

6.1.0 put an access gate in front of the whole app. The owner has two ways in: the `owners/{uid}` marker in
Firestore, and their address baked into the build. Watching the first real install decide is what showed which
one actually does the work:

```
16:27:37.218  startup
16:27:37.358  gate   phase=admitted  because=product-owner-address
16:27:41.960  workspace-found  role=admin status=active
```

The gate opened **140 ms after startup**, four and a half seconds before the workspace check came back. The
database marker would have worked too, but it was not what let them in, and on a slow network or offline it
would not have been there in time at all. Order in `admission()` is the feature: the address is checked before
anything that needs a request.

Two things to carry:

- **A safety valve that has never carried load is a guess.** The hardwired address was added as insurance;
  reading the log is what turned it into a fact. Check `because=` after any change to who gets in.
- **Admission must not wait on the network to say yes.** Anything that can only be answered by a request
  belongs *after* the answers that cannot fail, or the first slow morning locks the owner out of their own
  business.
