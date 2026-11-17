// Package domain holds Inventory's pure rules. It imports nothing from internal/ and no driver (depguard).
// Walking skeleton: only the probe value. FEAT-012 and FEAT-017 add stock, reservations and their states.
package domain

// SkeletonProbe is what GetAvailability returns until real stock exists.
const SkeletonProbe = "inventory-ok"

// VariantAvailability is the available quantity for one variant (FR-INV-001).
type VariantAvailability struct {
	VariantID string
	Available int64
}
