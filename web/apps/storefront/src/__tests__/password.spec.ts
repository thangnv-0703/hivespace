import { createHiveI18n } from '@hivespace/core'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi, type Mock } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import { ChangePasswordPage, ProfileToasts, profileSessionKey, type ProfileSession } from '../features/profile'
import * as navigation from '../features/profile/navigation'
import { toasts } from '../features/profile/toasts'
import { messages } from '../locales'
import enAccount from '../locales/en/password.json'
import viAccount from '../locales/vi/password.json'

// SCR-STF-018 (ui-design.md): AC-11 and the error-code table.

interface Call {
  method: string
  path: string
  headers: Headers
  body: unknown
}

const calls: Call[] = []

function mockApi(handler: () => Response | Promise<Response>) {
  calls.length = 0
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const request = input as Request
    let body: unknown
    try {
      body = await request.clone().json()
    } catch {
      body = undefined
    }
    calls.push({ method: request.method, path: new URL(request.url).pathname, headers: request.headers, body })
    return handler()
  })
}

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

type FakeSession = ProfileSession & { refresh: Mock<() => Promise<void>>; markSignedOut: Mock<() => void> }

function fakeSession(): FakeSession {
  return {
    isSignedIn: () => true,
    email: () => 'an@example.com',
    refresh: vi.fn<() => Promise<void>>(async () => undefined),
    markSignedOut: vi.fn<() => void>(),
  }
}

async function mountPage(session: ProfileSession, locale: 'vi' | 'en' = 'en') {
  localStorage.setItem('hs-locale', locale)
  document.documentElement.lang = locale
  toasts.value = []
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { template: '<div />' } }] })
  const wrapper = mount(
    { components: { ChangePasswordPage, ProfileToasts }, template: '<div><ChangePasswordPage /><ProfileToasts /></div>' },
    { global: { plugins: [createHiveI18n(messages), router], provide: { [profileSessionKey as symbol]: session } }, attachTo: document.body },
  )
  await flushPromises()
  return wrapper
}

async function fill(wrapper: VueWrapper, current: string, next: string, confirm: string = next) {
  await wrapper.get('input[autocomplete="current-password"]').setValue(current)
  const fresh = wrapper.findAll('input[autocomplete="new-password"]')
  await fresh[0]!.setValue(next)
  await fresh[1]!.setValue(confirm)
}

