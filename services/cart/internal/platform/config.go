// Package platform holds configuration. Everything comes from the environment; nothing is hard-coded.
package platform

import (
	"fmt"
	"os"
)

// Config is read from environment variables; see .env.example.
type Config struct {
	HTTPPort    string
	DatabaseURL string
}

// Load reads and validates the configuration.
func Load() (Config, error) {
	c := Config{
		HTTPPort:    os.Getenv("PORT"),
		DatabaseURL: os.Getenv("ConnectionStrings__cartdb"),
	}
	if c.HTTPPort == "" {
		return c, fmt.Errorf("missing required environment variable PORT")
	}
	if c.DatabaseURL == "" {
		return c, fmt.Errorf("missing database connection setting")
	}
	return c, nil
}
