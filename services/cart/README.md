# Cart (Go, lean, mongo)

Generated from `templates/go-service`. Its responsibilities are in [architecture §5.2](../../docs/architecture.md); its first feature slice is in [the implementation plan](../../docs/implementation-plan.md). Until then it serves the outer shell only: `/health`, `/ready` and the sample `GET /v1/cart/ping`.

System-wide setup, the local stack and conventions are in the [root README](../../README.md). Config names: [`.env.example`](.env.example).

```bash
go test -short ./...
```

Update from the template:

```bash
uvx copier@9.19.1 update -a services/cart/.copier-answers.yml
```
