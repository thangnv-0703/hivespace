# Scaffold Notes: HiveSpace

> Project-owned record of how the walking skeleton was generated (project-scaffolding step). Started 2026-11-17.
> Plan: [implementation-plan.md §2](implementation-plan.md). Layout: [repo-structure.md](repo-structure.md).

## 1. Confirmed scaffold plan

- Monorepo per repo-structure.md. Aspire AppHost is the local orchestrator (ADR-0015) instead of a compose file.
- Real skeleton units: AppHost + ServiceDefaults, gateway, Catalog (.NET rich), Inventory (Go rich), Notification (Python lean), web (storefront, seller centre, packages).
- Copier templates (`dotnet-service`, `go-service`, `python-service`) built from those units; identity, order, payment (rich), media (lean), cart (Go lean, Mongo), search-ai (Python lean) generated from them as outer-shell stubs.
- Deferred: `vue-app` and `dotnet-function` templates; the image-processor Function (FEAT-035).
- CI: GitHub Actions, written, not yet run (no remote). Nothing provisioned or deployed.

## 2. Environment preflight (2026-11-17, Windows 11)

| Tool | Found | Needed | Result |
| :-- | :-- | :-- | :-- |
| .NET SDK | 10.0.301 (also 8.0.x) | 10.0.x | ok; pinned in `global.json` |
| Go | 1.23.4, `GOTOOLCHAIN=auto` | 1.26 (modules need it) | ok; Go auto-fetched go1.26.0 into the module cache |
| Python | 3.10.9 | 3.12 | ok; uv-managed 3.12 per service (`requires-python >=3.12,<3.13`) |
| uv | 0.11.13 | — | ok |
| Node | 20.18.1 (nvm-windows) | ≥ 22.12 (create-vue, Vite 8, Vitest 5) | **blocking**; user switches to Node 24 via nvm |
| pnpm | 9.15.9 | — | ok |
| Docker | CLI 27.5.1, engine not running | engine running | **blocking**; user starts Docker Desktop |
| buf, copier, azd | not installed | — | not installed globally: `npx @bufbuild/buf@1.75.0`, `uvx copier@9.19.1`; azd not needed until deployment |
| gcc (cgo) | not installed | for `go test -race` | race detector runs in CI (Linux) only |

## 3. Generators and versions

| Unit | Generator | Version | Flags / answers | Basis |
| :-- | :-- | :-- | :-- | :-- |
| `global.json`, `HiveSpace.slnx` | `dotnet new globaljson`, `dotnet new sln` | SDK 10.0.301 | `--sdk-version 10.0.301 --roll-forward latestFeature`; `--format slnx` | repo-structure §2 |
| AppHost, ServiceDefaults | `dotnet new aspire-apphost`, `aspire-servicedefaults` (Aspire.ProjectTemplates) | 13.6.5 | `-f net10.0 --no-restore`; templates installed into a scratch `--debug:custom-hive`, nothing global | ADR-0015 |
| Gateway | `dotnet new web` | SDK 10.0.301 | `-f net10.0` | architecture §5.2 |
| Catalog | `dotnet new webapi` + 3× `classlib` | SDK 10.0.301 | `--no-openapi` (OpenAPI is contract-first in `contracts/`) | repo-structure §4.1 |
| Shared .NET libs | `dotnet new classlib` | SDK 10.0.301 | — | repo-structure §6 |
| Test projects | hand-written (no `xunit3` template in this SDK) | xUnit v3 4.0.3, Microsoft.Testing.Platform | `global.json` `test.runner = Microsoft.Testing.Platform` (required by .NET 10 `dotnet test`) | dev-standards §8.1 |
| EF tooling | local tool `dotnet-ef` (`dotnet-tools.json`) | 10.0.14 | migration `Initial` | dev-standards §7 |
| Go modules | hand-written per Go docs (no official generator) | Go 1.26.0 | `go.work`; each service `replace hivespace.dev/libs/go => ../../libs/go` so `go mod tidy` works with and without the workspace | repo-structure §4.3 |
| Python services | hand-written `pyproject.toml` + uv | uv 0.11.13 | per-service uv project with an editable path dependency on `libs/python/hivespace_common` | dev-standards §8.3 |
| gRPC stubs | `buf generate` | buf 1.75.0, remote plugins protocolbuffers/go, grpc/go, protocolbuffers/python, pyi, grpc/python | Go → `libs/go/gen`, Python → `libs/python/hivespace_contracts/src` | dev-standards §6.1 |
| Services from templates | Copier | 9.19.1 | see §6 | ADR-0020 |
| Web apps | `create-vue` | 3.24.0 | `--ts --router --pinia --vitest --eslint --prettier --bare`, once per app; then reshaped into `src/app`, `src/features`, `src/shared`, `src/locales` | repo-structure §8 |
| Web workspace | hand-written `package.json`, `pnpm-workspace.yaml`, `turbo.json` | pnpm 9.15.9, turbo 2.11.11 | — | repo-structure §8 |
| Theme CSS | `web/packages/ui/scripts/build-theme.mjs` (written here) | — | output is byte-identical to the `docs/design.md` §2.4 block; `--check` fails on drift | ADR-0021 |
| API client | `@hey-api/openapi-ts` | 0.99.0 | `@hey-api/client-fetch` plugin, one output per service spec (catalog for now) | dev-standards §6 |
| E2E | `@playwright/test` | 1.65.1 | Chromium; desktop and 360 px projects | architecture §8.6 |

