import type { PasswordRule } from '@hivespace/ui'

import type { FieldProblem } from './api'

// Client-side checks are hints and early feedback only; the API is the authority (FR-AUTH-003, FR-AUTH-004).
export const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
export const DISPLAY_NAME_MAX = 60

export function passwordChecks(password: string): Record<'length' | 'letter' | 'digit', boolean> {
  return { length: password.length >= 8, letter: /\p{L}/u.test(password), digit: /\d/.test(password) }
}

export function passwordRules(password: string, label: (key: 'length' | 'letter' | 'digit') => string): PasswordRule[] {
  const checks = passwordChecks(password)
  return (['length', 'letter', 'digit'] as const).map((key) => ({ key, label: label(key), met: checks[key] }))
}

export type RegisterField = 'displayName' | 'email' | 'password'

/** Routes a server detail to its form field by the stable code prefix (technical-design 3.7). */
export function fieldOf(problem: FieldProblem): RegisterField | undefined {
  if (problem.code.startsWith('email_') || problem.field === 'email') return 'email'
  if (problem.code.startsWith('display_name_') || problem.field === 'displayName') return 'displayName'
  if (problem.code.startsWith('password_') || problem.field === 'password') return 'password'
  return undefined
}

/** Safe `returnUrl` passthrough: the client never builds one, it only forwards a single string. */
export function returnUrlOf(query: unknown): string | undefined {
  const value = Array.isArray(query) ? query[0] : query
  return typeof value === 'string' && value.length > 0 ? value : undefined
}
