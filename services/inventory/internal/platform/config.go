// Package platform holds configuration and server wiring. Everything comes from the environment
// that the AppHost (or the cloud) sets; nothing is hard-coded.
package platform

import (
	"fmt"
	"os"
)

// Config is read from environment variables; see services/inventory/.env.example.
type Config struct {
	HTTPPort           string
	GRPCPort           string
	DatabaseURL        string
	MessagingConn      string
	EventsTopic        string
	EventsSubscription string
}

// Load reads and validates the configuration.
func Load() (Config, error) {
	c := Config{
		HTTPPort:           os.Getenv("PORT"),
		GRPCPort:           os.Getenv("GRPC_PORT"),
		DatabaseURL:        os.Getenv("DATABASE_URL"),
		MessagingConn:      os.Getenv("ConnectionStrings__messaging"),
		EventsTopic:        os.Getenv("EVENTS_TOPIC"),
		EventsSubscription: os.Getenv("EVENTS_SUBSCRIPTION"),
	}
	for name, v := range map[string]string{"PORT": c.HTTPPort, "GRPC_PORT": c.GRPCPort, "DATABASE_URL": c.DatabaseURL} {
		if v == "" {
			return c, fmt.Errorf("missing required environment variable %s", name)
		}
	}
	return c, nil
}
