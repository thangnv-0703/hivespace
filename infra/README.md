# Infrastructure

Azure deployment config for the `demo` environment (architecture §7, ADR-0005). **Nothing here has been provisioned.**

- `azure.yaml` at the repository root points `azd` at `apphost/HiveSpace.AppHost`.
- At the initial-deployment step, `azd infra gen` produces the Bicep from the AppHost. The generated files are reviewed and committed here (NFR-MNT-004), together with what azd does not produce: the budget alert (NFR-COST-001), Neon databases and Key Vault.
- Open decisions that step must close: provision-per-window vs. always-on (risk R-2, Service Bus cost), the MongoDB host for Cart (R-13), and the region.
- Deployed configuration values are supplied through the platform at deploy time, never committed.
- Gateway: the AppHost sets `Gateway__TrustForwardedFor=true` in publish mode, so the rate limits (NFR-SEC-005) key on the client address the Container Apps ingress puts in `X-Forwarded-For`. Review that it reaches the generated Bicep.
