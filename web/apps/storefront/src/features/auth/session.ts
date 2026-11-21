import { defineStore } from 'pinia'

import { fetchSession, refreshSessionUser, type SessionUser } from './api'

let loading: Promise<void> | null = null

/** The signed-in user, from `GET /bff/user` (never a token: the browser holds only the HttpOnly cookie). */
export const useSessionStore = defineStore('session', {
  state: () => ({ status: 'unknown' as 'unknown' | 'anonymous' | 'authenticated', user: null as SessionUser | null }),
  actions: {
    /** One probe at a time: the page guard and the app shell may both ask for it on a first load. */
    load(): Promise<void> {
      return (loading ??= this.apply(fetchSession()).finally(() => {
        loading = null
      }))
    },
    /** Forces a token refresh after a profile change, so the header name and language update without waiting (D-4). */
    async refresh() {
      await this.apply(refreshSessionUser())
    },
    /** The session ended on a call (for example a 401): the page guard sends the user to sign in. */
    markSignedOut() {
      this.user = null
      this.status = 'anonymous'
    },
    async apply(pending: Promise<SessionUser | null>) {
      const user = await pending
      this.user = user
      this.status = user ? 'authenticated' : 'anonymous'
    },
  },
})
