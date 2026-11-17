package app

import (
	"context"
	"testing"
)

type memInbox map[string]bool

func (m memInbox) Record(_ context.Context, eventID, consumer string) (bool, error) {
	key := consumer + "/" + eventID
	if m[key] {
		return false, nil
	}
	m[key] = true
	return true, nil
}

func TestHandleEventIsIdempotent(t *testing.T) {
	svc := New(memInbox{})
	ctx := context.Background()

	tests := []struct {
		name string
		want bool
	}{
		{"first delivery is processed", true},
		{"second delivery is ignored", false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got, err := svc.HandleEvent(ctx, "0193-event", "Ping")
			if err != nil {
				t.Fatal(err)
			}
			if got != tt.want {
				t.Fatalf("got %v, want %v", got, tt.want)
			}
		})
	}
}

func TestGetAvailabilityReturnsProbe(t *testing.T) {
	items, probe := New(memInbox{}).GetAvailability(context.Background(), []string{"v1"})
	if probe == "" || len(items) != 1 {
		t.Fatalf("unexpected result: %v %q", items, probe)
	}
}
