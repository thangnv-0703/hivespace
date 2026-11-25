import { inject, type InjectionKey } from 'vue'

/**
 * What the settings pages need from the session and the toast stack, supplied by the app (features never import each other,
 * dev-standards §8.4): `App.vue` provides it from the auth feature's session store and the shared toasts.
 */
export interface SettingsSession {
  /** Forces a token refresh so the roles are current (a 403 means the Seller role is gone). */
  refresh(): Promise<void>
  /** A call answered 401: the session is gone. */
  markSignedOut(): void
  notify(toast: { variant: 'success' | 'error'; title: string }): void
}

export const settingsSessionKey: InjectionKey<SettingsSession> = Symbol('settings-session')

export function useSettingsSession(): SettingsSession {
  const session = inject(settingsSessionKey)
  if (!session) throw new Error('settingsSessionKey is not provided; App.vue must provide it.')
  return session
}
