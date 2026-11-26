# Repository Structure: HiveSpace (MVP)

> Status: Draft · Last updated: 2026-11-16
> Inputs: [`architecture.md`](architecture.md), [`dev-standards.md`](dev-standards.md), [ADR-0020](adr/0020-service-shapes-and-copier-templates.md). This file is the reference for where code goes. Where it and `dev-standards.md` disagree, `dev-standards.md` wins and this file is corrected.

## 1. Principles

- **One monorepo, one folder per deployable.** Each block in architecture §5.2 maps to exactly one folder.
- **Same outer shell for every service, in every stack** (section 3). Tooling, CI, Aspire and reviewers depend on it.
- **Two inner shapes:** `rich` for services whose value is invariants and state machines, `lean` for services that are mostly I/O, projections or pipelines (section 4, ADR-0020).
- **Shared code is cross-cutting only.** `libs/` holds Money, messaging, errors, telemetry. No service's domain concept lives there. Cross-stack behaviour is defined by `contracts/`, never by shared code.
- **New services are generated** from `templates/` (section 9), not copied by hand.

## 2. Top level

```
/
├─ apphost/                      Aspire: HiveSpace.AppHost, HiveSpace.ServiceDefaults (section 5)
├─ gateway/                      .NET YARP gateway + BFF, serves both SPAs, one host each (section 7)
├─ services/
│  ├─ identity/ catalog/ order/ payment/ media/      .NET
│  ├─ inventory/ cart/                               Go
│  └─ search-ai/ notification/                       Python
├─ functions/
│  └─ image-processor/           .NET isolated Azure Function (section 7)
├─ web/                          pnpm + turbo workspace (section 8)
├─ libs/                         cross-cutting code per stack; no service domain concepts
│  ├─ dotnet/                    HiveSpace.BuildingBlocks.{Abstractions,Messaging,Money,Errors,Grpc,Idempotency,Testing}
│  ├─ go/                        one module: money, eventbus, outbox, inbox, grpcx, errorsx, otelx, testkit, gen/
│  └─ python/                    hivespace_common (money, eventbus, outbox, inbox, errors, otel),
│                                hivespace_contracts (generated stubs)
├─ contracts/
│  ├─ openapi/                   <service>.yaml + shared/components.yaml (Money, Error, Page, Id)
│  ├─ proto/hivespace/<svc>/v1/  one package per service + hivespace/common/v1 (money.proto)
│  ├─ events/                    envelope.schema.json, <EventName>/v1.schema.json
│  ├─ features/<svc>/            shared Gherkin, read by every stack's runner
│  ├─ errors/                    error-codes.yaml, messages.vi.yaml, messages.en.yaml
│  └─ buf.yaml, buf.gen.yaml, .spectral.yaml, README.md (generator choices)
├─ infra/                        Bicep modules, main.bicep, parameters (azd output, reviewed); budget alert
├─ templates/                    Copier templates (section 9)
├─ tools/                        seed/, datagen/, eval/, k6/, scripts/ (id-resolver, i18n-parity)
│  └─ fake-idp/                  src/FakeIdp + tests/: a local OpenID Connect server standing in for Google
│                                (Identity integration tests, contract tests, AppHost, E2E; never deployed; FEAT-004 D-7)
├─ tests/
│  ├─ e2e/                       Playwright hero flow (features/ Gherkin journeys, specs/ technical checks)
│  └─ system/                    cross-service risk tests (saga failure injection, triple callback,
│                                duplicate events), booted through Aspire.Hosting.Testing
├─ docs/
├─ .github/workflows/            one workflow per deployable + contracts, i18n, security, smoke
├─ references/                   local-only, git-ignored reference code
│  └─ hivespace.web/             read-only UI reference (CON-6)
└─ HiveSpace.slnx, global.json, Directory.Build.props, Directory.Packages.props, go.work,
   pyproject.toml (uv workspace), .editorconfig, .golangci.yml, ruff.toml, azure.yaml,
   .gitleaks.toml, README.md
```

