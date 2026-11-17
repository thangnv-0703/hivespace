// Package grpc adapts the generated InventoryService to the app layer. Thin: no rules here.
package grpc

import (
	"context"

	inventoryv1 "hivespace.dev/libs/go/gen/hivespace/inventory/v1"
	"hivespace.dev/services/inventory/internal/app"
)

// Server implements inventoryv1.InventoryServiceServer.
type Server struct {
	inventoryv1.UnimplementedInventoryServiceServer
	App *app.Service
}

// GetAvailability returns availability per variant (skeleton: stub values plus a probe).
func (s Server) GetAvailability(ctx context.Context, req *inventoryv1.GetAvailabilityRequest) (*inventoryv1.GetAvailabilityResponse, error) {
	items, probe := s.App.GetAvailability(ctx, req.GetVariantIds())
	resp := &inventoryv1.GetAvailabilityResponse{Probe: probe}
	for _, it := range items {
		resp.Items = append(resp.Items, &inventoryv1.VariantAvailability{VariantId: it.VariantID, Available: it.Available})
	}
	return resp, nil
}
