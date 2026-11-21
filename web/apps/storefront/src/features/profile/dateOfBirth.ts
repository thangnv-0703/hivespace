export interface DateParts {
  day: string
  month: string
  year: string
}

export const EMPTY_DATE: DateParts = { day: '', month: '', year: '' }

/** `YYYY-MM-DD` to its three parts, without leading zeros so they match the select values. */
export function splitDate(iso: string | null): DateParts {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso ?? '')
  return match ? { year: String(Number(match[1])), month: String(Number(match[2])), day: String(Number(match[3])) } : EMPTY_DATE
}

export type DateCheck = 'empty' | 'incomplete' | 'invalid' | 'ok'

export function checkDate(parts: DateParts): DateCheck {
  const filled = [parts.day, parts.month, parts.year].filter(Boolean).length
  if (filled === 0) return 'empty'
  if (filled < 3) return 'incomplete'
  const [year, month, day] = [Number(parts.year), Number(parts.month), Number(parts.day)] as [number, number, number]
  const date = new Date(Date.UTC(year, month - 1, day))
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day ? 'ok' : 'invalid'
}

/** The three parts back to `YYYY-MM-DD`; all three empty is `null` (clears the date). Call only after `checkDate` says ok or empty. */
export function joinDate(parts: DateParts): string | null {
  if (!parts.day && !parts.month && !parts.year) return null
  return `${parts.year.padStart(4, '0')}-${parts.month.padStart(2, '0')}-${parts.day.padStart(2, '0')}`
}

/** `+84912345678` as `+84 912 345 678` for reading; anything else is shown as stored. */
export function formatPhone(stored: string | null): string {
  const match = /^\+84(\d{3})(\d{3})(\d{3})$/.exec(stored ?? '')
  return match ? `+84 ${match[1]} ${match[2]} ${match[3]}` : (stored ?? '')
}