Key package versions are pinned centrally in `Directory.Packages.props`, `go.mod`/`go.sum`, `uv.lock` and (later) `pnpm-lock.yaml`. Notable: Aspire 13.6.5, EF Core / Npgsql 10.0.14 / 10.0.3, Grpc 2.85.0, OpenTelemetry .NET 1.17.0, Testcontainers 4.17.0; Go: grpc 1.85.0, otel 1.47.0, azservicebus 1.11.0, pgx 5.13.0, golang-migrate 4.20.1; Python: FastAPI 0.163, SQLAlchemy 2.1, azure-servicebus 7.15.

## 4. Deviations from expectation (live reality won)

| Expected | Found | Done |
| :-- | :-- | :-- |
| `CommunityToolkit.Aspire.Hosting.Golang` | Marked obsolete in favour of the official `Aspire.Hosting.Go` | Switched to `Aspire.Hosting.Go` **13.6.5-preview.1.26559.7** (the only release is preview) |
| `dotnet test` with VSTest | .NET 10 SDK refuses VSTest with Microsoft.Testing.Platform | Opted in via `global.json` |
| EF Core Design 10.0.15 | Conflicted with the 10.0.14 that Npgsql/Aspire pull in | Pinned 10.0.14 |
| Python stubs under `hivespace_contracts/` package | Generated grpc code imports `hivespace.*` absolutely | Generated at `src/` root; distribution `hivespace-contracts`, package `hivespace` |
| uv workspace at the repo root (repo-structure §2) | Aspire's `AddUvicornApp(...).WithUv()` expects the venv in the service folder | Per-service uv projects with path dependencies; no root `pyproject.toml`. Candidate amendment to repo-structure §2 |
| One workflow per deployable (repo-structure §2) | Same jobs per stack | One path-filtered workflow per stack plus contracts, i18n, security, smoke. Candidate amendment |
| Copier templates edit workflow files | Shared files must not be edited by templates | Templates do not write workflows; per-stack workflows cover new services by path |
| Gateway random dev port | Playwright and docs need a stable URL | Gateway pinned to http://localhost:5080 in its launch profile |
| `pnpm` on PATH runs on Node 24 | The global `pnpm` lives in nvm's **Node 20.18** folder and its shim runs Node 20, so pnpm skipped native bindings that need Node ≥ 20.19 (Rolldown, oxlint) and Vite failed | Locally ran `npx -y pnpm@9.15.9 …` on Node 24. **Machine fix (user):** install pnpm under Node 24 (`npm i -g pnpm@9.15.9` while Node 24 is active, or `corepack enable`) |
| Go Testcontainers find Docker by default | On Docker Desktop for Windows the Ryuk reaper container died at start | Set `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock` (and `MSYS_NO_PATHCONV=1` in Git Bash). .NET Testcontainers needed nothing |
| Sheriff covers the whole app | Sheriff builds its graph from `.ts` files and cannot parse `.vue` imports | `.vue` ignored by Sheriff; ESLint `no-restricted-imports` covers .vue files (no deep feature imports, no feature-to-feature imports, API client only in `api.ts`) |
| `web/packages/config` (repo-structure §8) | Each app keeps the ESLint/Prettier/tsconfig create-vue generated | Not created; extract when a third app or a real divergence appears. Candidate amendment |
| `--frozen-lockfile` dotnet format clean | Generated files (EF migration) were CRLF against `.editorconfig` LF | Added `.gitattributes` (`* text=auto eol=lf`) and ran `dotnet format` |
| golang-migrate driver releases its connection | `pool.Close()` hung after migrations (the migrate driver kept a pooled connection) | `m.Close()` after `Up()` in Inventory; caught by the Testcontainers integration test |

