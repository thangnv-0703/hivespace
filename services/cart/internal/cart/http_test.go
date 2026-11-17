package cart

import (
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestPing(t *testing.T) {
	tests := []struct {
		path string
		want int
	}{
		{"/v1/cart/ping", http.StatusOK},
		{"/v1/cart/unknown", http.StatusNotFound},
	}
	mux := http.NewServeMux()
	Register(mux)
	for _, tt := range tests {
		t.Run(tt.path, func(t *testing.T) {
			rec := httptest.NewRecorder()
			mux.ServeHTTP(rec, httptest.NewRequest(http.MethodGet, tt.path, nil))
			if rec.Code != tt.want {
				t.Fatalf("got %d, want %d", rec.Code, tt.want)
			}
		})
	}
}
