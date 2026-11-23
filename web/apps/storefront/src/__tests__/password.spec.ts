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
const profileCalls: Call[] = []

const profile = (hasPassword: boolean) => ({
  userId: 'u1',
  email: 'an@example.com',
  displayName: 'An',
  phone: null,
  gender: null,
  dateOfBirth: null,
  avatarUrl: null,
  language: 'en',
  hasPassword,
})

/** Answers the page's own profile read (FEAT-005: the mode comes from `hasPassword`), and hands every other call to `handler`. */
function mockApi(handler: () => Response | Promise<Response>, options: { hasPassword?: boolean | (() => Response) } = {}) {
  calls.length = 0
  profileCalls.length = 0
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const request = input as Request
    if (request.method === 'GET' && new URL(request.url).pathname === '/v1/me/profile') {
      profileCalls.push({ method: request.method, path: new URL(request.url).pathname, headers: request.headers, body: undefined })
      const has = options.hasPassword ?? true
      return typeof has === 'function' ? has() : json(200, profile(has))
    }
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
    ;(wrapper.findAll('input[autocomplete="new-password"]')[1]!.element as HTMLInputElement).focus() // the user submits from the last field
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
    expect(wrapper.text()).not.toContain('Confirm your new password.') // moving focus away must not report the cleared fields
  })

  it('has a hidden username field with the account email for password managers', async () => {
    mockApi(() => json(204))
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
    mockApi(() => json(204))
    const wrapper = await mountPage(fakeSession(), 'vi')

    expect(wrapper.get('h1').text()).toBe('Đổi mật khẩu')
    expect(wrapper.text()).toContain('Mật khẩu hiện tại')
  })
})

