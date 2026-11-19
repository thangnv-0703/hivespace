import { defineStore } from 'pinia'

import { fetchSession, type SessionUser } from './api'

/** The signed-in user, from `GET /bff/user` (never a token: the browser holds only the HttpOnly cookie). */
export const useSessionStore = defineStore('session', {
  state: () => ({ status: 'unknown' as 'unknown' | 'anonymous' | 'authenticated', user: null as SessionUser | null }),
  actions: {
    async load() {
      const user = await fetchSession()
      this.user = user
      this.status = user ? 'authenticated' : 'anonymous'
    },
  },
})
