import { createHiveI18n } from '@hivespace/core'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import { RegisterShopPage, shopSessionKey, type ShopSession } from '../features/shop'
import { register, resend } from '../features/shop/api'
import { messages } from '../locales'

vi.mock('../features/shop/api')

const shop = { shopId: 's1', name: 'Tiệm Bánh An', sellerType: 'individual' as const, setup: { pickupAddress: false, payout: false } }
const failure = (status: number, code: string, extra: object = {}) => ({ ok: false as const, failure: { status, code, details: [], ...extra } })

async function mountPage(overrides: Partial<ShopSession> = {}) {
  const calls: string[] = []
  const session: ShopSession = {
    email: () => 'an@example.com',
    emailVerified: () => true,
    refresh: async () => void calls.push('refresh'),
    markSignedOut: vi.fn<() => void>(),
    notify: vi.fn<ShopSession['notify']>(),
    ...overrides,
  }
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: { template: '<p/>' } }, { path: '/register-shop', component: RegisterShopPage }] })
  const replace = vi.spyOn(router, 'replace').mockImplementation(async () => void calls.push('replace /'))
  await router.push('/register-shop')
  const wrapper = mount(RegisterShopPage, { global: { plugins: [router, createHiveI18n(messages)], provide: { [shopSessionKey as symbol]: session } } })
  await flushPromises()
  return { wrapper, calls, session, replace }
}

async function fill(wrapper: Awaited<ReturnType<typeof mountPage>>['wrapper'], name: string, type = 'individual') {
  await wrapper.get('input[type=text]').setValue(name)
  await wrapper.get(`input[type=radio][value=${type}]`).setValue(true)
}

describe('RegisterShopPage (SCR-SEL-001)', () => {
  beforeEach(() => {
    vi.mocked(register).mockReset()
    vi.mocked(resend).mockReset()
    localStorage.setItem('hs-locale', 'en')
  })

  it('has no seller type preselected', async () => {
    const { wrapper } = await mountPage()

    expect(wrapper.findAll('input[type=radio]').some((radio) => (radio.element as HTMLInputElement).checked)).toBe(false)
  })

  it('refreshes the session before it navigates to the dashboard, then announces the shop', async () => {
    vi.mocked(register).mockResolvedValue({ ok: true, data: shop })
    const { wrapper, calls, session, replace } = await mountPage()

    await fill(wrapper, 'Tiệm Bánh An')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(register).toHaveBeenCalledWith({ name: 'Tiệm Bánh An', sellerType: 'individual' })
    expect(calls).toEqual(['refresh', 'replace /'])
    expect(replace).toHaveBeenCalledWith('/')
    expect(session.notify).toHaveBeenCalledWith({ variant: 'success', title: 'Your shop is open' })
  })

  it('shows shop_name_taken on the name field and keeps the values', async () => {
    vi.mocked(register).mockResolvedValue(failure(409, 'identity.shop_name_taken', { message: 'That name is taken.' }))
    const { wrapper, replace } = await mountPage()

    await fill(wrapper, 'Tiệm Bánh An', 'business')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('That name is taken.')
    expect((wrapper.get('input[type=text]').element as HTMLInputElement).value).toBe('Tiệm Bánh An')
    expect(replace).not.toHaveBeenCalled()
  })

  it('treats shop_already_owned as success: refresh, then the dashboard, with no toast', async () => {
    vi.mocked(register).mockResolvedValue(failure(409, 'identity.shop_already_owned'))
    const { wrapper, calls, session } = await mountPage()

    await fill(wrapper, 'Tiệm Bánh An')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(calls).toEqual(['refresh', 'replace /'])
    expect(session.notify).not.toHaveBeenCalled()
  })

  it('checks the fields before it calls the API', async () => {
    const { wrapper } = await mountPage()

    await wrapper.get('input[type=text]').setValue('ab')
    await wrapper.get('form').trigger('submit')

    expect(register).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Use at least 3 characters.')
    expect(wrapper.text()).toContain('Choose a seller type.')
  })

  it('shows each server field problem on its own field', async () => {
    vi.mocked(register).mockResolvedValue(
      failure(400, 'common.validation_failed', {
        details: [
          { field: 'name', code: 'identity.shop_name_invalid', message: 'Bad characters.' },
          { field: 'sellerType', code: 'identity.seller_type_invalid', message: 'Bad type.' },
        ],
      }),
    )
    const { wrapper } = await mountPage()

    await fill(wrapper, 'Tiệm Bánh An')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Bad characters.')
    expect(wrapper.text()).toContain('Bad type.')
  })

  it('shows the unverified state, disables the form and starts the resend cooldown', async () => {
    vi.mocked(resend).mockResolvedValue({ ok: true, data: undefined })
    const { wrapper } = await mountPage({ emailVerified: () => false, refresh: async () => {} })

    expect(wrapper.text()).toContain('Verify your email first')
    expect(wrapper.text()).toContain('an@example.com')
    expect(wrapper.get('button[type=submit]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid=resend]').trigger('click')
    await flushPromises()

    expect(resend).toHaveBeenCalledWith('an@example.com')
    expect(wrapper.text()).toContain('Email sent.')
    expect(wrapper.get('[data-testid=resend]').attributes('disabled')).toBeDefined()
  })

  it('shows the unverified state when the API answers email_not_verified', async () => {
    vi.mocked(register).mockResolvedValue(failure(403, 'identity.email_not_verified'))
    const { wrapper } = await mountPage()

    await fill(wrapper, 'Tiệm Bánh An')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Verify your email first')
  })
})
