// Package cart is a lean feature package (repo-structure §4.3): model, service, handlers together.
package cart

import (
	"encoding/json"
	"net/http"
)

// Register mounts this feature's routes. STUB: the template sample, replaced by the first feature slice.
func Register(mux *http.ServeMux) {
	mux.HandleFunc("GET /v1/cart/ping", ping)
}

func ping(w http.ResponseWriter, _ *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(map[string]string{"service": "cart"})
}
