// Package events subscribes Inventory to the broker and hands envelopes to the app layer.
package events

import (
	"context"

	"hivespace.dev/libs/go/eventbus"
	"hivespace.dev/services/inventory/internal/app"
)

// Handler returns the eventbus handler for Inventory's subscription.
func Handler(svc *app.Service) eventbus.Handler {
	return func(ctx context.Context, e eventbus.Envelope) error {
		_, err := svc.HandleEvent(ctx, e.EventID, e.EventType)
		return err
	}
}
