# AGENTS.md — HiveSpace

Instructions for any coding agent working in this repository. `CLAUDE.md` only points here; this file is the single source of truth.

## Where things are

| Need | Read |
| :-- | :-- |
| Requirements (FR, NFR, CON IDs) | `docs/srs.md`, `docs/use-cases.md`, `docs/rtm.md` |
| Design, services, events, gRPC calls | `docs/architecture.md`, decisions in `docs/adr/` |
| Build order, features (`FEAT-*`), walking skeleton | `docs/implementation-plan.md` |
| Conventions and rules (and the tool that enforces each) | `docs/dev-standards.md` |
| Folder layout per stack and shape | `docs/repo-structure.md` |
| UI: design system and tokens | `docs/design.md` and `docs/tokens.json` (load both before building any screen) |
| UI: screens (`SCR-*`) and flows | `docs/ux-foundations.md` |
| How the scaffold was generated, versions, deviations | `docs/scaffold-notes.md` |
| Interfaces between units | `contracts/` (OpenAPI, proto, event schemas, Gherkin, error codes) — change the contract first |

## Commands

```bash
dotnet run --project apphost/HiveSpace.AppHost
```
Starts everything (Postgres, MongoDB, Service Bus emulator, Azurite, Mailpit, all services, both SPAs). Storefront: http://localhost:5080, seller centre: http://seller.localhost:5080 (both through the gateway, ADR-0027). Needs Docker running.

```bash
dotnet build HiveSpace.slnx && dotnet test --solution HiveSpace.slnx
```

```bash
go test -short ./libs/go/... ./services/inventory/... ./services/cart/...
```

```bash
cd services/notification && uv run pytest && uv run mypy && uv run lint-imports
```

```bash
cd web && pnpm install && pnpm turbo run lint typecheck test build
```

```bash
dotnet test --project tests/system/HiveSpace.SystemTests
```

```bash
cd tests/contract && pnpm install && pnpm test
```
Gherkin API contract tests for every service (cucumber-js over `contracts/features/`, ADR-0025). Needs Docker and the .NET SDK; it starts each service and a Postgres container itself.

```bash
cd tests/e2e && pnpm install && pnpm test
```
End-to-end browser tests (Playwright). User journeys are Gherkin in `tests/e2e/features/` (playwright-bdd, ADR-0026); technical checks are plain specs in `tests/e2e/specs/`. Runs against the local AppHost stack (starts it if nothing answers on `http://localhost:5080`).

New service: generate it from `templates/` (see `templates/README.md`); never copy a service by hand.

## Cloud sessions

`scripts/cloud-setup.sh` (run by the `SessionStart` hook in `.claude/settings.json`) installs the .NET SDK, pnpm and uv when Claude Code runs in the cloud, and prints whether Docker is available. Without Docker, Testcontainers integration tests, `tests/contract`, `tests/system`, the AppHost and `tests/e2e` cannot run. Then: run every other check, never mark a task done when its done-when needs one of those, and list them as "pending local run" in the PR. Project skills, including `/feature-implementation FEAT-NNN`, are in `.claude/skills/` (see its `README.md`).

## Boundary rules (enforced by tooling — do not work around them)

- No service references another service's code or database; data crosses by gRPC or events only.
- .NET rich services: `Api → Application → Domain`, `Infrastructure → Application`; Domain has no framework or vendor reference (NetArchTest in each service's tests).
- Go: `internal/domain` imports nothing from `internal/`; only `internal/store` imports a driver; only `transport/grpc`, `clients/` and `cmd/` import generated stubs (depguard, `.golangci.yml`).
- Python: features do not import each other; vendor SDKs only under `providers/` (import-linter, each `pyproject.toml`).
- Web: features import each other only through `index.ts`; only `api.ts` imports the API client; apps never import each other (Sheriff).
- `libs/` holds cross-cutting code only (Money, messaging, errors, telemetry) — never a service concept.
- Generated code (`libs/go/gen`, `libs/python/hivespace_contracts`, `web/packages/api-client`) is regenerated, never edited.

## Project principles

These apply to every design and architecture task, for the whole system or for a single service.

- **Learning first.** The main purpose of this project is learning. When designing the system or a service, you may choose any technology that suits it, including Azure Functions and NoSQL databases, even if no other service uses it. Do not treat the current stack in `docs/srs.md` or `docs/dev-standards.md` as a limit. Record each choice in an ADR in `docs/adr/` with a short note on what it teaches.
- **Guardrails that still win:** the monthly budget (NFR-COST-001), correctness of checkout, stock and payment (CON-7), and the solo-builder capacity limit (risk R-1 and its cut order in `docs/architecture.md`).
- **Language-neutral conventions stay in every stack:** money as amount plus currency, stable error codes, idempotency, tracing, server-side authorization, and the event envelope with outbox and inbox (see `docs/dev-standards.md` section 5).
- **Communication:** internal synchronous calls between services use gRPC (ADR-0019); asynchronous events go through the broker (ADR-0002); the public API is REST/OpenAPI through the gateway/BFF; payment callbacks stay REST.
- "Simplicity First" below applies to code, not to how many technologies the project explores.
- Where things live: `docs/architecture.md` (design), `docs/adr/` (decisions), `docs/dev-standards.md` (conventions), `docs/srs.md` (requirements).
- **UI work:** load `docs/design.md` (with `docs/tokens.json`) before building any screen; screens and flows with their `SCR-*` IDs are in `docs/ux-foundations.md`.


## Behavioral Guidelines

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

### 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them; don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it; don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that your changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" -> "Write tests for invalid inputs, then make them pass"
- "Fix the bug" -> "Write a test that reproduces it, then make it pass"
- "Refactor X" -> "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

```text
1. [Step] -> verify: [check]
2. [Step] -> verify: [check]
3. [Step] -> verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
