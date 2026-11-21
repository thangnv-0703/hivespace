import { useSessionStore } from '@/features/auth'
import type { NavigationGuard } from 'vue-router'

/**
 * Pages with `meta.requiresAuth` need a signed-in user. Without a session the browser goes to the gateway sign-in and comes back
 * to the same path (FR-AUTH-009, technical-design §5.5). The session probe runs once and is shared with App.vue.
 */
export function createAuthGuard(assign: (url: string) => void = (url) => globalThis.location.assign(url)): NavigationGuard {
  return async (to) => {
    if (!to.meta.requiresAuth) return true
    const session = useSessionStore()
    if (session.status === 'unknown') await session.load()
    if (session.status === 'authenticated') return true
    assign(`/bff/login?returnUrl=${encodeURIComponent(to.fullPath)}`)
    return false
  }
}
