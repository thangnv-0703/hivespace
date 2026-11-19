import { createHiveI18n } from '@hivespace/core'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import { AccountMenu, RegisterPage, SignInPage, UnverifiedBanner, VerifyEmailPage, useSessionStore } from '../features/auth'
import * as navigation from '../features/auth/navigation'
import enAuth from '../locales/en/auth.json'
import viAuth from '../locales/vi/auth.json'
import { messages } from '../locales'

interface Call {
  method: string
  path: string
  headers: Headers
  body: unknown
}

const calls: Call[] = []

/** Answers fetch by `METHOD /path`. Unlisted calls fail the test by returning 404 and being recorded. */
function mockApi(handlers: Record<string, () => Response>) {
  calls.length = 0
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const request = input as Request
    const url = new URL(request.url)
    let body: unknown
    try {
      body = await request.clone().json()
    } catch {
      body = undefined
    }
    calls.push({ method: request.method, path: url.pathname, headers: request.headers, body })
    const handler = handlers[`${request.method} ${url.pathname}`]
    return handler ? handler() : new Response('{}', { status: 404, headers: { 'Content-Type': 'application/json' } })
  })
}

const last = () => calls[calls.length - 1]!

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

async function mountAt(component: object, path: string, locale: 'vi' | 'en' = 'en') {
  localStorage.setItem('hs-locale', locale)
  const i18n = createHiveI18n(messages)
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div />' } },
      { path: '/signin', component },
      { path: '/register', component },
      { path: '/verify-email', component },
    ],
  })
  await router.push(path)
  await router.isReady()
  const pinia = createPinia()
  setActivePinia(pinia)
  const wrapper = mount(component, { global: { plugins: [router, i18n, pinia] }, attachTo: document.body })
  await flushPromises()
  return { wrapper, router }
}

async function fill(wrapper: VueWrapper, values: string[]) {
  const inputs = wrapper.findAll('input')
  for (const [index, value] of values.entries()) await inputs[index]!.setValue(value)
}

afterEach(() => vi.restoreAllMocks())

