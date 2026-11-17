import { createHiveI18n } from '@hivespace/core'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import App from '../app/App.vue'
import { HomePage } from '../features/home'
import { messages } from '../locales'

function setup(locale: 'vi' | 'en' = 'vi') {
  localStorage.setItem('hs-locale', locale)
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: HomePage }] })
  return { router, i18n: createHiveI18n(messages) }
}

describe('storefront shell', () => {
  afterEach(() => vi.restoreAllMocks())

  it('renders categories from the API in the active language', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          items: [{ id: '01930000-0000-7000-8000-000000000001', slug: 'thoi-trang', name: { vi: 'Thời trang', en: 'Fashion' } }],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    )
    const { router, i18n } = setup()
    await router.push('/')
    const wrapper = mount(App, { global: { plugins: [router, i18n] } })
    await flushPromises()

    expect(wrapper.get('[data-testid=category]').text()).toBe('Thời trang')
  })

  it('shows the English name when the language is English (FR-LOC-007)', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          items: [{ id: '01930000-0000-7000-8000-000000000001', slug: 'thoi-trang', name: { vi: 'Thời trang', en: 'Fashion' } }],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    )
    const { router, i18n } = setup('en')
    await router.push('/')
    const wrapper = mount(App, { global: { plugins: [router, i18n] } })
    await flushPromises()

    expect(wrapper.get('[data-testid=category]').text()).toBe('Fashion')
  })

  it('shows an error with a retry when the API fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new Error('down'))
    const { router, i18n } = setup()
    await router.push('/')
    const wrapper = mount(App, { global: { plugins: [router, i18n] } })
    await flushPromises()

    expect(wrapper.text()).toContain('Không tải được danh mục.')
  })
})
