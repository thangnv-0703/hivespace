import { createHiveI18n } from '@hivespace/core'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import App from '../app/App.vue'
import { DashboardPage } from '../features/dashboard'
import { fetchSession } from '../features/auth/api'
import { loadMyShop } from '../features/dashboard/api'
import { messages } from '../locales'

// The shell asks for the session and the dashboard asks for the shop: both answer here without a network.
vi.mock('../features/auth/api')
vi.mock('../features/dashboard/api')

describe('seller centre shell', () => {
  it.each([
    ['vi', 'Sản phẩm'],
    ['en', 'Products'],
  ] as const)('renders the sidebar sections in %s', async (locale, products) => {
    vi.mocked(fetchSession).mockResolvedValue({ userId: 'u1', email: 'an@example.com', displayName: 'An Nguyen', emailVerified: true, roles: ['Buyer', 'Seller'], language: 'vi' })
    vi.mocked(loadMyShop).mockResolvedValue({ ok: true, shop: { shopId: 's1', name: 'Tiệm Bánh An', sellerType: 'individual', setup: { pickupAddress: false, payout: false } } })
    localStorage.setItem('hs-locale', locale)
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: DashboardPage }] })
    await router.push('/')

    const wrapper = mount(App, { global: { plugins: [createPinia(), router, createHiveI18n(messages)] } })

    expect(wrapper.get('[data-testid=sidebar]').text()).toContain(products)
  })
})