describe('SignInPage (SCR-STF-012)', () => {
  beforeEach(() => vi.spyOn(navigation, 'goTo').mockImplementation(() => undefined))

  it('shows required errors and sends nothing when the form is empty', async () => {
    const fetchSpy = mockApi({})
    const { wrapper } = await mountAt(SignInPage, '/signin')

    await wrapper.get('form').trigger('submit')

    expect(wrapper.text()).toContain('Enter your email.')
    expect(wrapper.text()).toContain('Enter your password.')
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('posts the credentials with CSRF and language headers, forwards returnUrl and navigates to redirectUrl', async () => {
    mockApi({ 'POST /v1/users/sign-in': () => json(200, { redirectUrl: '/connect/authorize?x=1' }) })
    const { wrapper } = await mountAt(SignInPage, '/signin?returnUrl=%2Fconnect%2Fauthorize%3Fx%3D1')

    await fill(wrapper, ['an@example.com', 'hello1234'])
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const call = calls.find((c) => c.path === '/v1/users/sign-in')!
    expect(call.body).toEqual({ email: 'an@example.com', password: 'hello1234', returnUrl: '/connect/authorize?x=1' })
    expect(call.headers.get('X-HS-CSRF')).toBe('1')
    expect(call.headers.get('Accept-Language')).toBe('en')
    expect(navigation.goTo).toHaveBeenCalledWith('/connect/authorize?x=1')
  })

  it('shows the API message for invalid credentials, clears the password and keeps the email', async () => {
    mockApi({ 'POST /v1/users/sign-in': () => json(401, { code: 'identity.invalid_credentials', message: 'Wrong email or password.', details: [] }) })
    const { wrapper } = await mountAt(SignInPage, '/signin')

    await fill(wrapper, ['an@example.com', 'wrong-pass1'])
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[role=alert]').text()).toContain('Wrong email or password.')
    const inputs = wrapper.findAll('input')
    expect((inputs[0]!.element as HTMLInputElement).value).toBe('an@example.com')
    expect((inputs[1]!.element as HTMLInputElement).value).toBe('')
  })

  it('reveals and hides the password with an accessible toggle', async () => {
    mockApi({})
    const { wrapper } = await mountAt(SignInPage, '/signin')
    const toggle = wrapper.get('button[aria-label="Show password"]')

    expect(wrapper.findAll('input')[1]!.attributes('type')).toBe('password')
    await toggle.trigger('click')
    await flushPromises()

    expect(wrapper.findAll('input')[1]!.attributes('type')).toBe('text')
    expect(toggle.attributes('aria-label')).toBe('Hide password')
  })

  it('renders in Vietnamese by default copy and keeps returnUrl on the register link', async () => {
    mockApi({})
    const { wrapper } = await mountAt(SignInPage, '/signin?returnUrl=%2Fconnect%2Fauthorize%3Fx%3D1', 'vi')

    expect(wrapper.get('h1').text()).toBe('Đăng nhập')
    expect(wrapper.get('a[href^="/register"]').attributes('href')).toContain('returnUrl=')
  })
})

describe('RegisterPage (SCR-STF-013)', () => {
  it('validates on submit: required fields, a bad email and a confirm mismatch', async () => {
    const fetchSpy = mockApi({})
    const { wrapper } = await mountAt(RegisterPage, '/register')

    await fill(wrapper, ['', 'a@', 'abc', 'xyz'])
    await wrapper.get('form').trigger('submit')

    expect(wrapper.text()).toContain('Enter a display name.')
    expect(wrapper.text()).toContain('Enter a valid email address.')
    expect(wrapper.text()).toContain('Passwords do not match.')
    // Three or more problems also produce the summary alert.
    expect(wrapper.text()).toContain('Please fix the following')
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('marks password rules as they are met', async () => {
    mockApi({})
    const { wrapper } = await mountAt(RegisterPage, '/register')

    await wrapper.findAll('input')[2]!.setValue('hello123')

    expect(wrapper.findAll('[data-testid=password-rules] li').map((li) => li.attributes('data-state'))).toEqual(['met', 'met', 'met'])
  })

  it('does not send confirmPassword, and switches to the notice with the email on 201', async () => {
    mockApi({ 'POST /v1/users': () => json(201, { userId: '01930000-0000-7000-8000-000000000001' }) })
    const { wrapper } = await mountAt(RegisterPage, '/register')

    await fill(wrapper, ['An', 'an@example.com', 'hello1234', 'hello1234'])
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(calls[0]!.body).toEqual({ email: 'an@example.com', displayName: 'An', password: 'hello1234' })
    expect(wrapper.get('[data-testid=register-notice]').text()).toContain('an@example.com')
    expect(wrapper.get('h1').text()).toBe('Check your email')
  })

  it('shows an inline sign-in link when the email is taken (409)', async () => {
    mockApi({ 'POST /v1/users': () => json(409, { code: 'identity.email_in_use', message: 'This email is already registered.', details: [] }) })
    const { wrapper } = await mountAt(RegisterPage, '/register')

    await fill(wrapper, ['An', 'an@example.com', 'hello1234', 'hello1234'])
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('This email is already registered.')
    expect(wrapper.text()).toContain('Sign in instead')
  })

  it('lists every password rule the server reports, together', async () => {
    mockApi({
      'POST /v1/users': () =>
        json(400, {
          code: 'common.validation_failed',
          message: 'Invalid.',
          details: [
            { field: 'password', code: 'password_too_short', message: 'Password is too short.' },
            { field: 'password', code: 'password_needs_digit', message: 'Add a number.' },
          ],
        }),
    })
    const { wrapper } = await mountAt(RegisterPage, '/register')

    await fill(wrapper, ['An', 'an@example.com', 'hello123', 'hello123'])
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Password is too short.')
    expect(wrapper.text()).toContain('Add a number.')
  })

  it('resends from the notice and starts the cooldown', async () => {
    mockApi({
      'POST /v1/users': () => json(201, { userId: '01930000-0000-7000-8000-000000000001' }),
      'POST /v1/users/verification-email': () => json(202),
    })
    const { wrapper } = await mountAt(RegisterPage, '/register')
    await fill(wrapper, ['An', 'an@example.com', 'hello1234', 'hello1234'])
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    await wrapper.get('[data-testid=register-notice] button').trigger('click')
    await flushPromises()

    expect(last().path).toBe('/v1/users/verification-email')
    expect(last().body).toEqual({ email: 'an@example.com' })
    expect(wrapper.text()).toContain('Sent again to an@example.com')
    expect(wrapper.get('[data-testid=register-notice] button').attributes('disabled')).toBeDefined()
  })
})

describe('VerifyEmailPage (SCR-STF-014)', () => {
  it('verifies once on load and shows the verified state with a sign-in action', async () => {
    mockApi({ 'POST /v1/users/verify-email': () => json(204) })
    const { wrapper } = await mountAt(VerifyEmailPage, '/verify-email?token=abc')

    expect(calls.filter((c) => c.path === '/v1/users/verify-email')).toHaveLength(1)
    expect(calls.find((c) => c.path === '/v1/users/verify-email')!.body).toEqual({ token: 'abc' })
    expect(wrapper.get('h1').text()).toBe('Email verified')
    expect(wrapper.get('[data-testid=verify-result]').attributes('data-state')).toBe('verified')
    expect(wrapper.text()).toContain('Sign in')
  })

  it.each([
    ['identity.verification_token_expired', 400, 'expired', 'This link has expired'],
    ['identity.verification_token_invalid', 400, 'invalid', 'This link is not valid'],
    ['identity.email_already_verified', 409, 'already', 'Your email is already verified'],
  ])('maps %s to the %s state', async (code, status, state, title) => {
    mockApi({ 'POST /v1/users/verify-email': () => json(status, { code, message: 'x', details: [] }) })
    const { wrapper } = await mountAt(VerifyEmailPage, '/verify-email?token=abc')

    expect(wrapper.get('[data-testid=verify-result]').attributes('data-state')).toBe(state)
    expect(wrapper.get('h1').text()).toBe(title)
  })

  it('offers a new link on an expired token and asks for the email', async () => {
    mockApi({
      'POST /v1/users/verify-email': () => json(400, { code: 'identity.verification_token_expired', message: 'x', details: [] }),
      'POST /v1/users/verification-email': () => json(202),
    })
    const { wrapper } = await mountAt(VerifyEmailPage, '/verify-email?token=abc')

    await wrapper.get('input[type=email]').setValue('an@example.com')
    await wrapper.findAll('button').find((b) => b.text().startsWith('Send a new link'))!.trigger('click')
    await flushPromises()

    expect(last().body).toEqual({ email: 'an@example.com' })
    expect(wrapper.text()).toContain('a new link is on its way')
  })

  it('makes no API call without a token', async () => {
    const fetchSpy = mockApi({})
    const { wrapper } = await mountAt(VerifyEmailPage, '/verify-email')

    expect(wrapper.get('h1').text()).toBe('No verification link')
    expect(calls.some((c) => c.path === '/v1/users/verify-email')).toBe(false)
    expect(fetchSpy).toHaveBeenCalledTimes(1) // only the session probe
  })

  it('shows a retry on a network failure and verifies again when it is pressed', async () => {
    let attempts = 0
    mockApi({ 'POST /v1/users/verify-email': () => (++attempts === 1 ? json(500, { code: 'common.internal_error', message: 'x' }) : json(204)) })
    const { wrapper } = await mountAt(VerifyEmailPage, '/verify-email?token=abc')
    expect(wrapper.get('[data-testid=verify-result]').attributes('data-state')).toBe('network')

    await wrapper.findAll('button').find((b) => b.text() === 'Retry')!.trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid=verify-result]').attributes('data-state')).toBe('verified')
  })
})

describe('account area and unverified banner', () => {
  const session = (verified: boolean) =>
    json(200, { isAuthenticated: true, userId: 'u1', email: 'an@example.com', displayName: 'An', emailVerified: verified, roles: ['Buyer'], language: 'en' })

  it('shows sign in and register while signed out', async () => {
    mockApi({ 'GET /bff/user': () => json(200, { isAuthenticated: false }) })
    const { wrapper } = await mountAt(AccountMenu, '/')
    await useSessionStore().load()
    await flushPromises()

    expect(wrapper.get('a[href^="/bff/login?returnUrl="]').text()).toBe('Sign in')
    expect(wrapper.text()).toContain('Register')
  })

  it('shows the name and a sign-out form POST to /bff/logout when signed in', async () => {
    mockApi({ 'GET /bff/user': () => session(true) })
    const { wrapper } = await mountAt(AccountMenu, '/')
    await useSessionStore().load()
    await flushPromises()

    expect(wrapper.get('[data-testid=account-name]').text()).toBe('An')
    const form = wrapper.get('form')
    expect(form.attributes('method')).toBe('post')
    expect(form.attributes('action')).toBe('/bff/logout')
  })

  it('shows the banner only for an unverified signed-in user, and it can be dismissed for the session', async () => {
    sessionStorage.clear()
    mockApi({ 'GET /bff/user': () => session(false) })
    const { wrapper } = await mountAt(UnverifiedBanner, '/')
    await useSessionStore().load()
    await flushPromises()

    expect(wrapper.get('[data-testid=unverified-banner]').text()).toContain('an@example.com')
    await wrapper.get('button[aria-label=Dismiss]').trigger('click')

    expect(wrapper.find('[data-testid=unverified-banner]').exists()).toBe(false)
  })

  it('hides the banner for a verified user', async () => {
    sessionStorage.clear()
    mockApi({ 'GET /bff/user': () => session(true) })
    const { wrapper } = await mountAt(UnverifiedBanner, '/')
    await useSessionStore().load()
    await flushPromises()

    expect(wrapper.find('[data-testid=unverified-banner]').exists()).toBe(false)
  })
})

describe('auth copy (NFR-L10N-004)', () => {
  const keys = (value: unknown, prefix = ''): string[] =>
    Object.entries(value as Record<string, unknown>).flatMap(([key, child]) =>
      typeof child === 'object' && child !== null ? keys(child, `${prefix}${key}.`) : [`${prefix}${key}`],
    )

  it('has the same keys in Vietnamese and English', () => {
    expect(keys(viAuth).sort()).toEqual(keys(enAuth).sort())
  })

  it('has a non-empty string for every key in both languages', () => {
    for (const source of [viAuth, enAuth]) {
      const values = JSON.stringify(source).match(/:\s*""/g)
      expect(values).toBeNull()
    }
  })
})
