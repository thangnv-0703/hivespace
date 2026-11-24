// The challenge of an email-code sign-in in progress (FEAT-006 ui-design "Stored challenge"). Kept in sessionStorage so the
// code page survives a reload; never the code itself. Cleared on success and replaced by each new code.
const KEY = 'hs-code-challenge'

export interface CodeChallenge {
  challengeId: string
  email: string
  expiresAt: string
  resendAvailableAt: string
  returnUrl?: string
}

export function loadChallenge(): CodeChallenge | null {
  try {
    const raw = globalThis.sessionStorage?.getItem(KEY)
    if (!raw) return null
    const value = JSON.parse(raw) as Partial<CodeChallenge>
    return value.challengeId && value.email && value.expiresAt && value.resendAvailableAt ? (value as CodeChallenge) : null
  } catch {
    return null
  }
}

export function saveChallenge(challenge: CodeChallenge): void {
  try {
    globalThis.sessionStorage?.setItem(KEY, JSON.stringify(challenge))
  } catch {
    // Storage blocked: the code page then sends the user back to request a code.
  }
}

export function clearChallenge(): void {
  try {
    globalThis.sessionStorage?.removeItem(KEY)
  } catch {
    // Nothing to clear.
  }
}

/** Whole seconds from now until an ISO time, never below zero. */
export function secondsUntil(iso: string, now = Date.now()): number {
  return Math.max(0, Math.ceil((Date.parse(iso) - now) / 1000))
}

/** `m:ss` for the expiry and resend timers. */
export function formatClock(seconds: number): string {
  const s = Math.max(0, seconds)
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`
}