**Generated code.**

| Output | Where | Committed |
| :-- | :-- | :-- |
| .NET gRPC stubs | Built from `contracts/proto/` by Grpc.Tools at compile time | No |
| Go gRPC stubs | `libs/go/gen/` by `buf generate` | Yes |
| Python gRPC stubs | `libs/python/hivespace_contracts/` by `buf generate` | Yes |
| TypeScript API client | `web/packages/api-client/` from `contracts/openapi/` | Yes |

CI regenerates all of them and fails on a diff (dev-standards §6, §6.1).

## 3. The outer shell (every service)

Every service, in every stack and either shape, has:

| Item | Detail |
| :-- | :-- |
| Folders | `src/`, `tests/` (unit, integration, contract), migrations, `Dockerfile`, `README.md` |
| Endpoints | `/health` (liveness), `/ready` (dependencies) |
| Telemetry | OpenTelemetry traces, metrics, logs; JSON logs with a correlation ID; ServiceDefaults in .NET, `otelx` in Go, `hivespace_common.otel` in Python |
| Errors | `{ code, message, details[] }` on REST; `google.rpc.ErrorInfo` on gRPC |
| Auth | JWT validation through JWKS; role and ownership checks in the service |
| Messaging | `outbox` and `inbox` tables, created by the first migration |
| Contract tests | Runner wired to `contracts/features/<service>/` |
| CI | `.github/workflows/<service>.yml`, path-filtered |
| Template answers | `.copier-answers.yml` (enables `copier update`) |

## 4. Service shapes

| Service | Stack | Shape | Why |
| :-- | :-- | :-- | :-- |
| Order | .NET | rich | Checkout saga, order state machine, isolated pricing pipeline |
| Payment | .NET | rich | Payment status compare-and-set, ledger states, callback idempotency |
| Catalog | .NET | rich | Product and variant rules, deactivation, reviews and verified purchase |
| Identity | .NET | rich | Shop and Seller role commit together; OpenIddict and ASP.NET Identity sit in Infrastructure |
| Inventory | Go | rich | Atomic reservation, reservation states, expiry |
| Media | .NET | lean | Upload intents, SAS issuing, status records |
| Cart | Go | lean | Lines per user, composition of Catalog and Inventory data |
| Search-AI | Python | lean | Read model, query pipeline, provider calls |
| Notification | Python | lean | Contact projection, templates, send and retry |

**Promotion rule.** When a lean service grows real invariants (rules that must hold across several entities), move it to rich. The outer shell does not change, so it is an internal refactor and a short ADR note.

### 4.1 .NET, rich (Order shown)

```
services/order/
├─ src/
│  ├─ Order.Api/                 composition root and driving adapters
│  │  ├─ Program.cs
│  │  ├─ Endpoints/              REST, grouped by feature (Checkouts/, Orders/)
│  │  ├─ Grpc/                   gRPC service implementations, thin, call Application
│  │  └─ Consumers/              event subscriptions, call Application handlers
│  ├─ Order.Application/
│  │  ├─ Features/<Feature>/     command or query, handler, validator, DTOs
│  │  ├─ Saga/                   checkout orchestrator and compensations (ADR-0006)
│  │  ├─ EventHandlers/          OnPaymentSucceeded, ...
│  │  └─ Abstractions/           ports: IInventoryClient, IShippingProvider, repositories, IClock
│  ├─ Order.Domain/              no framework or vendor references
│  │  ├─ Orders/  Checkouts/     aggregates, value objects, state machines, domain events
│  │  └─ Pricing/                isolated total pipeline (ADR-0010)
│  └─ Order.Infrastructure/      driven adapters
│     ├─ Persistence/            DbContext, Configurations/, Migrations/, Repositories/
│     ├─ Messaging/              outbox relay, inbox, publisher (from BuildingBlocks)
│     ├─ GrpcClients/            Catalog, Inventory, Cart, Payment, Identity
│     ├─ Providers/Shipping/     Mock
│     └─ Jobs/                   payment-window and auto-completion sweepers (ADR-0012)
├─ tests/
│  ├─ Order.UnitTests/           domain and application
│  ├─ Order.IntegrationTests/    Testcontainers (Postgres, broker)
│  ├─ Order.ContractTests/       (none: contract tests run from tests/contract)
│  └─ Order.ArchitectureTests/   NetArchTest: layer and vendor rules
├─ Dockerfile, README.md, Order.slnf
```

