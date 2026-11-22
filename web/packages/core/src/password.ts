// Client-side password rules, shared by every feature that asks for a password. They are hints and early feedback only;
// the API is the authority (FR-AUTH-003, FR-AUTH-004).
export type PasswordRuleKey = 'length' | 'letter' | 'digit'

/** Same shape as `PasswordRule` in `@hivespace/ui`. */
export interface PasswordRuleState {
  key: PasswordRuleKey
  label: string
  met: boolean
}

export function passwordChecks(password: string): Record<PasswordRuleKey, boolean> {
  return { length: password.length >= 8, letter: /\p{L}/u.test(password), digit: /\d/.test(password) }
}

export function passwordRules(password: string, label: (key: PasswordRuleKey) => string): PasswordRuleState[] {
  const checks = passwordChecks(password)
  return (['length', 'letter', 'digit'] as const).map((key) => ({ key, label: label(key), met: checks[key] }))
}
