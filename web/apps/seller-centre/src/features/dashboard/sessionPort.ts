import { inject, type InjectionKey } from 'vue'

/** What the dashboard needs from the session, supplied by `App.vue` (features never import each other, dev-standards §8.4). */
export interface DashboardSession {
  /** The seller role was lost (cannot happen in the MVP): refresh the claims so the route guard reroutes. */
  refresh(): Promise<void>
  /** The session ended on a call (401). */
  markSignedOut(): void
}

export const dashboardSessionKey: InjectionKey<DashboardSession> = Symbol('dashboard-session')

export function useDashboardSession(): DashboardSession {
  const session = inject(dashboardSessionKey)
  if (!session) throw new Error('dashboardSessionKey is not provided; App.vue must provide it.')
  return session
}
