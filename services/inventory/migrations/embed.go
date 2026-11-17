// Package migrations embeds the golang-migrate SQL files so the binary carries its schema.
package migrations

import "embed"

// FS holds the versioned migrations.
//
//go:embed *.sql
var FS embed.FS
