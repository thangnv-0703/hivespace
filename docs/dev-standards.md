# Development Standards: HiveSpace (MVP)

> Version: 0.2 · Status: Draft · Last updated: 2026-11-16 (0.2: service shapes, templates, repo layout per ADR-0020)
> Inputs: `docs/srs.md` v0.10, `docs/use-cases.md`. Where this file and the SRS disagree, the SRS wins. SRS v0.11 already reflects the Aspire and separate-services decisions.

## 1. Purpose and enforcement model

These standards exist so a solo builder (and AI agents working in the repo) produce consistent, reviewable code across .NET, Go, Python and Vue.

- **Rules live in tooling.** Every rule below names the tool or CI job that enforces it. A rule marked *Review* is checked by the author against the PR checklist (§4.4) because no tool can check it.
- **Tool versions are pinned at scaffolding time**, not in this file. Exact versions live in lockfiles, `global.json`, `go.mod`, `pyproject.toml` and `package.json`.
- A rule that cannot be enforced and is not worth reviewing is deleted, not kept.

### 1.1 Technology selection

The stack rules in section 8 describe the current allocation of technology to services. They are not a ban on other technology. The main purpose of the project is learning, so a new design, or any single service, may choose the technology that suits it, including Azure Functions and NoSQL databases. Each choice is recorded in an ADR with a short note on what it teaches. The budget (NFR-COST-001), correctness of checkout, stock and payment (CON-7) and the solo-capacity limit still win over novelty, and the language-neutral conventions in section 5 apply to every stack.

## 2. Repository layout

One monorepo. Backend and frontend share the repo and are separated by top-level folder. The detailed layout of every folder, each service shape and the templates is in [`repo-structure.md`](repo-structure.md).

```
apphost/        Aspire AppHost + ServiceDefaults (the single entry point to run everything)
gateway/        API gateway (EIR-COM-001)
services/
  identity/  catalog/  order/  payment/  media/          (.NET)
  inventory/  cart/                                      (Go)
  search-ai/  notification/                              (Python)
functions/      image-processor (.NET isolated Azure Function)
web/
  apps/storefront/  apps/seller-centre/  packages/       (Vue 3 + TypeScript, pnpm + turbo workspace)
libs/           cross-cutting code per stack (dotnet/, go/, python/); no service domain concepts
templates/      Copier templates per stack (ADR-0020)
tests/          e2e/ (Playwright) and system/ (cross-service risk tests)
contracts/
  openapi/      one OpenAPI file per service (source of truth, §6)
  features/     Gherkin API contract tests, one folder per service (§9)
  events/       versioned event schemas (§7)
  proto/        gRPC contracts for internal calls, one package per service (§6.1)
infra/          IaC (Bicep via azd) and deployment config
tools/          seed, eval, k6, data generator (run locally only, NFR-COST-003)
docs/           srs, use-cases, rtm, dev-standards, adr/
references/     local-only reference code, git-ignored; holds hivespace.web/ (existing UI reference; read-only, not a dependency, CON-6)
```

| Rule | Enforced by |
| :-- | :-- |
| Each service owns one folder and one database; no service references another service's code; `libs/` never references `services/` | Architecture test per .NET service (NetArchTest); `go vet` + import rule in Go; `import-linter` in Python; Sheriff in `web/` |
| Path-filtered CI: a change under one folder runs only that folder's jobs plus contract jobs | CI path filters |
| New code under `references/hivespace.web/` is not allowed; copy what is needed into `web/` | Code owners / *Review* |

## 3. Local orchestration with .NET Aspire

Aspire replaces docker compose as the local orchestrator.

