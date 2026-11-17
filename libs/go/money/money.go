// Package money is the Go Money type (ADR-0010): an amount in minor units plus a currency code.
package money

import "fmt"

// Money is never a bare number (NFR-MNT-008).
type Money struct {
	Amount   int64  `json:"amount"`
	Currency string `json:"currency"`
}

// Add returns a+b; combining two currencies is an error (dev-standards §5).
func (a Money) Add(b Money) (Money, error) {
	if a.Currency != b.Currency {
		return Money{}, fmt.Errorf("money: cannot add %s to %s", b.Currency, a.Currency)
	}
	return Money{Amount: a.Amount + b.Amount, Currency: a.Currency}, nil
}
