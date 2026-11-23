# Gateway (.NET, YARP)

The only public entry point (EIR-COM-001, ADR-0008). It routes `/v1/<service>/…` to the services, owns the identity and correlation headers, and serves both SPAs from one origin.

System-wide setup is in the [root README](../README.md).

| | |
| :-- | :-- |
| Routes | `src/Gateway/appsettings.json` (`ReverseProxy`); development adds the Vite dev-server routes in `appsettings.Development.json` |
| Transforms | `src/Gateway/Proxy/GatewayTransforms.cs` — strips client-sent `X-User-*` headers, forwards the correlation ID |
| Identity | **Stub**: in Development a fixed `dev-user` is asserted. The BFF cookie session and token relay replace it in FEAT-001 |
| SPAs | Production: the Dockerfile builds `web/` and copies it into `wwwroot`. Development: YARP forwards to Vite |
| Port | http://localhost:5080 locally |

## Rate limits (NFR-SEC-005, FEAT-005)

| Setting | Default | Meaning |
| :-- | :-- | :-- |
| `RateLimits:PermitLimit` (`RateLimits__PermitLimit`) | `10` | Requests per window. Policy `auth` counts per client IP (`/v1/users*`, Google's callback), `auth-user` per signed-in user (`/v1/me/password*`) |
| `RateLimits:Window` | `00:01:00` | The fixed window |
| `Gateway:TrustForwardedFor` (`Gateway__TrustForwardedFor`) | `false` | Reads the client address from `X-Forwarded-For` (one hop). Only `true` behind the Container Apps ingress; the AppHost sets it in publish mode |

A plain `dotnet run` enforces the real limits. The AppHost passes `RateLimits__PermitLimit` on to the gateway when it is set, and `tests/e2e` (Playwright `webServer.env`) and `tests/system` (`StackFixture`) set it to `1000`, because they sign in many users from one loopback address. The limiter is in memory, so it is correct for one gateway replica only (risk R-5).

```bash
dotnet test --project gateway/tests/Gateway.Tests
```