## 5. Boilerplate removed

- Catalog: `WeatherForecast` sample, `Catalog.Api.http`, `appsettings.Development.json`, `Class1.cs` in each classlib.
- Gateway: default `Program.cs` "Hello World", `https` launch profile.
- Shared libs: `Class1.cs`.
- AppHost: default single-line `AppHost.cs` replaced by `Resources/*.cs`; launch profile order changed so `http` is the default (OTLP over plain HTTP so Go and Python exporters need no dev certificate).

## 6. Services generated from templates

```bash
uvx copier@9.19.1 copy --trust --defaults templates/dotnet-service . -a services/identity/.copier-answers.yml --data name=identity --data shape=rich
```

The same for `order` and `payment` (rich) and `media` (lean); `cart` from `templates/go-service` (`shape=lean store=mongo`); `search-ai` from `templates/python-service` (`shape=lean`). Each passed its own tests on generation. The rich Go variant (`shape=rich store=postgres`) was also rendered into a scratch folder and built and tested, then deleted.

## 7. Local stack

| Action | Command |
| :-- | :-- |
| Up | `dotnet run --project apphost/HiveSpace.AppHost` |
| Down | Ctrl+C in that terminal (Postgres and MongoDB containers are `Persistent` and keep running for fast restarts) |
| Reset data | stop the AppHost, then remove the containers and the `hivespace-postgres`, `hivespace-mongo`, `hivespace-azurite` volumes |

**Cloud sandbox and CI runs (FEAT-010 finding).** `Parameters:postgres-password` is a secret parameter with no default. If it is missing, the AppHost waits for it forever (nothing says so on the console) and no .NET service starts. Supply it as the environment variable `Parameters__postgres-password` (a throwaway value is fine for tests; `dotnet user-secrets` is the local way). `tests/system`, `tests/e2e` and the AppHost all need it. `dotnet run` goes through the Aspire CLI and stalled in the sandbox with only the web and Python resources started; running the built `apphost/HiveSpace.AppHost/bin/Debug/net10.0/HiveSpace.AppHost` with the `http` launch-profile variables works, and Playwright then reuses it (`reuseExistingServer`). `scripts/cloud-setup.sh` should export the variable (plan item P-11).

No compose file: Aspire is the local orchestrator by ADR-0015. Postgres image `pgvector/pgvector:pg17` matches the Neon major version planned for the demo. MongoDB has no Azure emulator (R-13) and runs as a plain container.

Local configuration is generated by the AppHost at run time (connection strings, ports, OTLP endpoint) — there is no hand-made local config file to recreate. Each unit's variable names are in its `.env.example` (Go, Python) or `appsettings.json` (.NET).

## 8. Stubs and where they are replaced

| Stub | Location | Replaced by |
| :-- | :-- | :-- |
| ~~Fixed `dev-user` identity header~~ | `gateway/src/Gateway/Proxy/GatewayTransforms.cs` | **Done in FEAT-001**: BFF cookie, token relay of the validated session; no `dev-user` branch remains |
| One seeded category, `availabilityProbe` field | Catalog `CatalogDbContext`, `ListCategoriesHandler` | FEAT-014, FEAT-015 |
| `POST /v1/catalog/ping` and the `Ping` event | Catalog, Inventory, Notification consumers (Notification now only records `Ping` in its inbox) | removed after FEAT-012 introduces real events |
| `GetAvailability` stub | Inventory `internal/app` | FEAT-015 |
| ~~`GET /v1/identity/ping`~~ | Identity | **Done in FEAT-001**: replaced by the users endpoints and the OpenIddict endpoints |
| `GET /v1/<name>/ping` in generated services | order, payment, media, cart, search-ai | each service's first slice |

