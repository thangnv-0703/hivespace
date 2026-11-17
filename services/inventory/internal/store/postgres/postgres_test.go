package postgres

import (
	"context"
	"testing"
	"time"

	"github.com/testcontainers/testcontainers-go"
	tcpostgres "github.com/testcontainers/testcontainers-go/modules/postgres"
	"github.com/testcontainers/testcontainers-go/wait"
)

// Integration test against a real Postgres container (dev-standards §8.2). Needs Docker; skipped with -short.
func TestInboxDedupesAgainstRealPostgres(t *testing.T) {
	if testing.Short() {
		t.Skip("integration test")
	}
	ctx := context.Background()
	container, err := tcpostgres.Run(ctx, "pgvector/pgvector:pg17",
		tcpostgres.WithDatabase("inventory"),
		testcontainers.WithWaitStrategy(wait.ForLog("database system is ready to accept connections").
			WithOccurrence(2).WithStartupTimeout(60*time.Second)))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = container.Terminate(ctx) })

	url, err := container.ConnectionString(ctx, "sslmode=disable")
	if err != nil {
		t.Fatal(err)
	}
	pool, err := Open(ctx, url)
	if err != nil {
		t.Fatal(err)
	}
	defer pool.Close()

	inbox := Inbox{Pool: pool}
	for i, want := range []bool{true, false} {
		got, err := inbox.Record(ctx, "01930000-0000-7000-8000-000000000009", "inventory")
		if err != nil {
			t.Fatal(err)
		}
		if got != want {
			t.Fatalf("delivery %d: got %v, want %v", i+1, got, want)
		}
	}
}
