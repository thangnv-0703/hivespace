package otelx

import (
	"context"
	"testing"
)

// Regression: Setup must succeed when the AppHost sets an OTLP endpoint (it failed on a schema URL conflict).
func TestSetupWithOTLPEndpoint(t *testing.T) {
	t.Setenv("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317")
	shutdown, err := Setup(context.Background(), "test")
	if err != nil {
		t.Fatalf("Setup: %v", err)
	}
	_ = shutdown(context.Background())
}
