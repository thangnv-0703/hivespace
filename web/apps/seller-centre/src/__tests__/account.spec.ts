import { createHiveI18n } from '@hivespace/core'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'

import { AccountMenu, useLanguagePreference, useSessionStore } from '../features/auth'
import { refreshSessionUser, saveLanguage, type SessionUser } from '../features/auth/api'
import { messages } from '../locales'

vi.mock('../features/auth/api')

const seller: SessionUser = { userId: 'u1', email: 'an@example.com', displayName: 'An Nguyen', emailVerified: true, roles: ['Buyer', 'Seller'], language: 'vi' }

function mountWithLanguage(onError: (message: string) => void) {
  const Probe = defineComponent({
    setup() {
      const language = useLanguagePreference(onError)
      return () => h('button', { 'data-testid': 'to-en', onClick: () => language.change('en') })
    },
  })
  return mount(Probe, { global: { plugins: [createHiveI18n(messages)] } })
}

describe('seller-centre account menu and language', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(saveLanguage).mockReset()
    vi.mocked(refreshSessionUser).mockReset()
    localStorage.setItem('hs-locale', 'vi')
  })

  it('signs out by posting a same-origin form to /bff/logout', async () => {
    const session = useSessionStore()
    await session.apply(Promise.resolve(seller))
    const submit = vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => {})
    const wrapper = mount(AccountMenu, { attachTo: document.body, global: { plugins: [createHiveI18n(messages)] } })

    const form = wrapper.get('form[action="/bff/logout"]')
    expect(form.attributes('method')).toBe('post')
    await wrapper.get('button').trigger('click')
    const signOut = wrapper.findAll('[role=menuitem]').find((item) => item.text() === 'Đăng xuất')
    await signOut!.trigger('click')

    expect(submit).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })

  it('saves the language to the profile and refreshes the session when signed in', async () => {
    const session = useSessionStore()
    await session.apply(Promise.resolve(seller))
    vi.mocked(saveLanguage).mockResolvedValue({ ok: true })
    vi.mocked(refreshSessionUser).mockResolvedValue({ ...seller, language: 'en' })
    const onError = vi.fn<(message: string) => void>()

    mountWithLanguage(onError).get('[data-testid=to-en]').trigger('click')
    await flushPromises()

    expect(saveLanguage).toHaveBeenCalledWith('en')
    expect(refreshSessionUser).toHaveBeenCalledTimes(1)
    expect(onError).not.toHaveBeenCalled()
  })

  it('keeps a visitor’s choice in the browser without calling the API', async () => {
    const session = useSessionStore()
    await session.apply(Promise.resolve(null))

    mountWithLanguage(vi.fn<(message: string) => void>()).get('[data-testid=to-en]').trigger('click')
    await flushPromises()

    expect(saveLanguage).not.toHaveBeenCalled()
    expect(localStorage.getItem('hs-locale')).toBe('en')
  })

  it('keeps the new language and reports the server message when the save fails', async () => {
    const session = useSessionStore()
    await session.apply(Promise.resolve(seller))
    vi.mocked(saveLanguage).mockResolvedValue({ ok: false, failure: { status: 500, code: 'common.internal_error', message: 'Boom' } })
    const onError = vi.fn<(message: string) => void>()

    mountWithLanguage(onError).get('[data-testid=to-en]').trigger('click')
    await flushPromises()

    expect(onError).toHaveBeenCalledWith('Boom')
    expect(refreshSessionUser).not.toHaveBeenCalled()
    expect(localStorage.getItem('hs-locale')).toBe('en')
  })
})
