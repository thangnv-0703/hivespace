// Package mongo is the only package that imports the MongoDB driver (depguard). Single-document atomic
// updates only, so no replica set is needed (ADR-0018, risk R-13).
package mongo

import (
	"context"

	"go.mongodb.org/mongo-driver/v2/mongo"
	"go.mongodb.org/mongo-driver/v2/mongo/options"
)

// DB wraps the client for the service's own database.
type DB struct{ client *mongo.Client }

// Open connects lazily; Ping reports readiness. Versioned startup scripts (collections, indexes) arrive with the first slice.
func Open(_ context.Context, url string) (*DB, error) {
	client, err := mongo.Connect(options.Client().ApplyURI(url))
	if err != nil {
		return nil, err
	}
	return &DB{client: client}, nil
}

// Ping checks the connection.
func (d *DB) Ping(ctx context.Context) error { return d.client.Ping(ctx, nil) }

// Close disconnects.
func (d *DB) Close() { _ = d.client.Disconnect(context.Background()) }
