// The only file in this feature that imports the generated client (dev-standards §8.4). The CSRF header and base URL are set
// once on the shared identity client by the auth feature's api.ts, which is loaded before any page.
import { getMyShop, registerShop, resendVerificationEmail } from '@hivespace/api-client/identity'

export type SellerType = 'individual' | 'business'

export interface FieldProblem {
  field: string
  code: string
  message?: string
}

/** `status` 0 means the request never got an answer (network failure). */
export interface Failure {
  status: number
  code: string
  message?: string
  details: FieldProblem[]
  retryAfter?: number
}

export type Outcome<T> = { ok: true; data: T } | { ok: false; failure: Failure }

export interface MyShop {
  shopId: string
  name: string
  sellerType: SellerType
  setup: { pickupAddress: boolean; payout: boolean }
}

interface Result<T> {
  data?: T
  error?: { code?: string; message?: string; details?: FieldProblem[] }
  response?: Response
}

const headers = () => ({
  'X-HS-CSRF': '1',
  'Accept-Language': (globalThis.document?.documentElement.lang === 'en' ? 'en' : 'vi') as 'vi' | 'en',
})

async function run<T>(call: () => Promise<Result<T>>): Promise<Outcome<T>> {
  let result: Result<T>
  try {
    result = await call()
  } catch {
    return { ok: false, failure: { status: 0, code: 'network', details: [] } }
  }
  if (result.error || !result.response?.ok) {
    const seconds = Number(result.response?.headers.get('Retry-After'))
    return {
      ok: false,
      failure: {
        status: result.response?.status ?? 0,
        code: result.error?.code ?? 'common.internal_error',
        message: result.error?.message,
        details: result.error?.details ?? [],
        retryAfter: Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : undefined,
      },
    }
  }
  return { ok: true, data: result.data as T }
}

/** SCR-SEL-001: opens the shop and grants the Seller role (FR-SHOP-001). */
export const register = (input: { name: string; sellerType: SellerType }) =>
  run<MyShop>(() => registerShop({ body: input, headers: headers() }) as Promise<Result<MyShop>>)

/** SCR-SEL-002: the signed-in seller's shop and its setup checklist. */
export const loadMyShop = () => run<MyShop>(() => getMyShop({ headers: headers() }) as Promise<Result<MyShop>>)

/** The unverified state's "Resend email" (FEAT-001). */
export const resend = (email: string) => run<unknown>(() => resendVerificationEmail({ body: { email }, headers: headers() }))
