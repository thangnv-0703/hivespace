package eventbus

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/messaging/azservicebus"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/propagation"
	"go.opentelemetry.io/otel/trace"
)

// Handler processes one envelope. Returning an error abandons the message so the broker redelivers it.
type Handler func(ctx context.Context, e Envelope) error

// Subscription receives from one topic subscription (one per consuming service, ADR-0002).
type Subscription struct {
	ConnectionString string
	Topic            string
	Name             string
}

// Run receives until ctx is cancelled. Delivery is at-least-once; handlers must be idempotent (NFR-REL-005).
func (s Subscription) Run(ctx context.Context, handle Handler) error {
	client, err := azservicebus.NewClientFromConnectionString(s.ConnectionString, nil)
	if err != nil {
		return fmt.Errorf("service bus client: %w", err)
	}
	defer func() { _ = client.Close(context.Background()) }()

	receiver, err := client.NewReceiverForSubscription(s.Topic, s.Name, nil)
	if err != nil {
		return fmt.Errorf("service bus receiver: %w", err)
	}
	defer func() { _ = receiver.Close(context.Background()) }()

	tracer := otel.Tracer("hivespace.eventbus")
	for {
		messages, err := receiver.ReceiveMessages(ctx, 10, nil)
		if err != nil {
			if errors.Is(err, context.Canceled) {
				return nil
			}
			slog.WarnContext(ctx, "receive failed; retrying", "error", err)
			select {
			case <-ctx.Done():
				return nil
			case <-time.After(2 * time.Second):
			}
			continue
		}
		for _, m := range messages {
			s.process(ctx, tracer, receiver, m, handle)
		}
	}
}

func (s Subscription) process(ctx context.Context, tracer trace.Tracer, receiver *azservicebus.Receiver, m *azservicebus.ReceivedMessage, handle Handler) {
	e, err := Decode(m.Body)
	if err != nil {
		slog.ErrorContext(ctx, "malformed envelope; dead-lettering", "error", err)
		_ = receiver.DeadLetterMessage(ctx, m, nil)
		return
	}

	// Continue the producer's trace so one flow is one trace (NFR-OBS-001).
	carrier := propagation.MapCarrier{"traceparent": e.Traceparent}
	msgCtx := otel.GetTextMapPropagator().Extract(ctx, carrier)
	msgCtx, span := tracer.Start(msgCtx, "consume "+e.EventType, trace.WithSpanKind(trace.SpanKindConsumer))
	defer span.End()

	if err := handle(msgCtx, e); err != nil {
		span.RecordError(err)
		slog.ErrorContext(msgCtx, "handler failed; abandoning for redelivery", "eventId", e.EventID, "error", err)
		_ = receiver.AbandonMessage(ctx, m, nil)
		return
	}
	if err := receiver.CompleteMessage(ctx, m, nil); err != nil {
		slog.WarnContext(msgCtx, "complete failed; message may be redelivered (inbox dedupes)", "error", err)
	}
}
