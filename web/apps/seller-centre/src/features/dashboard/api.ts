// The only file in this feature that imports the generated client (dev-standards §8.4). The base URL is set once on the shared
// identity client by the auth feature's api.ts, which is loaded before any page.
import { getMyShop } from '@hivespace/api-client/identity'

export interface MyShop {
  shopId: string
  name: string
  sellerType: 'individual' | 'business'
  setup: { pickupAddress: boolean; payout: boolean }
}

/** `status` 0 means the request never got an answer (network failure). */
export type ShopResult = { ok: true; shop: MyShop } | { ok: false; status: number; code: string; message?: string }

/** SCR-SEL-002: the signed-in seller's shop and its setup checklist. */
export async function loadMyShop(): Promise<ShopResult> {
  try {
    const language = globalThis.document?.documentElement.lang === 'en' ? 'en' : 'vi'
    const result = await getMyShop({ headers: { 'X-HS-CSRF': '1', 'Accept-Language': language } })
    if (result.response?.ok && result.data) return { ok: true, shop: result.data as MyShop }
    const error = result.error as { code?: string; message?: string } | undefined
    return { ok: false, status: result.response?.status ?? 0, code: error?.code ?? 'common.internal_error', message: error?.message }
  } catch {
    return { ok: false, status: 0, code: 'network' }
  }
}
