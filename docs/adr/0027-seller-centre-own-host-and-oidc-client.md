# ADR-0027: Seller centre on its own host, with its own OIDC client

**Status:** Accepted
**Date:** 2026-11-21
**Requirements addressed:** FR-AUTH-013, FR-AUTH-015, FR-AUTH-016, EIR-COM-001, NFR-SEC-001; UC-002
**Amends:** ADR-0008 (the "one origin for both SPAs" reading of it)

## Context
The gateway served both SPAs from one origin: storefront at `/`, seller centre at `/seller`, one BFF cookie and one OIDC client (`hivespace-gateway`). Most marketplaces give the seller side its own host (for example `banhang.shopee.vn`), and the builder wants that layout. The seller centre has no sign-in wiring yet (FEAT-009 adds it), so the change is cheapest now.

## Decision
- **Two hosts, one gateway.** Production: `hivespace.vn` (storefront) and `seller.hivespace.vn` (seller centre). Local: `http://localhost:5080` and `http://seller.localhost:5080`; browsers resolve `*.localhost` to loopback, so no hosts file is needed. The same gateway and ingress serve both (EIR-COM-001 still holds). Development YARP routes and the production SPA fallbacks select the app by `Host`; each app is built with base path `/`.
- **Two confidential OIDC clients:** `hivespace-storefront` and `hivespace-seller`, each with its own secret, redirect URI (`{host}/bff/callback`, seller `{host}/bff/callback/seller`) and post-logout URI (`{host}/`). The gateway registers one cookie scheme and one OIDC scheme per client; a policy scheme picks them by `Host`, falling back to the storefront.
- **One sign-in, two sessions (SSO).** Each host has its own host-only BFF cookie (`hs.bff.stf`, `hs.bff.sel`). Identity (`/connect/*`, the `hs.idp` cookie, the hosted `/signin` page) stays on the storefront host, which remains the issuer. A user who signed in on one host reaches the other through redirects only: the authorize request finds `hs.idp` and issues tokens for the second client without a password. No `Domain=` cookie and no CORS.
- **Sign-out is per app.** It revokes that app's refresh token, clears its BFF cookie and ends the Identity session (`hs.idp`), then returns to `/` of the same host. The other app keeps working until its next refresh fails (the ADR-0008 interpretation, R-8). Single logout across both apps is a follow-up.
- `returnUrl` stays a local path, now meaning a path on the calling host. The `Sec-Fetch-Site: same-origin` check on `/bff/logout` is unchanged: each app posts to its own host.

## Options considered
- **Own host plus own OIDC client (chosen)** — matches common marketplaces, keeps cookies host-only, and teaches SSO between OIDC clients. Cons: two schemes in the gateway, sign-out is not global yet.
- **Own host, one shared cookie (`Domain=.hivespace.vn`)** — one session for both apps. Every subdomain then receives the session cookie, the `__Host-` prefix is impossible, and local development needs a shared parent domain.
- **Keep `/seller` on one origin** — least work and already built. Rejected because the builder wants the separate seller host.

## Consequences
Easier: the seller app can get its own scopes, audience or cookie policy later; each app's session is isolated. Harder: two client secrets to manage; theme and language in `localStorage` no longer carry across the two hosts (a signed-in user's language still comes from the profile, FEAT-002). Before the first deploy, the gateway must honour forwarded headers (`X-Forwarded-Proto`, `X-Forwarded-Host`) so the per-host `redirect_uri` is right behind Container Apps ingress, and both custom domains must be bound (initial deployment). Revisit with single logout (OIDC back-channel or front-channel) when a seller flow needs it.

**What it teaches:** SSO across OIDC clients through the authorization server's own session; host-based routing in YARP and endpoint routing; ASP.NET Core authentication scheme selection.
