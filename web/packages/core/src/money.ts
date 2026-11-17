/** Amount in minor units plus ISO 4217 code (ADR-0010). Never a bare number. */
export interface Money {
  amount: number
  currency: string
}

const exponents: Record<string, number> = { VND: 0 }

/**
 * Formats money for the active language (FR-CUR-002, FR-LOC-006):
 * vi → "300.000 ₫", en → "₫300,000".
 */
export function formatMoney(money: Money, locale: string): string {
  const exponent = exponents[money.currency]
  if (exponent === undefined) throw new Error(`Currency ${money.currency} is not enabled`)
  const value = money.amount / 10 ** exponent
  return new Intl.NumberFormat(locale === 'vi' ? 'vi-VN' : 'en-US', {
    style: 'currency',
    currency: money.currency,
    currencyDisplay: 'narrowSymbol',
    minimumFractionDigits: exponent,
    maximumFractionDigits: exponent,
  }).format(value)
}
