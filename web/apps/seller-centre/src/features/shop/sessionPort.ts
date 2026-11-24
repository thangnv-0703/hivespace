import { inject, type InjectionKey } from 'vue'

/**
 * What the shop feature needs from the session and the toast stack, supplied by the app (features never import each other,
 * dev-standards §8.4): `App.vue` provides it from the auth feature's session store and the shared toasts.
 */
export interface ShopSession {
  /** The signed-in user's email, for the unverified state's copy and resend. */
  email(): string
  emailVerified(): boolean
  /** Forces a token refresh so the roles and the verified flag are current (FEAT-009 D-2, D-3). */
  refresh(): Promise<void>
  /** A call answered 401: the session is gone. */
  markSignedOut(): void
  notify(toast: { variant: 'success' | 'error'; title: string }): void
}

export const shopSessionKey: InjectionKey<ShopSession> = Symbol('shop-session')

export function useShopSession(): ShopSession {
  const session = inject(shopSessionKey)
  if (!session) throw new Error('shopSessionKey is not provided; App.vue must provide it.')
  return session
}
