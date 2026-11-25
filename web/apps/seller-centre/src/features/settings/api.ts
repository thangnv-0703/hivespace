// The only file in this feature that imports the generated client (dev-standards §8.4). The CSRF header and base URL are set
// once on the shared identity client by the auth feature's api.ts, which is loaded before any page.
import { getMyShop, setMyShopPayout, setMyShopPickupAddress, updateMyShopProfile } from '@hivespace/api-client/identity'

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
}

export type Outcome<T> = { ok: true; data: T } | { ok: false; failure: Failure }

export interface PickupAddress {
  contactName: string
  phone: string
  province: string
  district: string
  ward: string
  street: string
}

/** The account number is never part of a response: only its last four digits come back (NFR-SEC-008). */
export interface Payout {
  bankName: string
  accountNumberLast4: string
  accountHolder: string
}

export interface MyShop {
  shopId: string
  name: string
  sellerType: SellerType
  description: string | null
  pickupAddress: PickupAddress | null
  payout: Payout | null
  setup: { pickupAddress: boolean; payout: boolean }
}

export interface PayoutInput {
  bankName: string
  accountNumber: string
  accountHolder: string
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
    return {
      ok: false,
      failure: {
        status: result.response?.status ?? 0,
        code: result.error?.code ?? 'common.internal_error',
        message: result.error?.message,
        details: result.error?.details ?? [],
      },
    }
  }
  return { ok: true, data: result.data as T }
}

/** SCR-SEL-007 and SCR-SEL-008: the signed-in seller's shop with its settings. */
export const loadShop = () => run<MyShop>(() => getMyShop({ headers: headers() }) as Promise<Result<MyShop>>)

/** SCR-SEL-007: name and description (FR-SHOP-005). An empty description is sent as null. */
export const saveProfile = (input: { name: string; description: string }) =>
  run<MyShop>(
    () =>
      updateMyShopProfile({
        body: { name: input.name, description: input.description.trim() ? input.description : null },
        headers: headers(),
      }) as Promise<Result<MyShop>>,
  )

/** SCR-SEL-008: the pickup address (FR-SHOP-006). */
export const saveAddress = (input: PickupAddress) =>
  run<MyShop>(() => setMyShopPickupAddress({ body: input, headers: headers() }) as Promise<Result<MyShop>>)

/** SCR-SEL-008: the payout bank details (FR-SHOP-007). The full number is always sent and never kept. */
export const savePayout = (input: PayoutInput) =>
  run<MyShop>(() => setMyShopPayout({ body: input, headers: headers() }) as Promise<Result<MyShop>>)
