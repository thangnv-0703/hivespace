# Service templates (Copier)

New services are generated, never copied by hand (ADR-0020, dev-standards §8.0). Copier runs project-locally through `uvx`; nothing is installed globally.

| Template | Options | Built from | Used by |
| :-- | :-- | :-- | :-- |
| `dotnet-service` | `name`, `shape` (rich, lean) | Catalog (rich) | identity, order, payment (rich); media (lean) |
| `go-service` | `name`, `shape` (rich, lean), `store` (postgres, mongo) | Inventory (rich) | cart (lean, mongo) |
| `python-service` | `name`, `shape` (lean, rich) | Notification (lean) | search-ai (lean) |
| `vue-app`, `dotnet-function` | — | deferred | the image Function arrives with FEAT-035 |

Each template renders from the repository root and creates the outer shell (repo-structure §3): the service folder with `/health`, `/ready` and one sample `GET /v1/<name>/ping` plus its test, a `Dockerfile`, a `README.md`, an `.env.example` (Go, Python), `contracts/openapi/<name>.yaml`, and `services/<name>/.copier-answers.yml`.

**Shared files are not edited by templates** (AppHost resources, `HiveSpace.slnx`, `go.work`, gateway routes). Copier prints the lines to add after it finishes, so `copier update` stays safe.

Generate (replace the values):

```bash
uvx copier@9.19.1 copy --trust templates/dotnet-service . -a services/order/.copier-answers.yml --data name=order --data shape=rich
```

Update an existing service after a template fix:

```bash
uvx copier@9.19.1 update --trust -a services/order/.copier-answers.yml
```

The templates do not yet carry messaging, persistence or gRPC wiring; each service adds those in its first slice, copying the pattern from the skeleton service of the same stack (Catalog, Inventory, Notification).
