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

```bash
dotnet test --project gateway/tests/Gateway.Tests
```
