// Package app holds Inventory's use cases and the ports they need.
package app

import (
	"context"
	"log/slog"

	"hivespace.dev/services/inventory/internal/domain"
)

// Inbox records processed events; Record returns false when the event was already handled (NFR-REL-005).
type Inbox interface {
	Record(ctx context.Context, eventID, consumer string) (bool, error)
}

// Consumer is this service's inbox consumer name.
const Consumer = "inventory"

// Service is the application layer.
type Service struct {
	inbox Inbox
}

// New builds the application layer.
func New(inbox Inbox) *Service { return &Service{inbox: inbox} }

// GetAvailability is a stub in the skeleton (STUB: FEAT-015 reads real stock).
func (s *Service) GetAvailability(_ context.Context, variantIDs []string) ([]domain.VariantAvailability, string) {
	items := make([]domain.VariantAvailability, 0, len(variantIDs))
	for _, id := range variantIDs {
		items = append(items, domain.VariantAvailability{VariantID: id, Available: 0})
	}
	return items, domain.SkeletonProbe
}

// HandleEvent processes one event once. It returns true if the event was new.
func (s *Service) HandleEvent(ctx context.Context, eventID, eventType string) (bool, error) {
	isNew, err := s.inbox.Record(ctx, eventID, Consumer)
	if err != nil || !isNew {
		return false, err
	}
	// Skeleton: Ping has no side effect beyond the inbox row. Real handlers (VariantCreated, OrderPlaced…)
	// run in the same transaction as the inbox insert once they exist.
	slog.InfoContext(ctx, "event processed", "eventId", eventID, "eventType", eventType)
	return true, nil
}
