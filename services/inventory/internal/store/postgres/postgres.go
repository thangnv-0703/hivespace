// Package postgres is the only package that imports a database driver (depguard).
package postgres

import (
	"context"
	"errors"
	"fmt"

	"github.com/golang-migrate/migrate/v4"
	pgxmigrate "github.com/golang-migrate/migrate/v4/database/pgx/v5"
	"github.com/golang-migrate/migrate/v4/source/iofs"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/jackc/pgx/v5/stdlib"

	"hivespace.dev/services/inventory/migrations"
)

// Open connects and applies migrations (local and test; the cloud runs them in a deploy step).
func Open(ctx context.Context, url string) (*pgxpool.Pool, error) {
	pool, err := pgxpool.New(ctx, url)
	if err != nil {
		return nil, fmt.Errorf("connect: %w", err)
	}
	if err := migrateUp(pool); err != nil {
		pool.Close()
		return nil, err
	}
	return pool, nil
}

func migrateUp(pool *pgxpool.Pool) error {
	source, err := iofs.New(migrations.FS, ".")
	if err != nil {
		return err
	}
	db := stdlib.OpenDBFromPool(pool)
	defer func() { _ = db.Close() }()
	driver, err := pgxmigrate.WithInstance(db, &pgxmigrate.Config{})
	if err != nil {
		return err
	}
	m, err := migrate.NewWithInstance("iofs", source, "pgx5", driver)
	if err != nil {
		return err
	}
	// Close releases the connection the driver holds; without it pool.Close blocks forever.
	defer func() { _, _ = m.Close() }()
	if err := m.Up(); err != nil && !errors.Is(err, migrate.ErrNoChange) {
		return fmt.Errorf("migrate: %w", err)
	}
	return nil
}

// Inbox implements app.Inbox with a primary key on (event_id, consumer).
type Inbox struct{ Pool *pgxpool.Pool }

// Record inserts the inbox row; zero rows affected means the event was already processed.
func (i Inbox) Record(ctx context.Context, eventID, consumer string) (bool, error) {
	tag, err := i.Pool.Exec(ctx,
		`INSERT INTO inbox (event_id, consumer, processed_at) VALUES ($1, $2, now()) ON CONFLICT DO NOTHING`,
		eventID, consumer)
	if err != nil {
		return false, err
	}
	return tag.RowsAffected() == 1, nil
}
