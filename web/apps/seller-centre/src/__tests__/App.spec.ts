import { createHiveI18n } from '@hivespace/core'
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import App from '../app/App.vue'
import { DashboardPage } from '../features/dashboard'
import { messages } from '../locales'

describe('seller centre shell', () => {
  it.each([
    ['vi', 'Sản phẩm'],
    ['en', 'Products'],
  ] as const)('renders the sidebar sections in %s', async (locale, products) => {
    localStorage.setItem('hs-locale', locale)
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: DashboardPage }] })
    await router.push('/')

    const wrapper = mount(App, { global: { plugins: [router, createHiveI18n(messages)] } })

    expect(wrapper.get('[data-testid=sidebar]').text()).toContain(products)
  })
})