- `dotnet run --project apphost` brings up the full stack: all services, PostgreSQL with pgvector, MongoDB (Cart; no Azure emulator exists), the message broker, Mailpit, an object-storage emulator and the gateway.
- .NET services are registered as projects. The Go and Python services, the broker, databases and emulators are registered as containers or executables in the AppHost, so one command and one dashboard cover everything.
- **ServiceDefaults** (shared .NET project) provides OpenTelemetry, health endpoints, resilience and the correlation ID. Go and Python services implement the same contract by hand (§11) and are verified by the conformance check in CI.
- Configuration and secrets come from Aspire parameters and user-secrets locally and from the cloud secret store when deployed; never from files in the repo (NFR-SEC-007).
- Cloud: `azd` publishes the AppHost to Azure Container Apps with scale-to-zero (NFR-PORT-002). IaC is the generated Bicep, reviewed and committed under `infra/` (NFR-MNT-004).
- Prerequisites (documented in the README): .NET SDK pinned by `global.json`, Docker-compatible container runtime, Go, Python, Node and pnpm at the pinned versions.

| Rule | Enforced by |
| :-- | :-- |
| A fresh clone runs with the documented command and no other manual step (NFR-PORT-001) | CI smoke job: start the AppHost headless and call each `/health` |
| No connection string, key or token in source or in `appsettings*.json` | gitleaks (NFR-SEC-007) |
| Heavy jobs (embeddings, generator, load tests) are never part of the deployed AppHost profile (NFR-COST-003) | AppHost profile review; *Review* |

## 4. Git workflow

### 4.1 Branching
Trunk-based. `main` is always releasable and always green (NFR-MNT-001). Work happens on short-lived branches (target: merged within 2 days). No long-lived `develop` or release branches. Every change reaches `main` through a pull request, even when solo, so CI gates the merge.

Branch name: `<type>/<ID>-<slug>`, for example `feat/FR-CHK-005-checkout-rollback`.

### 4.2 Commits and PR titles
Conventional Commits: `type(scope): summary [IDs]`. Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `ci`, `perf`. Scope is the service or folder. The requirement IDs the change implements go in square brackets, for example `feat(checkout): compensate on reserve failure [FR-CHK-005, FR-CHK-006]`.

### 4.3 Merging
Squash merge; the PR title becomes the commit message. Delete the branch after merge. Force-push to `main` is disabled.

### 4.4 Definition of done (PR checklist)
- [ ] The cited FR/NFR IDs are fully met, not partly.
- [ ] OpenAPI changed first, if the API changed (§6), and the breaking-change check passes.
- [ ] A Gherkin scenario exists or is updated for each changed endpoint behaviour (§9).
- [ ] Mandatory risk tests (§9.2) pass if the change touches their area.
- [ ] New user-facing strings exist in both `vi` and `en`.
- [ ] No secret, no bare money value, no local-time timestamp introduced.
- [ ] Docs updated: README run steps, ADR if a decision was made, RTM if requirement status changed.

| Rule | Enforced by |
| :-- | :-- |
| PR title matches Conventional Commits with bracketed IDs that exist in the SRS | CI check (commitlint + ID resolver script) |
| Required checks must pass before merge; `main` is protected | Repository branch protection |
| Checklist in 4.4 | PR template; *Review* |

## 5. Cross-cutting conventions

These come from the SRS and apply to every service and every language.

