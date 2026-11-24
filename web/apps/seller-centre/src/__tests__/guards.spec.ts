import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { RouteLocationNormalized } from 'vue-router'

import { createSellerGuard } from '../app/guards'
import { fetchSession, type SessionUser } from '../features/auth/api'

vi.mock('../features/auth/api')

const user = (roles: string[]): SessionUser => ({ userId: 'u1', email: 'an@example.com', displayName: 'An', emailVerified: true, roles, language: 'vi' })
const to = (path: string) => ({ path, fullPath: path }) as RouteLocationNormalized
const run = (guard: ReturnType<typeof createSellerGuard>, path: string) => guard(to(path), to('/'), () => {})

describe('seller guard (FEAT-009 §5.5)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(fetchSession).mockReset()
  })

  it('sends a visitor to the gateway sign-in and comes back to the same path', async () => {
    vi.mocked(fetchSession).mockResolvedValue(null)
    const assign = vi.fn<(url: string) => void>()

    expect(await run(createSellerGuard(assign), '/orders?page=2')).toBe(false)
    expect(assign).toHaveBeenCalledWith('/bff/login?returnUrl=%2Forders%3Fpage%3D2')
  })

  it('sends a buyer without a shop to registration, from any page', async () => {
    vi.mocked(fetchSession).mockResolvedValue(user(['Buyer']))

    expect(await run(createSellerGuard(vi.fn<(url: string) => void>()), '/')).toBe('/register-shop')
  })

  it('lets a buyer without a shop open registration', async () => {
    vi.mocked(fetchSession).mockResolvedValue(user(['Buyer']))

    expect(await run(createSellerGuard(vi.fn<(url: string) => void>()), '/register-shop')).toBe(true)
  })

  it('sends a seller who opens registration to the dashboard', async () => {
    vi.mocked(fetchSession).mockResolvedValue(user(['Buyer', 'Seller']))

    expect(await run(createSellerGuard(vi.fn<(url: string) => void>()), '/register-shop')).toBe('/')
  })

  it('lets a seller open the other pages', async () => {
    vi.mocked(fetchSession).mockResolvedValue(user(['Buyer', 'Seller']))

    expect(await run(createSellerGuard(vi.fn<(url: string) => void>()), '/')).toBe(true)
  })
})
