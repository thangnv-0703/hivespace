# Catalog (.NET, rich)

Category tree, products, variants, reviews (architecture §5.2). In the walking skeleton it serves `GET /v1/categories` from Postgres (one seeded row), calls Inventory over gRPC to prove the path, and writes a `Ping` event to its outbox on `POST /v1/catalog/ping`. Real catalog features start with FEAT-012 and FEAT-014.

System-wide setup, the local stack and conventions are in the [root README](../../README.md). This is the reference service the `templates/dotnet-service` template was built from.

| Project | Holds |
| :-- | :-- |
| `Catalog.Domain` | Entities and rules; no framework or vendor references |
| `Catalog.Application` | Handlers (`Features/`) and ports (`Abstractions/`) |
| `Catalog.Infrastructure` | EF Core (`Persistence/`, migrations), gRPC clients, outbox wiring |
| `Catalog.Api` | Composition root and endpoints |
| `tests/` | Unit, Integration (Testcontainers Postgres), Contract (cucumber-js over `contracts/features/catalog`, in `tests/contract`), Architecture (NetArchTest) |

Config: connection strings `catalogdb` and `messaging` (set by the AppHost); `Database:MigrateOnStartup` (default true, false in the container image).

New migration (from the repository root):

```bash
dotnet ef migrations add <Name> --project services/catalog/src/Catalog.Infrastructure --startup-project services/catalog/src/Catalog.Infrastructure --output-dir Persistence/Migrations
```
