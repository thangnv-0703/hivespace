// Package otelx gives Go services the same hosting contract as .NET ServiceDefaults (dev-standards §11):
// OTLP traces, W3C propagation and JSON logs with a correlation ID.
package otelx

import (
	"context"
	"log/slog"
	"os"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc"
	"go.opentelemetry.io/otel/propagation"
	"go.opentelemetry.io/otel/sdk/resource"
	sdktrace "go.opentelemetry.io/otel/sdk/trace"
)

// Setup configures JSON logging and, when OTEL_EXPORTER_OTLP_ENDPOINT is set (the AppHost sets it),
// an OTLP trace exporter. It returns a shutdown func to flush spans on exit.
func Setup(ctx context.Context, service string) (func(context.Context) error, error) {
	slog.SetDefault(slog.New(slog.NewJSONHandler(os.Stdout, nil)).With("service", service))
	otel.SetTextMapPropagator(propagation.NewCompositeTextMapPropagator(propagation.TraceContext{}, propagation.Baggage{}))

	if os.Getenv("OTEL_EXPORTER_OTLP_ENDPOINT") == "" {
		return func(context.Context) error { return nil }, nil
	}

	exporter, err := otlptracegrpc.New(ctx)
	if err != nil {
		return nil, err
	}
	name := os.Getenv("OTEL_SERVICE_NAME")
	if name == "" {
		name = service
	}
	// Schemaless, so it never conflicts with the schema URL of the SDK's default resource.
	res, err := resource.Merge(resource.Default(), resource.NewSchemaless(attribute.String("service.name", name)))
	if err != nil {
		return nil, err
	}
	provider := sdktrace.NewTracerProvider(sdktrace.WithBatcher(exporter), sdktrace.WithResource(res))
	otel.SetTracerProvider(provider)
	return provider.Shutdown, nil
}
