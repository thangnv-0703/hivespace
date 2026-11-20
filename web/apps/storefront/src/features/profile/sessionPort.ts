import { inject, type InjectionKey } from 'vue'

/**
 * What the profile feature needs from the session, supplied by the app shell (features never import each other, dev-standards §8.4):
 * `App.vue` provides it from the auth feature's session store.
 */
export interface ProfileSession {
  isSignedIn(): boolean
  /** Forces a token refresh so the header name and language are current after a save (technical-design D-4). */
  refresh(): Promise<void>
  /** A call answered 401: the session is gone. */
  markSignedOut(): void
}

export const profileSessionKey: InjectionKey<ProfileSession> = Symbol('profile-session')

export function useProfileSession(): ProfileSession {
  const session = inject(profileSessionKey)
  if (!session) throw new Error('profileSessionKey is not provided; App.vue must provide it.')
  return session
}
