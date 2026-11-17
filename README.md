# HiveSpace

A Shopee-inspired multi-seller marketplace, built by one person as a learning and portfolio project ([SRS §1](docs/srs.md)). One hero flow — register → search (including natural-language Vietnamese) → cart → split checkout → pay → track → review — plus a seller centre, used to demonstrate a multi-seller checkout saga, zero overselling under concurrency, idempotent payment callbacks and one measured AI feature.

**Status:** walking skeleton. Every stack, the broker, gRPC and tracing run end to end; features are built slice by slice per the [implementation plan](docs/implementation-plan.md).

## Units

| Folder | What | Stack |
| :-- | :-- | :-- |
| `apphost/` | Aspire AppHost (runs everything locally) and ServiceDefaults | .NET 10, Aspire 13.6 |
| `gateway/` | Gateway and backend-for-frontend; serves both SPAs from one origin | .NET, YARP |
| `services/catalog`, `identity`, `order`, `payment`, `media` | .NET services | .NET 10 |
| `services/inventory`, `cart` | Go services | Go 1.26 |
| `services/notification`, `search-ai` | Python services | Python 3.12, FastAPI, uv |
| `web/` | Storefront (`/`) and seller centre (`/seller`) | Vue 3, TypeScript, Tailwind v4, pnpm + turbo |
| `libs/` | Cross-cutting code per stack (Money, messaging, errors, telemetry) | — |
| `contracts/` | OpenAPI, gRPC protos, event schemas, Gherkin features, error codes | — |
| `templates/` | Copier templates for new services | — |
| `tests/` | `system/` (whole-stack tests), `e2e/` (Playwright) | — |
| `infra/` | Azure deployment config (filled at initial deployment) | Bicep via azd |

Architecture: [docs/architecture.md](docs/architecture.md) (container diagram in §5). Decisions: [docs/adr/](docs/adr/).

## Prerequisites

- .NET SDK 10.0.301 (pinned in `global.json`)
- Docker Desktop, running (Postgres, MongoDB, Service Bus emulator, Azurite, Mailpit run as containers)
- Go 1.23+ with `GOTOOLCHAIN=auto` (the default); Go fetches the 1.26 toolchain the modules ask for
- uv 0.11+ (it installs Python 3.12 for the services by itself)
- Node 24 and pnpm 9+, with pnpm itself running on Node 24 (`pnpm exec node -v` should print v24). A pnpm installed under an older Node silently skips native packages Vite needs
- Windows + Docker Desktop, Go integration tests only: set `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock`

No cloud account is needed locally: every Azure dependency runs as an emulator (ADR-0015).

## Run

```bash
dotnet run --project apphost/HiveSpace.AppHost
```

The Aspire dashboard opens (logs, traces, resources). The site is at http://localhost:5080 (storefront) and http://localhost:5080/seller (seller centre). Data volumes persist between runs; to reset, stop the AppHost and remove the `hivespace-*` Docker volumes.

## Test

```bash
dotnet test --solution HiveSpace.slnx
```

```bash
go test -short ./libs/go/... ./services/inventory/... ./services/cart/...
```

```bash
cd services/notification && uv run pytest
```

```bash
cd web && pnpm turbo run lint typecheck test
```

Whole stack (boots the AppHost, checks every service, the categories round trip and the event path):

```bash
dotnet test --project tests/system/HiveSpace.SystemTests
```

Browser end to end (start the AppHost first):

```bash
cd tests/e2e && pnpm install && pnpm exec playwright test
```

The mandatory risk tests (oversell, saga failure injection, triple payment callback) arrive with FEAT-017, FEAT-023 and FEAT-029 and will be listed here.

## Docs

| | |
| :-- | :-- |
| Requirements | [srs.md](docs/srs.md), [use-cases.md](docs/use-cases.md), [rtm.md](docs/rtm.md) |
| Design | [architecture.md](docs/architecture.md), [adr/](docs/adr/), [repo-structure.md](docs/repo-structure.md) |
| UX | [ux-foundations.md](docs/ux-foundations.md), [design.md](docs/design.md), [tokens.json](docs/tokens.json) |
| Plan | [implementation-plan.md](docs/implementation-plan.md) |
| Conventions | [dev-standards.md](docs/dev-standards.md), [AGENTS.md](AGENTS.md) |
| Scaffold record | [scaffold-notes.md](docs/scaffold-notes.md) |