| Topic | Rule | SRS | Enforced by |
| :-- | :-- | :-- | :-- |
| Money | Always an amount plus a currency code, in code, storage, APIs, events and logs. Never a bare number. VND amounts are whole dong (exponent 0). Use a `Money` type per language; arithmetic across currencies throws. | FR-CUR-001..005, NFR-MNT-008, NFR-L10N-001 | Roslyn analyzer / `go vet` custom check / `ruff` rule banning float money; OpenAPI Spectral rule (money schema `$ref` only); money unit tests |
| Currency set | Currency list and formats are configuration; adding one must not touch cart, checkout, order or inventory code | NFR-MNT-009 | Architecture test: those modules do not reference a specific currency code |
| Time | Store UTC; convert to Asia/Ho_Chi_Minh only at the UI edge. API timestamps are ISO-8601 with `Z`. | NFR-L10N-002 | Analyzer banning `DateTime.Now` / `time.Now().Local` / naive `datetime`; Spectral `format: date-time` |
| Text | UTF-8 everywhere; Vietnamese diacritics preserved. Database collation and encoding set in migrations. | NFR-L10N-003 | Migration review; seed test with diacritics |
| Identifiers | Public IDs are opaque strings (UUID v7 or ULID, decided in an ADR); never expose auto-increment keys. Order numbers are separate and human-readable. | FR-ORD-016 | Spectral rule; *Review* |
| Errors | Every rejection returns `{ code, message, details[] }`. `code` is stable and documented in OpenAPI; `message` is localized to the request language; field errors identify each invalid field. | FR-LOC-004, FR-AUTH-004 | OpenAPI error schema + Gherkin scenarios assert `code`, never `message` |
| Localization | UI, system messages and emails exist in `vi` and `en`; `vi` is the fallback. Translation keys must match across languages. | FR-LOC-001..008, NFR-L10N-004 | CI key-parity script |
| Authorization | Every protected endpoint checks identity, role and resource ownership on the server. UI hiding is not enforcement. | FR-ROLE-003..005 | Mandatory authz test suite (§9.2); gateway rejects unauthenticated requests |
| Idempotency | State-changing endpoints that can be retried (checkout, payment callbacks, reservations, releases) accept an idempotency key or are naturally idempotent; repeats return the original outcome. | FR-CHK-008, FR-INV-005, FR-INV-008, FR-PAY-007 | Mandatory idempotency tests (§9.2) |
| Events | Consumers tolerate at-least-once delivery and deduplicate by event ID. | NFR-REL-005, FR-NOTIF-010 | Per-consumer duplicate-delivery test |
| Pagination | List endpoints are paged (default 20) and return the total count. | FR-SRCH-008, FR-ORD-013 | Spectral rule on list operations |
| Providers | Payment, email, shipping, LLM, embedding, Google identity and object storage each sit behind an interface; implementations are chosen by configuration. Calling code never references a vendor SDK. | NFR-MNT-006, NFR-MNT-010, EIR-SW | Architecture tests: vendor SDK namespaces only in the provider project/package |
| Dependency failure | Failure of LLM, embedding, email, image storage, Google or shipping must not fail checkout, cart, browse, keyword search or password sign-in. Use timeouts and fallbacks. | NFR-REL-008 | Failure-injection tests (§9.2) |
| Personal data | Collect only the fields in NFR-LEG-001. Never log passwords, tokens, full bank numbers, email bodies or full personal data. | NFR-LEG-001, NFR-SEC-008 | Log-redaction unit test; *Review* |

## 6. API standards (OpenAPI first)

The OpenAPI file for a service in `contracts/openapi/` is the source of truth. Write or change the spec first, then implement.

1. **Spec before code.** A PR that changes an endpoint's behaviour without changing the spec is rejected.
2. **Servers conform to the spec.** .NET validates responses against the spec in contract tests; Go and Python do the same through the Gherkin runners (§9.1). Server stubs/models are generated where the generator is stable, otherwise written by hand and checked against the spec.
3. **Clients are generated.** The TypeScript client for `web/` is generated from the specs into a workspace package; hand-written API calls in `web/` are not allowed.
4. **Style.** REST, JSON, `kebab-case` paths, `camelCase` properties, plural nouns, standard status codes, shared `components` for Money, Error, Page, and Id. Versioning by path prefix (`/v1`) on the gateway; breaking changes need a new version.
5. **Gateway.** Clients reach services only through the gateway (EIR-COM-001); the gateway verifies tokens and forwards identity and roles (FR-AUTH-015, FR-AUTH-016).
6. **Payment callbacks** are the only inbound external endpoints besides the gateway; each is authenticated by signature (EIR-COM-002, FR-PAY-008).
7. **Updates use PUT, never PATCH** (decision of 2026-11-19). An update replaces the resource's full editable representation. A part the UI changes on its own (for example the language) is a PUT on a sub-resource, such as `PUT /v1/me/profile/language`.

| Rule | Enforced by |
| :-- | :-- |
| Specs lint clean against the project ruleset (naming, Money/Error/Page refs, operation IDs, `security` set) | Spectral in CI |
| No breaking change to a published version | oasdiff in CI against `main` |
| No `patch` operations in any spec (rule 7) | Spectral rule `hs-no-patch` in CI |
| Generated client is up to date with the specs | CI regenerates and fails on diff |
| Every operation has a Gherkin feature (§9.1) | Coverage script comparing operation IDs to `@operation` tags |

