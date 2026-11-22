import type { FieldProblem } from './api'

// Client-side checks are hints and early feedback only; the API is the authority (FR-AUTH-003, FR-AUTH-004).
export const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
export const DISPLAY_NAME_MAX = 80

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
