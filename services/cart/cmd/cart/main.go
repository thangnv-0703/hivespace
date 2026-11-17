// Command cart runs the cart service.
package main

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"hivespace.dev/libs/go/otelx"
	"hivespace.dev/services/cart/internal/cart"
	"hivespace.dev/services/cart/internal/platform"
	"hivespace.dev/services/cart/internal/store/mongo"
)

func main() {
	if err := run(); err != nil {
		slog.Error("cart stopped", "error", err)
		os.Exit(1)
	}
}

func run() error {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	shutdown, err := otelx.Setup(ctx, "cart")
	if err != nil {
		return err
	}
	defer func() { _ = shutdown(context.Background()) }()

	cfg, err := platform.Load()
	if err != nil {
		return err
	}

	db, err := mongo.Open(ctx, cfg.DatabaseURL)
	if err != nil {
		return err
	}
	defer db.Close()

	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", func(w http.ResponseWriter, _ *http.Request) { _, _ = w.Write([]byte("Healthy")) })
	mux.HandleFunc("GET /ready", func(w http.ResponseWriter, r *http.Request) {
		if err := db.Ping(r.Context()); err != nil {
			http.Error(w, "Unhealthy", http.StatusServiceUnavailable)
			return
		}
		_, _ = w.Write([]byte("Healthy"))
	})
	cart.Register(mux)

	server := &http.Server{Addr: ":" + cfg.HTTPPort, Handler: mux, ReadHeaderTimeout: 5 * time.Second}
	go func() {
		if err := server.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			slog.Error("http server", "error", err)
		}
	}()
	slog.Info("cart started", "http", cfg.HTTPPort)
	<-ctx.Done()

	shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	return server.Shutdown(shutdownCtx)
}
