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

## Loading the category tree (FEAT-014)

The platform's category tree, attributes and list values are loaded by an operator command, not an endpoint:

```bash
# Locally: start `catalog-load-categories` from the Aspire dashboard (manual start; the report is in its console log), or:
# (dotnet run starts the app in the project directory, so pass an absolute --source)
ConnectionStrings__catalogdb='Host=...' dotnet run --project services/catalog/src/Catalog.Api -- load-categories --source "$PWD/data/category-tree"
```

The command checks the whole file against the stored tree, then either applies every change in one transaction and queues `CategoryTreeChanged`, or changes nothing and lists every problem. It prints a report and exits with `0` (applied or unchanged), `2` (refused) or `1` (unexpected failure, nothing committed). The file format is in [`data/category-tree/README.md`](../../data/category-tree/README.md); the rules are in `docs/features/FEAT-014-category-tree-with-attributes/technical-design.md`. The event leaves the outbox the next time the running Catalog service starts (the command does not run the relay).

**After migration `SharedAttributeLists` (FEAT-014 amendment):** the migration empties the category tree tables, because the list settings moved from each category link to the attribute and per-link lists cannot be merged faithfully in SQL. Reload the tree afterwards (start `catalog-load-categories`, or run the command above). The loader reads the v2 layout only (`attributes.json` plus link-only `category-attributes/*.json`). Before loading the crawler's real export, reset the local Catalog database or load into an empty one: the sample's categories are not all in the crawl, so loading it over the sample is refused as `category.removed`.
