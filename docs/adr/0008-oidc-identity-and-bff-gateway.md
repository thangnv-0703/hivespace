# ADR-0008: OIDC authorization server and gateway as BFF

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** FR-AUTH-001..027, FR-ROLE-001..005, EIR-COM-001, EIR-SW-006, FR-SRCH-011, NFR-SEC-001..005, NFR-SEC-009, NFR-SEC-014, NFR-SEC-015, NFR-REL-008, CON-8, UC-001, UC-002, UC-022, UC-025, UC-026; OQ-32, OQ-33

## Context
The builder wants a standards-based OAuth 2.1 and OIDC flow so more social providers can be added later, and wants tokens kept out of the frontend. The SRS needs access and refresh tokens (FR-AUTH-009), lockout, Google sign-in with auto-link, and a gateway that verifies tokens and forwards identity (FR-AUTH-015, FR-AUTH-016).

## Decision
- **Identity is an OIDC authorization server** using OpenIddict (open source) with ASP.NET Core Identity pieces for users. Endpoints: authorize, token, revocation, JWKS, discovery. ES256 JWTs; access token 15 minutes, refresh token 7 days with rotation (within NFR-SEC-004). Passwords: argon2id behind one hasher abstraction (NFR-SEC-002).
- **Hosted pages** for sign-in, register, verify, reset and change password, served by Identity in vi and en.
- **The gateway is the BFF**: a confidential OIDC client using code flow with PKCE. Tokens live inside an **encrypted HttpOnly, Secure, SameSite cookie**; data-protection keys are stored in blob storage so the gateway stays stateless. It refreshes tokens, relays the access token to services, and exposes `/bff/login`, `/bff/logout`, `/bff/user`. Calls under `/v1` require a custom header as CSRF protection. Sign-out revokes the refresh token.
- **Services validate the JWT** through JWKS as well (defence in depth) and enforce role and ownership.
- **Social login is pluggable:** an `IExternalLoginProvider` registry in Identity federates to upstream OIDC providers. Google first (code, PKCE, state, nonce, ID-token validation, scopes openid email profile). Auto-link to an existing account only when the provider asserts a verified email.
- Roles and `email_verified` are claims; shop registration and email verification force a refresh through the BFF so claims update.
- **Interpretation:** access tokens are not individually revocable; revocation takes effect at refresh (R-8).

## Options considered
- **OpenIddict plus BFF (chosen)** — standard flows, open source, teaches the protocol, pluggable providers. Cons: hosted login UI and protocol surface to build and secure.
- **Own JWT endpoints and SPA-held tokens** — least work; tokens reachable by XSS; non-standard; harder to add providers.
- **Duende IdentityServer or Keycloak** — full-featured; licensing (Duende) or a heavy extra container (Keycloak) that does not fit scale-to-zero and the budget.

## Consequences
Easier: adding Facebook or Apple later, no token in JavaScript, one place for session policy. Harder: more initial work (R-11), cookie size limits (tokens kept lean), and the gateway becomes security-critical. The gateway keeps an in-memory rate limiter (R-5). Revisit if provider count grows enough to justify a managed identity service.
