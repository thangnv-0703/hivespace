// Command inventory runs the Inventory service: HTTP health endpoints, the gRPC API and the event consumer.
package main

import (
	"context"
	"errors"
	"log/slog"
	"net"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"go.opentelemetry.io/contrib/instrumentation/google.golang.org/grpc/otelgrpc"
	"google.golang.org/grpc"

	"hivespace.dev/libs/go/eventbus"
	inventoryv1 "hivespace.dev/libs/go/gen/hivespace/inventory/v1"
	"hivespace.dev/libs/go/otelx"
	"hivespace.dev/services/inventory/internal/app"
	"hivespace.dev/services/inventory/internal/platform"
	"hivespace.dev/services/inventory/internal/store/postgres"
	"hivespace.dev/services/inventory/internal/transport/events"
	grpctransport "hivespace.dev/services/inventory/internal/transport/grpc"
)

func main() {
	if err := run(); err != nil {
		slog.Error("inventory stopped", "error", err)
		os.Exit(1)
	}
}

func run() error {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	shutdown, err := otelx.Setup(ctx, "inventory")
	if err != nil {
		return err
	}
	defer func() { _ = shutdown(context.Background()) }()

	cfg, err := platform.Load()
	if err != nil {
		return err
	}

	pool, err := postgres.Open(ctx, cfg.DatabaseURL)
	if err != nil {
		return err
	}
	defer pool.Close()

	svc := app.New(postgres.Inbox{Pool: pool})

	// gRPC on its own port (h2c), internal only (ADR-0019).
	grpcServer := grpc.NewServer(grpc.StatsHandler(otelgrpc.NewServerHandler()))
	inventoryv1.RegisterInventoryServiceServer(grpcServer, grpctransport.Server{App: svc})
	lis, err := net.Listen("tcp", ":"+cfg.GRPCPort)
	if err != nil {
		return err
	}
	go func() {
		if err := grpcServer.Serve(lis); err != nil {
			slog.Error("grpc server", "error", err)
		}
	}()

	// /health is liveness; /ready also checks the database (repo-structure §3).
	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", func(w http.ResponseWriter, _ *http.Request) { _, _ = w.Write([]byte("Healthy")) })
	mux.HandleFunc("GET /ready", func(w http.ResponseWriter, r *http.Request) {
		if err := pool.Ping(r.Context()); err != nil {
			http.Error(w, "Unhealthy", http.StatusServiceUnavailable)
			return
		}
		_, _ = w.Write([]byte("Healthy"))
	})
	httpServer := &http.Server{Addr: ":" + cfg.HTTPPort, Handler: mux, ReadHeaderTimeout: 5 * time.Second}
	go func() {
		if err := httpServer.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			slog.Error("http server", "error", err)
		}
	}()

	if cfg.MessagingConn != "" {
		sub := eventbus.Subscription{ConnectionString: cfg.MessagingConn, Topic: cfg.EventsTopic, Name: cfg.EventsSubscription}
		go func() {
			if err := sub.Run(ctx, events.Handler(svc)); err != nil {
				slog.Error("event consumer", "error", err)
			}
		}()
	}

	slog.Info("inventory started", "http", cfg.HTTPPort, "grpc", cfg.GRPCPort)
	<-ctx.Done()

	grpcServer.GracefulStop()
	shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	return httpServer.Shutdown(shutdownCtx)
}