Dependency direction: `Api → Application → Domain`, `Infrastructure → Application`. Domain references nothing.

Domain folders group by aggregate or concept (Identity: `Users/`, `Shops/`, `SignIn/`, `Verification/`, `Auditing/`); the namespace stays flat (`Identity.Domain`). Application ports live one per file under `Abstractions/` (Identity: `Security/`, `Persistence/`, `Exceptions/`), also with a flat namespace. Identity's single `EfUserRepository` (it implements five ports over one `DbContext` and one `SaveChangesAsync`) is a `partial` class spread over one file per port. Infrastructure adapters sit in folders by concern (`Persistence/`, `Messaging/`, `GrpcClients/`, `Providers/`, or one named after the concern, such as Catalog's `CategorySource/`), one adapter per file, with namespaces following the folders. There is no catch-all `Ports.cs` or `Adapters.cs` (dev-standards §8.0).

### 4.2 .NET, lean (Media shown)

```
services/media/
├─ src/Media.Api/
│  ├─ Program.cs
│  ├─ Features/
│  │  ├─ CreateUploadIntent/     Endpoint.cs, Handler.cs, Request.cs, Response.cs
│  │  ├─ VerifyMedia/            GrpcService.cs, Handler.cs
│  │  └─ OnMediaProcessed/       Consumer.cs
│  ├─ Data/                      DbContext, Entities/, Migrations/
│  └─ Providers/Storage/         IObjectStorage and the Blob implementation (only place the Azure SDK appears)
├─ tests/
│  ├─ Media.Tests/               unit and integration
│  └─ Media.ArchitectureTests/   vendor SDK only under Providers; features do not reference each other
├─ Dockerfile, README.md
```

### 4.3 Go, rich (Inventory) and lean (Cart)

```
services/inventory/                       services/cart/
├─ cmd/inventory/main.go   wiring         ├─ cmd/cart/main.go
├─ internal/                              ├─ internal/
│  ├─ domain/     stock, reservation,     │  ├─ cart/       model, service, repository (interface),
│  │              errors (pure)           │  │              http.go, grpc.go (feature package)
│  ├─ app/        reserve, release,       │  ├─ checkout/   CheckoutCompleted consumer
│  │              commit, sweep, ports    │  ├─ clients/    catalog, inventory (gRPC)
│  ├─ transport/  grpc/ http/ events/     │  ├─ store/mongo/
│  ├─ store/postgres/ repo, tx, outbox    │  └─ platform/   config, otel, server
│  └─ platform/   config, otel, server    ├─ migrations/    versioned MongoDB startup scripts
├─ migrations/    golang-migrate SQL      ├─ test/integration/
├─ test/integration/
├─ go.mod, Dockerfile, README.md          └─ go.mod, Dockerfile, README.md
```

`depguard` rules: `domain` imports nothing from `internal/`; only `store/` imports a database driver; only `clients/` and `transport/grpc` import generated stubs.

### 4.4 Python, lean (Search-AI shown)

```
services/search-ai/
├─ pyproject.toml
├─ src/search_ai/
│  ├─ main.py                    FastAPI app factory and consumer startup
│  ├─ settings.py                pydantic-settings
│  ├─ features/
│  │  ├─ keyword_search/         router.py, service.py, queries.py
│  │  ├─ nl_search/              router.py, price_parser.py, interpreter.py, schema.py (Pydantic)
│  │  └─ indexing/               consumers: ProductChanged, ShopChanged, ReviewChanged, CategoryTreeChanged
│  ├─ providers/                 llm/, embeddings/ (interfaces and implementations; only place vendor SDKs appear)
│  └─ db/                        models, session, repositories
├─ migrations/                   Alembic
├─ tests/ unit/ integration/ (contract tests: tests/contract)
├─ Dockerfile, README.md
```