### 6.1 Internal gRPC

Public operations are REST/OpenAPI above. Service-to-service synchronous calls use gRPC (ADR-0019); events stay on the broker (§7).

1. **Proto first.** The `.proto` file in `contracts/proto/` is the source of truth for an internal call; change it before the code.
2. **Style.** One package per service with a version in the package name, `PascalCase` services and methods, Money and Error messages shared, no breaking change within a version.
3. **Deadlines.** The caller sets a deadline on every call and it propagates. Retry only idempotent calls.
4. **Errors.** Use the gRPC status code for the class and put the stable error code and field details in status details (`google.rpc.ErrorInfo`).
5. **Auth.** The user's bearer token travels as call metadata and the callee validates it.
6. **Stubs** are generated per language and never edited by hand.

| Rule | Enforced by |
| :-- | :-- |
| Protos lint clean against the project ruleset | `buf lint` in CI |
| No breaking change to a published version | `buf breaking` against `main` in CI |
| Generated stubs are up to date | CI regenerates and fails on diff |

## 7. Service and data standards

Separate services, each with its own database (CON-4). The module-versus-service question (OQ-9) is settled as separate services.

- **Database per service.** PostgreSQL by default; MongoDB for Cart; Catalog's store is decided after the crawl review (ADR-0018). No service reads another service's tables or collections. Cross-service data comes through APIs or events.
- **Migrations** are code, versioned and run automatically by the AppHost locally and by a deployment step in the cloud: EF Core migrations (.NET), golang-migrate (Go), Alembic (Python); MongoDB: versioned startup scripts that create collections, validators and indexes. Restoring from scripts takes 10 minutes or less (NFR-REL-007).
- **Events.** Services exchange domain events through the broker (EIR-COM-003; broker choice is an ADR, OQ-5). Each event has a versioned JSON schema in `contracts/events/`, an event ID, a timestamp and a correlation ID. Adding a field is non-breaking; removing or renaming one needs a new version.
- **Outbox.** A service that changes state and publishes an event does both in one local transaction (transactional outbox) so no event is lost or phantom.
- **Checkout saga.** Order orchestrates reserve, create orders, initiate payment, with a compensation for every step and a timeout per step (FR-CHK-006, FR-CHK-007). Every step handler and every compensation is idempotent.
- **Inventory** reserves atomically in the database (conditional update or row lock) so available quantity never goes below zero (FR-INV-004). It never relies on application-level locks.
- **Search index** and embeddings refresh from catalog events within 60 seconds (FR-CAT-018, FR-NLS-007).
- **Seed** is idempotent: running it twice creates no duplicates (FR-CAT-017).

| Rule | Enforced by |
| :-- | :-- |
| Event payloads match their schema | Schema validation in producer tests and in consumers (CI) |
| No cross-database access | Connection strings are scoped per service in the AppHost; architecture tests |
| Migrations apply to an empty database and to the previous release's schema | CI migration job |

## 8. Per-stack standards

### 8.0 Service shapes

Every service has the same outer shell (repo-structure §3) and one of two inner shapes (ADR-0020):

- **rich** (ports and adapters, pure domain layer): Order, Payment, Catalog, Identity, Inventory.
- **lean** (vertical feature slices, no separate domain layer): Media, Cart, Search-AI, Notification.

New services are generated from `templates/` with Copier, never copied by hand. A lean service that grows invariants across several entities is promoted to rich.

| Rule | Enforced by |
| :-- | :-- |
| A service's shape matches the table above and its `.copier-answers.yml` | *Review* |
| Templates still generate a service that builds and passes its tests, for each stack and shape | CI `templates` job |

