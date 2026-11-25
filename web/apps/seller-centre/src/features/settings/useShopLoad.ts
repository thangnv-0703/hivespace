import { ref } from 'vue'
import { useRouter } from 'vue-router'

import { loadShop, type MyShop } from './api'
import { useSettingsSession } from './sessionPort'

/** Loads the shop for a settings page; 401 goes to sign-in, 403 refreshes the session so the route guard reroutes (SCR-SEL-007 error). */
export function useShopLoad(returnUrl: string, fallbackMessage: () => string) {
  const session = useSettingsSession()
  const router = useRouter()
  const shop = ref<MyShop | null>(null)
  const loading = ref(true)
  const error = ref('')

  async function load() {
    loading.value = true
    error.value = ''
    const result = await loadShop()
    if (result.ok) {
      shop.value = result.data
    } else if (result.failure.status === 401) {
      session.markSignedOut()
      globalThis.location.assign(`/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`)
    } else if (result.failure.status === 403) {
      await session.refresh()
      await router.replace('/')
    } else {
      error.value = result.failure.message ?? fallbackMessage()
    }
    loading.value = false
  }

  return { shop, loading, error, load }
}