## 9. Status and verification

See `docs/.scaffold-progress.md` for the live tracker; results are recorded here when verification completes.

| Check | Result |
| :-- | :-- |
| `dotnet build HiveSpace.slnx` (warnings as errors) | 33 projects, 0 warnings |
| Catalog unit + architecture tests | pass (2 + 3) |
| Gateway routing test (identity header stripped, correlation forwarded) | pass |
| Generated .NET stubs' tests | pass (identity 5, order 5, payment 5, media 3) |
| Go build, vet, `go test -short` (libs, inventory, cart) | pass |
| Notification: ruff, mypy --strict, pytest, import-linter | pass |
| search-ai: ruff, mypy --strict, pytest, import-linter | pass |
| `buf lint` | pass |
| vi/en key parity | pass |
| Catalog integration (Testcontainers Postgres) | pass (2); contract tests moved to cucumber-js (ADR-0025) |
| Inventory integration test (Testcontainers Postgres, inbox dedupe) | pass |
| Web: `turbo run lint typecheck test build` (storefront, seller centre, core, ui) | 12/12 tasks pass |
| Token wiring | `theme.css` byte-identical to design.md §2.4; changing `color.brand.500` changes `theme.css` and makes `ui:lint` fail; restored |
| Boundary rules fail on a planted violation, then pass | NetArchTest (Application → AspNetCore) · depguard (pgx in `internal/app`) · import-linter (azure in a feature) · Sheriff + ESLint (feature → feature, deep import) — all four rejected, all clean after removal |
| `dotnet format --verify-no-changes` | pass |
| `golangci-lint` (libs/go, inventory, cart) | 0 issues |
| Spectral (OpenAPI) | 0 errors (8 description warnings on template sample operations) |
| Workflow files (`@action-validator/cli`) | 8/8 valid |
| AppHost cold start, system tests, Playwright skeleton test | **pending**: Docker Desktop's engine returns "Internal Server Error" since the Testcontainers runs; needs a Docker Desktop restart |
| Deployed half of the done-when | pending initial deployment (not this step) |

## 9. Shared UI ported from the reference (`references/hivespace.web/packages/shared`)

Reviewed the copied reference package and ported a presentational subset into `web/packages/ui`, re-styled to the design tokens. It is not a dependency of the apps.

| Ported (adapted) | How it differs from the reference |
| :-- | :-- |
| Button, Input, Select, Spinner, Badge, Alert, Tabs, Pagination | Semantic role tokens instead of raw `gray`/`blue` utilities (dark mode comes from the tokens); Button variants per design.md §4.1 (no `success`/`warning`); labels and error text come in as props or slots; lucide icons |
| AppShell, AppSidebar, AppHeader, `useSidebar` | No vue-router, auth or notification code: links render through a `linkAs` prop (RouterLink in the apps), labels come from the app's i18n; 290/90 px widths from the tokens; collapse, hover-expand, mobile drawer, Escape to close, skip link |
| Language switcher, theme toggle | Globe icon + code in one 44 px bordered form with a flag menu; `rounded-lg` square icon button (aligned to the FEAT-001 mockups) |

Left out on purpose: `useAuth` (OIDC), `utils/api.ts` (axios), SignalR notifications, payments, user-profile, media-upload, charts, date pickers, Dropzone, Modal and DropdownMenu (a focus trap is required by design.md §5, so they are rebuilt with the first screen that needs them). Reasons: our API client is generated and imported only in `api.ts`; the SRS has no in-app notification centre; the reference targets pinia 3, vue-router 4, TypeScript 5.7 and Vite 6 (ours: 4, 5, 6, 8).

Icons use `@lucide/vue`: `lucide-vue-next` named in design.md §4.9 is deprecated in its favour. `packages/ui` now has its own Vitest setup.

A full component-by-component review of the reference package (ported, needs adapting, skipped, with reasons) is in [shared-ui-review.md](shared-ui-review.md).