Notification uses the same shape: `features/{contacts,send_email,retry}/`, `templates/{vi,en}/`, `providers/email/`. `import-linter` contracts: features do not import each other; vendor SDKs only under `providers/`.

The Python template also has a rich variant (`domain/`, `application/`, `adapters/`) for later use. No Python service uses it now.

## 5. Aspire (`apphost/`)

`apphost/` is the single entry point (`dotnet run --project apphost/HiveSpace.AppHost`), so it sits at the top level beside what it runs, not inside `services/` or `infra/`.

```
apphost/
├─ HiveSpace.AppHost/
│  ├─ Program.cs                  thin: calls the extension methods below in order
│  ├─ Resources/
│  │  ├─ Infrastructure.cs        Postgres (pgvector) with one database per service, MongoDB,
│  │  │                           Service Bus emulator, Azurite, Mailpit
│  │  ├─ DotnetServices.cs        identity, catalog, order, payment, media, gateway
│  │  ├─ GoServices.cs            inventory, cart (HTTP/2 endpoints for gRPC)
│  │  ├─ PythonServices.cs        search-ai, notification
│  │  ├─ Functions.cs             image-processor
│  │  └─ Web.cs                   Vite dev servers for storefront and seller-centre (local only)
│  ├─ Profiles/                   local (everything) and deploy (no heavy jobs, NFR-COST-003)
│  ├─ appsettings.json            non-secret parameters only; secrets through user-secrets
│  └─ HiveSpace.AppHost.csproj
├─ HiveSpace.ServiceDefaults/     OTel, health and ready, resilience, correlation ID, JSON logging;
│                                 referenced by every .NET service, the gateway and the function
└─ README.md                      how to run, ports, how to register a new service
```

- **ServiceDefaults stays in `apphost/`, not `libs/dotnet/`.** It is the hosting contract the AppHost expects from every process. `libs/dotnet/` holds business-agnostic code. Go (`libs/go/otelx`) and Python (`libs/python/hivespace_common/otel`) mirror the same contract, checked by the CI conformance job (dev-standards §11).
- **Relation to `infra/`.** `azd` reads the AppHost to generate Bicep. The generated Bicep is reviewed and committed to `infra/` (NFR-MNT-004), together with what azd does not produce (budget alert, Neon, Key Vault).
- **Tests.** `tests/system/` and the CI `smoke` job boot the AppHost with `Aspire.Hosting.Testing`.
- Root `azure.yaml` points azd at `apphost/HiveSpace.AppHost`.

## 6. Shared libraries (`libs/`)

| Library | Contents | Not allowed |
| :-- | :-- | :-- |
| `libs/dotnet/HiveSpace.BuildingBlocks.Abstractions` | `Result<T>`, `Failure`, `Violation`, `Unit`, `ErrorCodes`, `IEventOutbox`; Domain and Application layers may reference it (ADR-0028) | Any package or framework reference (`Microsoft.*`, EF, Azure, Grpc); enforced by its NetArchTest rule |
| `libs/dotnet/HiveSpace.BuildingBlocks.*` | Money, error envelope and `ErrorInfo` mapping, `IEventBus`, outbox relay, inbox, idempotency key store, gRPC interceptors (deadline, auth metadata), test fixtures | Any type named after a service concept (Order, Product, Cart) |
| `libs/go` | Same concepts in Go, plus `gen/` | Same |
| `libs/python` | Same concepts in Python, plus generated stubs | Same |

Enforced by architecture tests in each stack: a library may not reference any `services/*` code.

## 7. Gateway and Function