### 8.1 .NET (Identity, Catalog, Order, Payment, Media, gateway, image processor, ServiceDefaults)
| Area | Standard | Enforced by |
| :-- | :-- | :-- |
| Format and style | `.editorconfig`, file-scoped namespaces | `dotnet format --verify-no-changes` |
| Static analysis | Nullable reference types on, warnings as errors, Roslyn analyzers | `dotnet build -warnaserror` |
| Structure | Rich: `Api`, `Application`, `Domain`, `Infrastructure`; domain has no framework or vendor references. Lean: one `Api` project with `Features/<Feature>/` folders; vendor SDKs only under `Providers/`; features do not reference each other | NetArchTest in unit tests |
| Data access | EF Core, async only, no raw SQL without a parameter | Analyzer + SQL-injection test (NFR-SEC-006) |
| Unit and integration tests | xUnit; integration tests use Testcontainers, not mocks, for the database and broker | `dotnet test` |
| Contract tests | cucumber-js over `contracts/features/<service>` | `cd tests/contract && pnpm test` |

### 8.2 Go (Inventory, Cart)
| Area | Standard | Enforced by |
| :-- | :-- | :-- |
| Format | `gofmt` / `goimports` | CI `gofmt -l` must be empty |
| Lint | `golangci-lint` with the committed config | CI |
| Concurrency | `go test -race` on every package; context passed to every blocking call | CI |
| Structure | `cmd/`, `internal/`, no package cycles; handlers thin. Rich (Inventory): `internal/domain` imports nothing internal, use cases in `internal/app`, adapters in `transport/` and `store/`. Lean (Cart): one package per feature (`internal/cart`); only `store/` imports a database driver | `golangci-lint` (depguard, cyclop) |
| Tests | Table-driven tests; integration tests against a real PostgreSQL container | `go test` |
| MongoDB (Cart) | Official Go driver behind a repository interface; single-document atomic updates only; integration tests against a MongoDB container | `go test`, Testcontainers |
| Contract tests | cucumber-js over `contracts/features/inventory` | `cd tests/contract && pnpm test` |

### 8.3 Python (Search + AI, Notification, tools)
| Area | Standard | Enforced by |
| :-- | :-- | :-- |
| Format and lint | `ruff format` and `ruff check` | CI |
| Types | Type hints on public functions; `mypy --strict` for the service package | CI |
| Structure | `src/` layout, `pyproject.toml`, one uv workspace lockfile; scripts are run with `python -I`. Lean: `features/<feature>/`, vendor SDKs only under `providers/`, features do not import each other | CI (`import-linter`); *Review* |
| LLM output | Parsed into a typed model (Pydantic) with a fixed schema; invalid output triggers the keyword fallback (FR-NLS-005, NFR-SEC-010) | Unit tests with malformed LLM output |
| Tests | `pytest`; no network in unit tests | CI |
| Contract tests | cucumber-js over `contracts/features/search-ai` | `cd tests/contract && pnpm test` |

### 8.4 Vue 3 + TypeScript (storefront, seller centre)
| Area | Standard | Enforced by |
| :-- | :-- | :-- |
| Package manager | pnpm workspace | CI frozen-lockfile install |
| Format and lint | ESLint + Prettier (reuse the existing `references/hivespace.web` configs as the starting point) | CI |
| Types | `vue-tsc --noEmit`, `strict` on | CI |
| Structure | Sheriff module boundaries; feature folders; API access only through the generated client | `eslint-plugin-sheriff` |
| i18n | All user-visible strings through `vue-i18n` keys; no literal UI text in templates | ESLint i18n rule + key-parity script |
| States | Every list and async action has loading, empty and error states (NFR-USE-002) | *Review* + component tests |
| Layout | Works at 360 px width and on desktop (EIR-UI-003) | Playwright viewport test on the hero flow |
| Tests | Vitest for units; Playwright for the hero flow, with the user journeys written as Gherkin (ADR-0026) | CI |

## 9. Testing strategy

Risk-based. There is no global coverage percentage. Test depth follows the risk: the checkout, inventory and payment paths get the most (CON-7); presentation code gets the least.