describe('ChangePasswordPage set mode and new errors (FEAT-005 AC-16)', () => {
  const setFill = async (wrapper: VueWrapper, next: string, confirm: string = next) => {
    const fields = wrapper.findAll('input[autocomplete="new-password"]')
    await fields[0]!.setValue(next)
    await fields[1]!.setValue(confirm)
  }

  it('reads the profile once and shows the set form without a current password when hasPassword is false', async () => {
    mockApi(() => json(204), { hasPassword: false })
    const wrapper = await mountPage(fakeSession())

    expect(profileCalls).toHaveLength(1)
    expect(wrapper.get('h1').text()).toBe('Set a password')
    expect(wrapper.find('input[autocomplete="current-password"]').exists()).toBe(false)
    expect(wrapper.findAll('input[autocomplete="new-password"]')).toHaveLength(2)
    expect(wrapper.get('button[type=submit]').text()).toBe('Set password')
  })

  it('shows the change form when hasPassword is true', async () => {
    mockApi(() => json(204), { hasPassword: true })
    const wrapper = await mountPage(fakeSession())

    expect(wrapper.get('h1').text()).toBe('Change password')
    expect(wrapper.find('input[autocomplete="current-password"]').exists()).toBe(true)
  })

  it('shows a spinner while the profile loads, and a retry when it fails', async () => {
    mockApi(() => json(204), { hasPassword: () => json(500, { code: 'common.internal', message: 'x' }) })
    const wrapper = await mountPage(fakeSession())

    expect(wrapper.find('[data-testid=password-load-failed]').exists()).toBe(true)
    expect(wrapper.find('form').exists()).toBe(false)
    await wrapper.get('[data-testid=password-load-failed] button').trigger('click')
    await flushPromises()
    expect(profileCalls).toHaveLength(2)
  })

  it('sends PUT /v1/me/password/initial with the new password only, then refreshes, toasts and switches to change mode', async () => {
    mockApi(() => json(204), { hasPassword: false })
    const session = fakeSession()
    const wrapper = await mountPage(session)

    await setFill(wrapper, 'world5678')
    ;(wrapper.findAll('input[autocomplete="new-password"]')[1]!.element as HTMLInputElement).focus()
    await submit(wrapper)

    const put = calls.filter((c) => c.method === 'PUT')
    expect(put).toHaveLength(1)
    expect(put[0]!.path).toBe('/v1/me/password/initial')
    expect(put[0]!.body).toEqual({ newPassword: 'world5678' })
    expect(put[0]!.headers.get('x-hs-csrf')).toBe('1')
    expect(session.refresh).toHaveBeenCalledTimes(1)
    expect(toasts.value.map((t) => t.title)).toEqual(['Password set. You can now also sign in with your email and password.'])
    expect(wrapper.get('h1').text()).toBe('Change password')
    expect(wrapper.find('input[autocomplete="current-password"]').exists()).toBe(true)
  })

  it('403 reauthentication_required shows the alert with Continue with Google, which goes through Google and back here', async () => {
    mockApi(() => json(403, { code: 'identity.reauthentication_required', message: "For your security, confirm it's you with Google first." }), { hasPassword: false })
    const goTo = vi.spyOn(navigation, 'goTo').mockImplementation(() => undefined)
    const wrapper = await mountPage(fakeSession())

    await setFill(wrapper, 'world5678')
    await submit(wrapper)

    const alert = wrapper.get('[data-testid=reauth-alert]')
    expect(alert.text()).toContain("Confirm it's you")
    expect(alert.text()).toContain("For your security, confirm it's you with Google first.")
    expect(alert.text()).toContain("You'll come back here and enter your new password again.")
    expect(value(wrapper, 'input[autocomplete="new-password"]', 0)).toBe('world5678') // kept until the browser leaves
    await wrapper.get('[data-testid=reauth-google]').trigger('click')
    expect(goTo).toHaveBeenCalledWith('/bff/login?reauth=true&returnUrl=%2Fprofile%2Fpassword')
  })

  it('409 password_already_set reloads the profile, switches to change mode and says why', async () => {
    let has = false
    mockApi(() => json(409, { code: 'identity.password_already_set', message: 'Your account already has a password. Use change password instead.' }), {
      hasPassword: () => json(200, profile(has)),
    })
    const wrapper = await mountPage(fakeSession())
    has = true

    await setFill(wrapper, 'world5678')
    await submit(wrapper)

    expect(profileCalls).toHaveLength(2)
    expect(wrapper.get('h1').text()).toBe('Change password')
    expect(wrapper.get('[data-testid=already-set]').text()).toContain('Your account already has a password. Use change password instead.')
  })

  it('400 on the new password in set mode lists the API messages', async () => {
    mockApi(
      () => json(400, { code: 'common.validation_failed', message: 'x', details: [{ field: 'newPassword', code: 'identity.password_needs_digit', message: 'Include at least one digit.' }] }),
      { hasPassword: false },
    )
    const wrapper = await mountPage(fakeSession())

    await setFill(wrapper, 'abcdefg1')
    await submit(wrapper)

    expect(wrapper.text()).toContain('Include at least one digit.')
  })

  it('423 in change mode shows the locked warning and clears only the current password', async () => {
    mockApi(() => json(423, { code: 'identity.account_locked', message: 'Too many failed attempts. This account is locked for 15 minutes.' }))
    const wrapper = await mountPage(fakeSession())

    await fill(wrapper, 'wrong9999', 'world5678')
    await submit(wrapper)

    const alert = wrapper.get('[data-testid=locked-alert]')
    expect(alert.attributes('role')).toBe('status')
    expect(alert.text()).toContain('Account temporarily locked')
    expect(alert.text()).toContain('Too many failed attempts.')
    expect(value(wrapper, 'input[autocomplete="current-password"]')).toBe('')
    expect(value(wrapper, 'input[autocomplete="new-password"]', 0)).toBe('world5678')
  })

  it.each([
    ['change', true],
    ['set', false],
  ])('429 in %s mode shows the rate-limit warning and counts down on the submit button from Retry-After', async (_name, hasPassword) => {
    vi.useFakeTimers()
    try {
      mockApi(
        () => new Response(JSON.stringify({ code: 'common.rate_limited', message: 'Too many requests.' }), { status: 429, headers: { 'Content-Type': 'application/json', 'Retry-After': '42' } }),
        { hasPassword },
      )
      const wrapper = await mountPage(fakeSession())

      if (hasPassword) await fill(wrapper, 'hello1234', 'world5678')
      else await setFill(wrapper, 'world5678')
      await submit(wrapper)

      expect(wrapper.get('[data-testid=rate-limited-alert]').text()).toContain('Too many requests.')
      const button = wrapper.get('button[type=submit]')
      expect(button.text()).toContain('(42 s)')
      expect(button.attributes('disabled')).toBeDefined()
      expect(value(wrapper, 'input[autocomplete="new-password"]', 0)).toBe('world5678')
      await vi.advanceTimersByTimeAsync(42_000)
      expect(wrapper.get('button[type=submit]').attributes('disabled')).toBeUndefined()
    } finally {
      vi.useRealTimers()
    }
  })

  it('shows the set copy in Vietnamese', async () => {
    mockApi(() => json(204), { hasPassword: false })
    const wrapper = await mountPage(fakeSession(), 'vi')

    expect(wrapper.get('h1').text()).toBe('Đặt mật khẩu')
  })
})