```
gateway/
├─ src/Gateway/
│  ├─ Program.cs, yarp.json
│  ├─ Bff/            login, callback, logout, encrypted cookie session, token refresh (ADR-0008)
│  ├─ Proxy/          YARP transforms: token relay, correlation ID, gRPC status to error envelope
│  ├─ RateLimiting/
│  ├─ Callbacks/      anonymous payment-callback route
│  └─ Spa/            host-based static hosting (storefront host, seller host)
├─ tests/Gateway.Tests/
└─ Dockerfile         multi-stage: builds web/apps/*, copies dist into wwwroot

functions/image-processor/
├─ src/ImageProcessor/
│  ├─ Program.cs
│  ├─ ProcessUploadedMedia.cs     Service Bus trigger
│  ├─ Imaging/                    signature check, re-encode, metadata strip, resize
│  └─ host.json
├─ tests/ImageProcessor.Tests/    signature, SVG, oversize, spoofed extension (NFR-SEC-013)
└─ README.md
```

## 8. Web workspace

```
web/
├─ apps/
│  ├─ storefront/                 base path /
│  │  ├─ src/
│  │  │  ├─ app/                  main.ts, App.vue, router/, plugins (i18n, pinia)
│  │  │  ├─ features/             search/ product/ cart/ checkout/ orders/ account/ auth/
│  │  │  │  └─ <feature>/         pages/ components/ composables/ store.ts api.ts index.ts (public API)
│  │  │  ├─ shared/               layouts/, app-specific components
│  │  │  └─ locales/              vi/, en/ (feature-namespaced JSON)
│  │  ├─ tests/                   Vitest
│  │  └─ sheriff.config.ts, vite.config.ts, tsconfig.json, package.json
│  └─ seller-centre/              own host (seller.localhost:5080 locally), same shape
│                                 (features: shop/ products/ stock/ orders/ account/ auth/)
├─ packages/
│  ├─ api-client/                 generated from contracts/openapi; never edited by hand
│  ├─ ui/                         shared components, including loading, empty and error states
│  ├─ core/                       Money and date formatting (Asia/Ho_Chi_Minh), error-code helpers, i18n setup
│  └─ config/                     ESLint, Prettier, tsconfig base, Vitest base
└─ package.json, pnpm-workspace.yaml, turbo.json
```

- Same `apps/` and `packages/` with turbo as `references/hivespace.web/`, so its ESLint and Prettier configs can be copied into `packages/config` (dev-standards §8.4).
- Sheriff rules: a feature imports another only through its `index.ts`; only `api.ts` files import `@hivespace/api-client`; apps never import each other.

## 9. Templates (`templates/`)

Tool: **Copier**, for every stack. One tool to learn, Jinja conditionals for the shape, and `copier update` pushes later template fixes into services that were already generated.

```
templates/
├─ README.md                    which template, which shape, how to run and update
├─ dotnet-service/  copier.yml  name, shape (rich|lean), grpc_server, consumes_events, port
├─ go-service/      copier.yml  name, shape (rich|lean), store (postgres|mongo), grpc_server
├─ python-service/  copier.yml  name, shape (rich|lean), grpc_server
├─ vue-app/         copier.yml  name, base_path
└─ dotnet-function/ copier.yml  name, trigger (servicebus|http)
```

- **Rendered from the repo root** with templated paths, so one run creates the service folder and also:
  - `contracts/openapi/<name>.yaml`
  - `contracts/proto/hivespace/<name>/v1/<name>.proto`
  - `contracts/features/<name>/health.feature`
  - `.github/workflows/<name>.yml`
  - `services/<name>/.copier-answers.yml`
- **Shared files are not edited by the template** (AppHost resources, `HiveSpace.slnx`, `go.work`, the uv workspace). Copier prints the lines to add after copying; editing shared files from a template would break `copier update`.
- **Each template ships one working sample slice**: `GET /v1/<name>/ping`, one gRPC method and one event consumer, with their tests, so a new service passes CI on its first commit.

Usage (once the templates exist):

```bash
copier copy templates/go-service . --data name=inventory --data shape=rich --data store=postgres
```

```bash
copier update --answers-file services/inventory/.copier-answers.yml
```

The templates are built in the project-scaffolding step.
