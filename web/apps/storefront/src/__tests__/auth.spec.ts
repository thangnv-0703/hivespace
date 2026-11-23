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

const SIGNIN = '/signin?returnUrl=%2Fconnect%2Fauthorize%3Fx%3D1'

describe('SignInPage (SCR-STF-012)', () => {
  beforeEach(() => vi.spyOn(navigation, 'goTo').mockImplementation(() => undefined))

  it('starts the gateway login when opened without a returnUrl, instead of creating a half session', async () => {
    const { wrapper } = await mountAt(SignInPage, '/signin')

    expect(navigation.goTo).toHaveBeenCalledWith('/bff/login')
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('shows required errors and sends nothing when the form is empty', async () => {
    const fetchSpy = mockApi({})
    const { wrapper } = await mountAt(SignInPage, SIGNIN)

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
    const { wrapper } = await mountAt(SignInPage, SIGNIN)

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
    const { wrapper } = await mountAt(SignInPage, SIGNIN)
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

describe('Google sign-in on SCR-STF-012 and SCR-STF-013 (FEAT-004 AC-13)', () => {
  beforeEach(() => vi.spyOn(navigation, 'goTo').mockImplementation(() => undefined))

  it.each([
    ['sign in', SignInPage, SIGNIN],
    ['register', RegisterPage, '/register?returnUrl=%2Fconnect%2Fauthorize%3Fx%3D1'],
  ])('navigates to the Google start with the page returnUrl on %s, and disables the form', async (_name, page, path) => {
    const fetchSpy = mockApi({})
    const { wrapper } = await mountAt(page, path)

    await wrapper.get('[data-testid=google-button]').trigger('click')
    await flushPromises()

    expect(navigation.goTo).toHaveBeenCalledWith('/connect/external/google/start?returnUrl=%2Fconnect%2Fauthorize%3Fx%3D1')
    expect(fetchSpy).not.toHaveBeenCalled()
    expect(wrapper.get('[data-testid=google-button]').attributes('aria-busy')).toBe('true')
    for (const input of wrapper.findAll('input')) expect(input.attributes('disabled')).toBeDefined()
    expect(wrapper.get('button[type=submit]').attributes('disabled')).toBeDefined()
  })

  it('renders the button and the divider above the form, in both languages', async () => {
    mockApi({})
    const en = (await mountAt(SignInPage, SIGNIN)).wrapper
    const vi_ = (await mountAt(RegisterPage, '/register', 'vi')).wrapper

    expect(en.get('[data-testid=google-button]').text()).toBe('Continue with Google')
    expect(en.get('[role=separator]').text()).toBe('or')
    expect(en.html().indexOf('google-button')).toBeLessThan(en.html().indexOf('<form'))
    expect(vi_.get('[data-testid=google-button]').text()).toBe('Tiếp tục với Google')
    expect(vi_.get('[role=separator]').text()).toBe('hoặc')
  })

  it.each([
    ['identity.external_login_denied', 'status', 'Google sign-in cancelled', 'Đã hủy đăng nhập bằng Google'],
    ['identity.external_email_unverified', 'alert', 'Google email not verified', 'Email Google chưa được xác minh'],
    ['identity.external_login_failed', 'alert', 'Google sign-in failed', 'Đăng nhập bằng Google không thành công'],
    ['identity.external_login_unavailable', 'status', 'Google sign-in unavailable', 'Đăng nhập bằng Google đang không khả dụng'],
  ])('shows the alert for %s in en and vi, with the form still usable', async (code, role, en, vi_) => {
    mockApi({})
    for (const [locale, title] of [['en', en], ['vi', vi_]] as const) {
      const { wrapper } = await mountAt(SignInPage, `${SIGNIN}&error=${code}`, locale)

      const alert = wrapper.get('[data-testid=google-alert]')
      expect(alert.attributes('role')).toBe(role)
      expect(alert.text()).toContain(title)
      expect(wrapper.get('input[type=email]').attributes('disabled')).toBeUndefined()
      expect(wrapper.get('[data-testid=google-button]').attributes('disabled')).toBeUndefined()
      wrapper.unmount()
    }
  })

  it('points to email and password when Google is unavailable (FR-AUTH-023)', async () => {
    mockApi({})
    const { wrapper } = await mountAt(SignInPage, `${SIGNIN}&error=identity.external_login_unavailable`)

    expect(wrapper.get('[data-testid=google-alert]').text()).toContain('Sign in with your email and password, or try again later.')
  })

  it.each(['identity.something_else', 'toString', ''])('ignores an unknown error value %j', async (value) => {
    mockApi({})
    const { wrapper } = await mountAt(SignInPage, `${SIGNIN}&error=${value}`)

    expect(wrapper.find('[data-testid=google-alert]').exists()).toBe(false)
    expect(wrapper.find('[role=alert]').exists()).toBe(false)
  })

  it('drops error from the URL but keeps returnUrl, so a reload does not show the alert again', async () => {
    mockApi({})
    const { wrapper, router } = await mountAt(SignInPage, `${SIGNIN}&error=identity.external_login_failed`)
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ returnUrl: '/connect/authorize?x=1' })
    expect(wrapper.find('[data-testid=google-alert]').exists()).toBe(true)
  })

  it('a new password submit replaces the Google alert with its own result', async () => {
    mockApi({ 'POST /v1/users/sign-in': () => json(401, { code: 'identity.invalid_credentials', message: 'Wrong email or password.', details: [] }) })
    const { wrapper } = await mountAt(SignInPage, `${SIGNIN}&error=identity.external_login_failed`)

    await fill(wrapper, ['an@example.com', 'wrong-pass1'])
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid=google-alert]').exists()).toBe(false)
    expect(wrapper.findAll('[role=alert]')).toHaveLength(1)
    expect(wrapper.get('[role=alert]').text()).toContain('Wrong email or password.')
  })
})

describe('SignInPage account hardening (FEAT-005 AC-16)', () => {
  beforeEach(() => vi.spyOn(navigation, 'goTo').mockImplementation(() => undefined))

  const submitCredentials = async (wrapper: VueWrapper) => {
    await fill(wrapper, ['an@example.com', 'wrong-pass1'])
    await wrapper.get('form').trigger('submit')
    await flushPromises()
  }

  it('423 shows the locked warning, clears the password, keeps the form usable and moves focus to Continue with Google', async () => {
    mockApi({ 'POST /v1/users/sign-in': () => json(423, { code: 'identity.account_locked', message: 'Too many failed attempts. This account is locked for 15 minutes.', details: [] }) })
    const { wrapper } = await mountAt(SignInPage, SIGNIN)

    await submitCredentials(wrapper)

    const alert = wrapper.get('[data-testid=form-alert]')
    expect(alert.attributes('role')).toBe('status')
    expect(alert.text()).toContain('Account temporarily locked')
    expect(alert.text()).toContain('Too many failed attempts.')
    expect((wrapper.get('input[type=password]').element as HTMLInputElement).value).toBe('')
    expect((wrapper.get('input[type=email]').element as HTMLInputElement).value).toBe('an@example.com')
    expect(wrapper.get('input[type=email]').attributes('disabled')).toBeUndefined()
    expect(wrapper.get('button[type=submit]').attributes('disabled')).toBeUndefined()
    expect(document.activeElement).toBe(wrapper.get('[data-testid=google-button]').element)
  })

  it('429 shows the rate-limit warning and counts the Sign in button down from Retry-After, keeping the values and Google enabled', async () => {
    vi.useFakeTimers()
    try {
      mockApi({
        'POST /v1/users/sign-in': () =>
          new Response(JSON.stringify({ code: 'common.rate_limited', message: 'Too many requests. Please wait a minute and try again.', details: [] }), {
            status: 429,
            headers: { 'Content-Type': 'application/json', 'Retry-After': '42' },
          }),
      })
      const { wrapper } = await mountAt(SignInPage, SIGNIN)

      await submitCredentials(wrapper)

      expect(wrapper.get('[data-testid=form-alert]').text()).toContain('Too many attempts')
      expect(wrapper.get('[data-testid=form-alert]').text()).toContain('Too many requests. Please wait a minute and try again.')
      expect(wrapper.get('button[type=submit]').text()).toBe('Sign in (42 s)')
      expect(wrapper.get('button[type=submit]').attributes('disabled')).toBeDefined()
      expect((wrapper.get('input[type=password]').element as HTMLInputElement).value).toBe('wrong-pass1')
      expect(wrapper.get('[data-testid=google-button]').attributes('disabled')).toBeUndefined()
      await vi.advanceTimersByTimeAsync(42_000)
      expect(wrapper.get('button[type=submit]').text()).toBe('Sign in')
      expect(wrapper.get('button[type=submit]').attributes('disabled')).toBeUndefined()
      expect(wrapper.find('[data-testid=form-alert]').exists()).toBe(true) // the alert stays until the next submit
    } finally {
      vi.useRealTimers()
    }
  })

  it('429 without a Retry-After header counts down from 60', async () => {
    vi.useFakeTimers()
    try {
      mockApi({ 'POST /v1/users/sign-in': () => json(429, { code: 'common.rate_limited', message: 'Too many requests.', details: [] }) })
      const { wrapper } = await mountAt(SignInPage, SIGNIN)

      await submitCredentials(wrapper)

      expect(wrapper.get('button[type=submit]').text()).toBe('Sign in (60 s)')
    } finally {
      vi.useRealTimers()
    }
  })

  it('shows the Google rate-limit alert for ?error=common.rate_limited in both languages', async () => {
    mockApi({})
    for (const [locale, title] of [['en', 'Too many attempts'], ['vi', 'Quá nhiều lần thử']] as const) {
      const { wrapper } = await mountAt(SignInPage, `${SIGNIN}&error=common.rate_limited`, locale)

      const alert = wrapper.get('[data-testid=google-alert]')
      expect(alert.attributes('role')).toBe('status')
      expect(alert.text()).toContain(title)
      wrapper.unmount()
    }
    const { wrapper } = await mountAt(SignInPage, `${SIGNIN}&error=common.rate_limited`)
    expect(wrapper.get('[data-testid=google-alert]').text()).toContain('Too many sign-in attempts from this network. Wait a minute, then try again.')
  })

  it('an error without a returnUrl shows the alert and Try again instead of starting the login, which then starts it', async () => {
    mockApi({})
    const { wrapper } = await mountAt(SignInPage, '/signin?error=common.rate_limited')

    expect(navigation.goTo).not.toHaveBeenCalled()
    expect(wrapper.get('[data-testid=google-alert]').text()).toContain('Too many attempts')
    expect(wrapper.find('form').exists()).toBe(false)
    expect(wrapper.find('[data-testid=google-button]').exists()).toBe(false)
    await wrapper.get('[data-testid=signin-retry]').trigger('click')
    expect(navigation.goTo).toHaveBeenCalledWith('/bff/login')
    expect(wrapper.get('[data-testid=signin-retry]').text()).toBe('Try again')
  })

  it('an unknown error without a returnUrl still starts the login at once', async () => {
    mockApi({})
    await mountAt(SignInPage, '/signin?error=identity.something_else')

    expect(navigation.goTo).toHaveBeenCalledWith('/bff/login')
  })

  it('a returnUrl with max_age shows the confirm-it-is-you notice, and any other alert replaces it', async () => {
    mockApi({ 'POST /v1/users/sign-in': () => json(401, { code: 'identity.invalid_credentials', message: 'Wrong email or password.', details: [] }) })
    const { wrapper } = await mountAt(SignInPage, '/signin?returnUrl=' + encodeURIComponent('/connect/authorize?x=1&max_age=60'))

    expect(wrapper.get('[data-testid=reauth-notice]').text()).toContain("Confirm it's you")
    expect(wrapper.get('[data-testid=reauth-notice]').text()).toContain('For your security, sign in again to continue.')
    await submitCredentials(wrapper)
    expect(wrapper.find('[data-testid=reauth-notice]').exists()).toBe(false)
  })

  it('shows no notice without max_age', async () => {
    mockApi({})
    const { wrapper } = await mountAt(SignInPage, SIGNIN)

    expect(wrapper.find('[data-testid=reauth-notice]').exists()).toBe(false)
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

  it('shows the initials of the first and last word of the display name in the account menu', async () => {
    mockApi({ 'GET /bff/user': () => json(200, { isAuthenticated: true, userId: 'u1', email: 'l@example.com', displayName: 'Linh Thi Nguyen', emailVerified: true, roles: ['Buyer'], language: 'en' }) })
    const { wrapper } = await mountAt(AccountMenu, '/')
    await useSessionStore().load()
    await flushPromises()

    expect(wrapper.get('button[aria-label=Account]').text()).toBe('LN')
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