### 9.1 Gherkin API contract tests
- Every public operation in the OpenAPI specs has at least one `.feature` file in `contracts/features/<service>/`. Saga-critical internal gRPC methods (Reserve, Release, selected cart lines, Initiate payment) also have scenarios, tagged `@rpc:<Service.Method>` with the requirement ID.
- Each scenario is tagged with the operation and the requirement it verifies, for example `@operation:placeCheckout @FR-CHK-005 @UC-011`.
- Scenarios assert status code, the stable error `code` (never the localized message) and that the response body validates against the OpenAPI schema.
- Scenarios cover the main flow and each exception flow in the matching use case.
- Runner: cucumber-js in `tests/contract` (ADR-0025). It starts each service and its Postgres container and calls the public API, so behaviour is defined once in the shared `.feature` files regardless of the implementation language. Steps that need state the API does not expose use direct SQL on the service database.
- The tags feed the RTM Test ref column (acceptance-verification updates it).

### 9.2 Mandatory risk tests
| Area | Test | SRS |
| :-- | :-- | :-- |
| Oversell | 10 units, 200 concurrent buyers, exactly 10 reservations succeed; 3 runs; k6 reports committed | NFR-SCAL-001, NFR-REL-001, NFR-OBS-003 |
| Saga | Inject failure at stock, order creation, payment initiation and timeout; 0 orphan orders, 0 held reservations | NFR-REL-002 |
| Idempotent callbacks | Same payment callback delivered 3 times; 1 payment effect, 1 order transition | NFR-REL-003 |
| Event duplication | Each consumer receives its event twice; no duplicate side effect | NFR-REL-005 |
| Authorization | A user cannot read or change another user's cart, order, address, shop or product; SQL-injection inputs rejected | NFR-SEC-006, FR-ROLE-004 |
| Money | Formatting, rounding and cross-currency rejection | FR-CUR, NFR-L10N-001 |
| Dependency failure | LLM, email, storage, Google or shipping down; checkout, browse, keyword search and password sign-in still work | NFR-REL-008 |
| Upload | Wrong type, oversize, SVG, spoofed extension rejected | NFR-SEC-013 |

These run from the repo with documented commands (NFR-MNT-005).

### 9.3 Other rules
- A bug fix includes a test that fails without the fix.
- Tests are never weakened, skipped or deleted to make a build pass.
- Integration tests use real containers started by the test harness, not hand-written database mocks.
- The NL-search evaluation set and results file are versioned (FR-NLS-010..012); the evaluation script is deterministic apart from the LLM.

## 10. Security and secrets

| Rule | SRS | Enforced by |
| :-- | :-- | :-- |
| No secrets in the repo; secret scan fails CI | NFR-SEC-007 | gitleaks (pre-commit and CI) |
| Known high-severity dependency vulnerabilities fail CI | NFR-SEC-012 | `dotnet list package --vulnerable`, `govulncheck`, `pip-audit`, `pnpm audit` |
| Passwords hashed with argon2id or bcrypt only | NFR-SEC-002 | Single `PasswordHasher` abstraction; unit test; *Review* |
| TLS 1.2+ on the deployed demo | NFR-SEC-001 | IaC config; post-deploy check |
| Rate limits as specified | NFR-SEC-005 | Gateway config + Gherkin scenario for the rate-limit response |
| LLM text is data only; output validated against a fixed schema; no tools granted | NFR-SEC-010 | Unit tests; *Review* |
| Images validated by signature, re-encoded, random names, SVG rejected | NFR-SEC-013 | Mandatory upload test |
| Google sign-in uses authorization code + PKCE, verifies state, nonce and ID token claims, minimal scopes | NFR-SEC-014 | Integration test with a fake IdP |
| Card numbers are never stored or processed | NFR-SEC-011 | *Review*; no card fields in any spec |
| Bank numbers encrypted at rest and masked in responses | NFR-SEC-008 | Spectral rule + unit test |
| Audit log of sign-in, lockout, role and status changes, kept 90 days | NFR-SEC-009 | Integration tests assert audit rows |

## 11. Observability

