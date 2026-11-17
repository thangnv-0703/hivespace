# System tests

Cross-service tests that boot the whole AppHost with `Aspire.Hosting.Testing` (repo-structure §5).

| Test | Proves |
| :-- | :-- |
| `SkeletonTests` | Every service is healthy; `GET /v1/categories` goes gateway → Catalog → Postgres and Catalog → Inventory (gRPC); a `Ping` event goes Catalog outbox → Service Bus emulator → Inventory (Go) and Notification (Python) inboxes |

The mandatory risk tests (dev-standards §9.2) join this project with their slices: saga failure injection (FEAT-023), triple payment callback (FEAT-029), duplicate events per consumer.

Needs Docker, Go, uv and pnpm, the same as running the AppHost:

```bash
dotnet test --project tests/system/HiveSpace.SystemTests
```
