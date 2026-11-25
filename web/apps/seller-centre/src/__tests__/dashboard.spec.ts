import { createHiveI18n } from '@hivespace/core'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import { DashboardPage, dashboardSessionKey } from '../features/dashboard'
import { loadMyShop } from '../features/dashboard/api'
import { messages } from '../locales'

vi.mock('../features/dashboard/api')

const shop = { shopId: 's1', name: 'Tiệm Bánh An', sellerType: 'business' as const, setup: { pickupAddress: false, payout: false } }

async function mountPage() {
  const session = { refresh: vi.fn<() => Promise<void>>().mockResolvedValue(undefined), markSignedOut: vi.fn<() => void>() }
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: DashboardPage }] })
  await router.push('/')
  const wrapper = mount(DashboardPage, { global: { plugins: [router, createHiveI18n(messages)], provide: { [dashboardSessionKey as symbol]: session } } })
  return { wrapper, session }
}

describe('DashboardPage (SCR-SEL-002)', () => {
  beforeEach(() => {
    vi.mocked(loadMyShop).mockReset()
    localStorage.setItem('hs-locale', 'en')
  })

  it('shows skeletons while the shop loads', async () => {
    vi.mocked(loadMyShop).mockReturnValue(new Promise(() => {}))
    const { wrapper } = await mountPage()

    expect(wrapper.find('[data-testid=dashboard-loading]').exists()).toBe(true)
  })

  it('shows the shop name, type and an unfinished checklist for a new shop', async () => {
    vi.mocked(loadMyShop).mockResolvedValue({ ok: true, shop })
    const { wrapper } = await mountPage()
    await flushPromises()

    expect(wrapper.get('[data-testid=shop-subtitle]').text()).toBe('Tiệm Bánh An · Business')
    expect(wrapper.get('[data-testid=setup-progress]').text()).toBe('0 of 2 done')
    const actions = wrapper.findAll('a[href="/settings/pickup-payout"]').map((link) => link.text())
    expect(actions).toEqual(['Add address', 'Add bank details'])
  })

  it('shows a done row with a badge and no button, and the ready line when both are done', async () => {
    vi.mocked(loadMyShop).mockResolvedValue({ ok: true, shop: { ...shop, setup: { pickupAddress: true, payout: true } } })
    const { wrapper } = await mountPage()
    await flushPromises()

    expect(wrapper.get('[data-testid=setup-progress]').text()).toBe('2 of 2 done')
    expect(wrapper.text()).toContain('Your shop is ready to publish products.')
    expect(wrapper.find('a[href="/settings/pickup-payout"]').exists()).toBe(false)
  })

  it('shows an error with a Retry that asks again', async () => {
    vi.mocked(loadMyShop).mockResolvedValueOnce({ ok: false, status: 500, code: 'common.internal_error' })
    vi.mocked(loadMyShop).mockResolvedValueOnce({ ok: true, shop })
    const { wrapper } = await mountPage()
    await flushPromises()

    expect(wrapper.text()).toContain("We couldn't load your shop. Try again.")
    await wrapper.get('[data-testid=retry]').trigger('click')
    await flushPromises()

    expect(loadMyShop).toHaveBeenCalledTimes(2)
    expect(wrapper.get('[data-testid=shop-subtitle]').text()).toContain('Tiệm Bánh An')
  })

  it('refreshes the session on a 403 so the route guard can reroute', async () => {
    vi.mocked(loadMyShop).mockResolvedValue({ ok: false, status: 403, code: 'common.forbidden' })
    const { session } = await mountPage()
    await flushPromises()

    expect(session.refresh).toHaveBeenCalledTimes(1)
  })
})
