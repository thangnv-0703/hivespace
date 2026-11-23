// The `?error=` codes Identity's Google redirects end with (FEAT-004 technical-design §3.4) and their alert tone
// (ui-design D-B: cancelled is the user's choice, unavailable is an outage, the others are errors).
export const GOOGLE_ERROR_TONES = {
  'identity.external_login_denied': 'info',
  'identity.external_email_unverified': 'error',
  'identity.external_login_failed': 'error',
  'identity.external_login_unavailable': 'warning',
  // The gateway's answer to a rate-limited Google callback (FEAT-005 §3.7).
  'common.rate_limited': 'warning',
} as const

export type GoogleErrorCode = keyof typeof GOOGLE_ERROR_TONES

/** The known code in a `?error=` query value, or null: an unknown value shows nothing. */
export function googleErrorOf(value: unknown): GoogleErrorCode | null {
  return typeof value === 'string' && Object.prototype.hasOwnProperty.call(GOOGLE_ERROR_TONES, value)
    ? (value as GoogleErrorCode)
    : null
}