- Every service emits OpenTelemetry traces, metrics and logs (EIR-COM-004). .NET gets this from ServiceDefaults; Go and Python use the OpenTelemetry SDKs configured the same way.
- One checkout produces one trace spanning the gateway and every service it touches (NFR-OBS-001). The trace context is propagated through HTTP and through broker messages.
- Logs are structured JSON, carry a correlation ID, and follow §5 on personal data (NFR-OBS-002).
- Every service exposes `/health` (liveness) and `/ready` (dependencies).
- Dashboards: the Aspire dashboard locally; the cloud equivalent is described in an ADR.

| Rule | Enforced by |
| :-- | :-- |
| Each service answers `/health` and `/ready` and emits a trace with the shared resource attributes | CI smoke job (§3) |
| A checkout trace contains spans from all expected services | Integration test reading the trace exporter |
| Logs are JSON with a correlation ID | Log-format unit test per stack |

## 12. CI/CD and infrastructure

CI runs on every push and pull request and keeps `main` green (NFR-MNT-001).

| Job | What it does |
| :-- | :-- |
| `lint-<stack>` | Format and lint for each changed stack (§8) |
| `build-test-<service>` | Build, unit tests, integration tests, Gherkin contract tests for each changed service |
| `contracts` | Spectral, oasdiff, `buf lint` and `buf breaking`, generated-client and stub freshness, event-schema validation, operation-to-feature coverage |
| `i18n` | vi/en key parity (NFR-L10N-004) |
| `security` | gitleaks, dependency audit (§10) |
| `smoke` | Start the AppHost headless; check every `/health` (§3) |
| `templates` | On a change under `templates/`: generate a sample per stack and shape, build and test it (§8.0) |
| `deploy` (manual or on tag) | `azd` provision and deploy to Azure Container Apps; post-deploy health check |

- Deployed infrastructure is defined as code under `infra/` (NFR-MNT-004). A budget alert is configured in IaC (NFR-COST-001).
- Load tests, batch embeddings and the data generator never run in CI against the deployed demo (NFR-COST-003, FR-GEN-003).
- Image and email usage stays inside provider free tiers (NFR-COST-004); caps are configuration, not code.

## 13. Documentation and traceability

- **ADRs** live in `docs/adr/` as `NNNN-title.md` (status, context, decision, consequences). At least five are required: service boundaries, message broker, search store, payment idempotency, hosting (NFR-MNT-002). Additional ADRs: ID strategy (§5), orchestration with Aspire (§3), outbox and saga approach (§7).
- **README** has run steps, prerequisites and the commands for the mandatory tests (NFR-MNT-003, NFR-MNT-005). An architecture diagram is committed alongside.
- **IDs.** FR, NFR, UC, SCR, FEAT, ADR and DEF IDs are stable and never reused. Commits, PR titles, Gherkin tags and test names cite them. A removed requirement keeps its ID with status Removed.
- **RTM** (`docs/rtm.md`) is updated when a requirement's status changes; the Test ref column is filled from Gherkin tags.
- **Comments** explain why, not what. Public APIs are documented in OpenAPI, not in code comments.

| Rule | Enforced by |
| :-- | :-- |
| Every ID cited in commits, tests or docs resolves in its defining document | ID resolver script in CI (also used by pipeline-verify) |
| At least five ADRs exist | CI count check |

## 14. Open items and follow-ups

| # | Item | Action |
| :-- | :-- | :-- |
| 1 | **Aspire replaces docker compose.** | Done: SRS v0.11 (CR-2) amended NFR-PORT-001, §2.4 and NFR-PERF-001 |
| 2 | OQ-9 (modular monolith vs services) is settled as separate services. | SRS v0.11 marks OQ-9 resolved; record the decision as ADR 0001 |
| 3 | OQ-5 message broker. | ADR before week 5; §7 stays broker-neutral until then |
| 4 | Public ID format (UUID v7 vs ULID). | ADR |
| 5 | Which generators produce server stubs and the TypeScript client. | Decide at scaffolding; record in `contracts/README.md` |
| 6 | Go and Python OpenTelemetry parity with ServiceDefaults. | Conformance check designed at scaffolding (§11) |
| 7 | Tool versions and exact CI configuration. | Pinned at project scaffolding |
