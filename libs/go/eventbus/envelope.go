// Package eventbus holds the event envelope (contracts/events/envelope.schema.json, ADR-0002)
// and a Service Bus subscription receiver. It contains no service domain concepts (repo-structure §6).
package eventbus

import (
	"encoding/json"
	"time"
)

// Envelope mirrors contracts/events/envelope.schema.json.
type Envelope struct {
	EventID       string          `json:"eventId"`
	EventType     string          `json:"eventType"`
	EventVersion  int             `json:"eventVersion"`
	OccurredAt    time.Time       `json:"occurredAt"`
	Producer      string          `json:"producer"`
	CorrelationID string          `json:"correlationId"`
	Traceparent   string          `json:"traceparent,omitempty"`
	Data          json.RawMessage `json:"data"`
}

// Decode parses an envelope from a message body.
func Decode(body []byte) (Envelope, error) {
	var e Envelope
	err := json.Unmarshal(body, &e)
	return e, err
}