const submit = async (wrapper: VueWrapper) => {
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

const value = (wrapper: VueWrapper, selector: string, index = 0) => (wrapper.findAll(selector)[index]!.element as HTMLInputElement).value

afterEach(() => {
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('ChangePasswordPage (SCR-STF-018)', () => {
  it('has the same copy keys in vi and en', () => {
    const keys = (o: unknown, prefix = ''): string[] =>
      Object.entries(o as Record<string, unknown>).flatMap(([k, v]) => (typeof v === 'object' && v ? keys(v, `${prefix}${k}.`) : [`${prefix}${k}`]))
    expect(keys(viAccount).sort()).toEqual(keys(enAccount).sort())
  })

  it('sends PUT with both passwords, never the confirmation, then refreshes the session, clears the form and toasts', async () => {
    mockApi(() => json(204))
    const session = fakeSession()
    const wrapper = await mountPage(session)

    await fill(wrapper, 'hello1234', 'world5678')
    await submit(wrapper)

    const put = calls.filter((c) => c.method === 'PUT' && c.path === '/v1/me/password')
    expect(put).toHaveLength(1)
    expect(put[0]!.body).toEqual({ currentPassword: 'hello1234', newPassword: 'world5678' })
    expect(put[0]!.headers.get('x-hs-csrf')).toBe('1')
    expect(put[0]!.headers.get('accept-language')).toBe('en')
    expect(session.refresh).toHaveBeenCalledTimes(1)
    expect(toasts.value.map((t) => t.title)).toEqual(['Password changed. Other devices have been signed out.'])
    expect(value(wrapper, 'input[autocomplete="current-password"]')).toBe('')
    expect(value(wrapper, 'input[autocomplete="new-password"]', 0)).toBe('')
    expect(value(wrapper, 'input[autocomplete="new-password"]', 1)).toBe('')
    expect(document.activeElement).toBe(wrapper.get('h1').element)
  })

  it('has a hidden username field with the account email for password managers', async () => {
    const wrapper = await mountPage(fakeSession())

    const username = wrapper.get('input[autocomplete="username"]')
    expect((username.element as HTMLInputElement).value).toBe('an@example.com')
    expect(username.attributes('aria-hidden')).toBe('true')
  })

  it('sends nothing while a client check fails and shows the messages', async () => {
    mockApi(() => json(204))
    const wrapper = await mountPage(fakeSession())

    await submit(wrapper)
    expect(calls).toHaveLength(0)
    expect(wrapper.text()).toContain('Enter your current password.')
    expect(wrapper.text()).toContain('Enter a new password.')
    expect(wrapper.text()).toContain('Confirm your new password.')

    await fill(wrapper, 'hello1234', 'abc', 'abd')
    await submit(wrapper)
    expect(calls).toHaveLength(0)
    expect(wrapper.text()).toContain('Your password does not meet the rules below.')
    expect(wrapper.text()).toContain('Passwords do not match.')
    expect(wrapper.find('[data-testid="password-rules"] [data-state="error"]').exists()).toBe(true)
  })

  it('puts every server detail on its field, clears only the wrong current password and keeps the new ones', async () => {
    mockApi(() =>
      json(400, {
        code: 'common.validation_failed',
        message: 'Some fields are not valid.',
        details: [
          { field: 'currentPassword', code: 'identity.current_password_incorrect', message: 'Your current password is incorrect.' },
          { field: 'newPassword', code: 'identity.password_needs_digit', message: 'Include at least one digit.' },
        ],
      }),
    )
    const wrapper = await mountPage(fakeSession())

    await fill(wrapper, 'wrong9999', 'abcdefg1') // passes the client hints; the mocked server still rejects it
    await submit(wrapper)

    expect(wrapper.text()).toContain('Your current password is incorrect.')
    expect(wrapper.text()).toContain('Include at least one digit.')
    expect(value(wrapper, 'input[autocomplete="current-password"]')).toBe('')
    expect(value(wrapper, 'input[autocomplete="new-password"]', 0)).toBe('abcdefg1')
    expect(document.activeElement).toBe(wrapper.get('input[autocomplete="current-password"]').element)
  })

  it('shows a summary with three or more problems', async () => {
    mockApi(() => json(204))
    const wrapper = await mountPage(fakeSession())

    await submit(wrapper)

    expect(wrapper.text()).toContain('Please fix the following')
  })

  it('answers 401 by marking the session signed out and going to sign in', async () => {
    mockApi(() => json(401, { code: 'common.unauthenticated', message: 'Please sign in to continue.' }))
    const goTo = vi.spyOn(navigation, 'goTo').mockImplementation(() => undefined)
    const session = fakeSession()
    const wrapper = await mountPage(session)

    await fill(wrapper, 'hello1234', 'world5678')
    await submit(wrapper)

    expect(session.markSignedOut).toHaveBeenCalledTimes(1)
    expect(goTo).toHaveBeenCalledWith('/bff/login?returnUrl=%2Fprofile%2Fpassword')
    expect(session.refresh).not.toHaveBeenCalled()
  })

  it('shows the API message for 409 password_not_set and keeps the form', async () => {
    mockApi(() => json(409, { code: 'identity.password_not_set', message: "Your account doesn't have a password yet." }))
    const wrapper = await mountPage(fakeSession())

    await fill(wrapper, 'hello1234', 'world5678')
    await submit(wrapper)

    expect(wrapper.text()).toContain("Your account doesn't have a password yet.")
    expect(wrapper.find('form').exists()).toBe(true)
    expect(toasts.value).toEqual([])
  })

  it('shows a generic error and keeps the values when the save fails', async () => {
    mockApi(() => json(500, { code: 'common.internal', message: 'Something went wrong.' }))
    const wrapper = await mountPage(fakeSession())

    await fill(wrapper, 'hello1234', 'world5678')
    await submit(wrapper)

    expect(wrapper.text()).toContain("Your password wasn't changed. Try again.")
    expect(value(wrapper, 'input[autocomplete="new-password"]', 0)).toBe('world5678')
    expect(toasts.value).toEqual([])
  })

  it('blocks a second submit while the first is in flight', async () => {
    let release: (response: Response) => void = () => undefined
    mockApi(() => new Promise<Response>((resolve) => (release = resolve)))
    const wrapper = await mountPage(fakeSession())
    await fill(wrapper, 'hello1234', 'world5678')

    await wrapper.get('form').trigger('submit')
    await wrapper.get('form').trigger('submit')
    release(json(204))
    await flushPromises()

    expect(calls.filter((c) => c.method === 'PUT')).toHaveLength(1)
  })

  it('shows the copy in Vietnamese', async () => {
    const wrapper = await mountPage(fakeSession(), 'vi')

    expect(wrapper.get('h1').text()).toBe('Đổi mật khẩu')
    expect(wrapper.text()).toContain('Mật khẩu hiện tại')
  })
})
