import { useSessionStore } from '@/features/auth'
import type { NavigationGuard } from 'vue-router'

const REGISTER_SHOP = '/register-shop'

/**
 * Every seller-centre page needs a signed-in seller (FEAT-009 technical-design §5.5, ui-design U-6):
 * a visitor goes to the gateway sign-in and comes back to the same path; a buyer without a shop goes to registration;
 * a seller who opens registration goes to the dashboard. The session probe runs once and is shared with App.vue.
 */
export function createSellerGuard(assign: (url: string) => void = (url) => globalThis.location.assign(url)): NavigationGuard {
  return async (to) => {
    const session = useSessionStore()
    if (session.status === 'unknown') await session.load()
    if (session.status !== 'authenticated') {
      assign(`/bff/login?returnUrl=${encodeURIComponent(to.fullPath)}`)
      return false
    }
    if (session.isSeller) return to.path === REGISTER_SHOP ? '/' : true
    return to.path === REGISTER_SHOP ? true : REGISTER_SHOP
  }
}
