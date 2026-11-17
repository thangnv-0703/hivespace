# Inventory (Go, rich)

Stock, reservations and their expiry (architecture §5.2, ADR-0007). In the walking skeleton it serves a stub `GetAvailability` over gRPC and consumes the `Ping` event once through its inbox. Stock arrives with FEAT-012, reservations with FEAT-017.

System-wide setup, the local stack and conventions are in the [root README](../../README.md).

| | |
| :-- | :-- |
| Layout | `cmd/inventory` wiring · `internal/domain` pure rules · `internal/app` use cases and ports · `internal/transport/{grpc,events}` · `internal/store/postgres` · `internal/platform` |
| Config | [`.env.example`](.env.example) (set by the AppHost) |
| Migrations | `migrations/*.sql`, golang-migrate, applied on start |
| Boundaries | depguard rules in [`/.golangci.yml`](../../.golangci.yml) |

Commands (from this folder):

```bash
go test -short ./...
```

```bash
go test ./...
```

The second runs the Postgres integration test and needs Docker. CI adds `-race`, which needs cgo.
