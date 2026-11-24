import { createHiveI18n } from '@hivespace/core'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter, type HistoryState } from 'vue-router'

import { CodeEntryPage, CodeRequestPage, SignInPage } from '../features/auth'
import * as navigation from '../features/auth/navigation'
import enAuth from '../locales/en/auth.json'
import viAuth from '../locales/vi/auth.json'
import { messages } from '../locales'

// FEAT-006 AC-17: SCR-STF-012's link, SCR-STF-021 and SCR-STF-022.
interface Call {
  method: string
  path: string
  body: unknown
}

const calls: Call[] = []

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
    calls.push({ method: request.method, path: url.pathname, body })
    const handler = handlers[`${request.method} ${url.pathname}`]
    return handler ? handler() : new Response('{}', { status: 404, headers: { 'Content-Type': 'application/json' } })
  })
}

const json = (status: number, body?: unknown, headers: Record<string, string> = {}) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', ...headers } })

const RETURN = '/connect/authorize?x=1'
const Q = `?returnUrl=${encodeURIComponent(RETURN)}`
const KEY = 'hs-code-challenge'
const inSeconds = (s: number) => new Date(Date.now() + s * 1000).toISOString()

function storeChallenge(overrides: Record<string, unknown> = {}) {
  sessionStorage.setItem(
    KEY,
    JSON.stringify({ challengeId: 'c-1', email: 'an@example.com', expiresAt: inSeconds(300), resendAvailableAt: inSeconds(42), returnUrl: RETURN, ...overrides }),
  )
}

async function mountAt(path: string, state?: HistoryState) {
  localStorage.setItem('hs-locale', 'en')
  const i18n = createHiveI18n(messages)
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div />' } },
      { path: '/signin', component: SignInPage },
      { path: '/register', component: { template: '<div />' } },
      { path: '/signin/code', component: CodeRequestPage },
      { path: '/signin/code/enter', component: CodeEntryPage },
    ],
  })
  await router.push({ path: path.split('?')[0]!, query: Object.fromEntries(new URLSearchParams(path.split('?')[1] ?? '')), state })
  await router.isReady()
  setActivePinia(createPinia())
  const component = router.currentRoute.value.matched[0]!.components!.default!
  const wrapper = mount(component, { global: { plugins: [router, i18n, createPinia()] }, attachTo: document.body })
  await flushPromises()
  return { wrapper, router }
}

/** Path and decoded returnUrl of a route or link target. */
function target(url: string) {
  const parsed = new URL(url, 'http://x')
  return { path: parsed.pathname, returnUrl: parsed.searchParams.get('returnUrl') }
}

const digits = (wrapper: { findAll: (s: string) => { element: Element }[] }) =>
  wrapper.findAll('[data-testid^=code-digit-]').map((b) => (b.element as HTMLInputElement).value).join('')

beforeEach(() => {
  sessionStorage.clear()
  vi.spyOn(navigation, 'goTo').mockImplementation(() => undefined)
})
afterEach(() => {
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('SignInPage email code link (SCR-STF-012)', () => {
  it('links to the code page with the returnUrl and carries the typed email in router state, not the URL', async () => {
    const { wrapper, router } = await mountAt(`/signin${Q}`)

    await wrapper.findAll('input')[0]!.setValue('an@example.com')
    const link = wrapper.get('[data-testid=code-link]')
    expect(link.text()).toBe('Sign in with an email code')
    expect(target(link.attributes('href')!)).toEqual({ path: '/signin/code', returnUrl: RETURN })
    await link.trigger('click')
    await flushPromises()

    expect(target(router.currentRoute.value.fullPath)).toEqual({ path: '/signin/code', returnUrl: RETURN })
    expect(router.options.history.state.email).toBe('an@example.com')
  })
})

describe('CodeRequestPage (SCR-STF-021)', () => {
  it('starts the gateway login without a returnUrl', async () => {
    const { wrapper } = await mountAt('/signin/code')

    expect(navigation.goTo).toHaveBeenCalledWith('/bff/login')
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('prefills the email from router state, stores the challenge on 201 and goes to the code page', async () => {
    mockApi({
      'POST /v1/users/sign-in/code-challenges': () =>
        json(201, { challengeId: 'c-9', expiresAt: inSeconds(300), resendAvailableAt: inSeconds(60) }),
    })
    const { wrapper, router } = await mountAt(`/signin/code${Q}`, { email: 'an@example.com' })

    expect((wrapper.get('input').element as HTMLInputElement).value).toBe('an@example.com')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(calls[0]!.body).toEqual({ email: 'an@example.com' })
    expect(target(router.currentRoute.value.fullPath)).toEqual({ path: '/signin/code/enter', returnUrl: RETURN })
    const stored = JSON.parse(sessionStorage.getItem(KEY)!)
    expect(stored).toMatchObject({ challengeId: 'c-9', email: 'an@example.com', returnUrl: RETURN })
    expect(JSON.stringify(stored)).not.toMatch(/code"/)
  })

  it('shows the field error for a 400 and a client check before sending', async () => {
    const spy = mockApi({
      'POST /v1/users/sign-in/code-challenges': () =>
        json(400, { code: 'common.validation_failed', message: 'x', details: [{ field: 'email', code: 'identity.email_invalid', message: 'Enter a valid email address.' }] }),
    })
    const { wrapper } = await mountAt(`/signin/code${Q}`)

    await wrapper.get('form').trigger('submit')
    expect(wrapper.text()).toContain('Enter your email.')
    expect(spy).not.toHaveBeenCalled()

    await wrapper.get('input').setValue('a@b.co')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('Enter a valid email address.')
  })

  it('shows the too-soon warning with a countdown from Retry-After and offers the code already sent', async () => {
    storeChallenge()
    mockApi({
      'POST /v1/users/sign-in/code-challenges': () =>
        json(429, { code: 'identity.sign_in_code_too_soon', message: 'You just asked for a code. Wait a moment before asking again.' }, { 'Retry-After': '42' }),
    })
    const { wrapper } = await mountAt(`/signin/code${Q}`, { email: 'an@example.com' })

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const alert = wrapper.get('[data-testid=form-alert]')
    expect(alert.text()).toContain('Please wait a moment')
    expect(alert.text()).toContain('You just asked for a code.')
    expect(alert.get('a').text()).toBe('Enter the code you already have')
    const button = wrapper.get('button[type=submit]')
    expect(button.text()).toBe('Send code (42 s)')
    expect(button.attributes('disabled')).toBeDefined()
  })

  it('shows the gateway rate limit with its countdown', async () => {
    mockApi({
      'POST /v1/users/sign-in/code-challenges': () =>
        json(429, { code: 'common.rate_limited', message: 'Too many requests.' }, { 'Retry-After': '30' }),
    })
    const { wrapper } = await mountAt(`/signin/code${Q}`, { email: 'an@example.com' })

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid=form-alert]').text()).toContain('Too many attempts')
    expect(wrapper.get('button[type=submit]').text()).toBe('Send code (30 s)')
  })
})

describe('CodeEntryPage (SCR-STF-022)', () => {
  it('goes back to the request page when no challenge is stored', async () => {
    const { router } = await mountAt(`/signin/code/enter${Q}`)

    expect(target(router.currentRoute.value.fullPath)).toEqual({ path: '/signin/code', returnUrl: RETURN })
  })

  it('shows the email and the countdowns, submits a pasted code and navigates to redirectUrl', async () => {
    storeChallenge()
    mockApi({ 'POST /v1/users/sign-in/code': () => json(200, { redirectUrl: RETURN }) })
    const { wrapper } = await mountAt(`/signin/code/enter${Q}`)

    expect(wrapper.get('[data-testid=code-email]').text()).toBe('an@example.com')
    expect(wrapper.text()).toMatch(/The code expires in [45]:\d\d\./)
    const resend = wrapper.get('[data-testid=code-resend]')
    expect(resend.text()).toMatch(/^Resend code in 0:4[12]$/)
    expect(resend.attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid=code-digit-0]').trigger('paste', { clipboardData: { getData: () => '042917' } })
    await flushPromises()

    expect(calls[0]!.body).toEqual({ challengeId: 'c-1', code: '042917', returnUrl: RETURN })
    expect(navigation.goTo).toHaveBeenCalledWith(RETURN)
    expect(sessionStorage.getItem(KEY)).toBeNull()
  })

  it('shows the inline error and clears the boxes for a wrong code', async () => {
    storeChallenge()
    mockApi({
      'POST /v1/users/sign-in/code': () =>
        json(401, { code: 'identity.sign_in_code_invalid', message: 'That code is not right. Check the email and try again.' }),
    })
    const { wrapper } = await mountAt(`/signin/code/enter${Q}`)

    await wrapper.get('[data-testid=code-digit-0]').trigger('paste', { clipboardData: { getData: () => '111111' } })
    await flushPromises()

    expect(wrapper.get('[role=alert]').text()).toBe('That code is not right. Check the email and try again.')
    expect(digits(wrapper)).toBe('')
    expect(document.activeElement).toBe(wrapper.get('[data-testid=code-digit-0]').element)
  })

  it.each([
    ['identity.sign_in_code_expired', 'Code expired', 'This code has expired. Request a new code.'],
    ['identity.sign_in_challenge_ended', 'Request a new code', 'This code can no longer be used. Request a new code.'],
  ])('offers a new code for %s and stops accepting this one', async (code, title, message) => {
    storeChallenge({ resendAvailableAt: inSeconds(-1) })
    mockApi({
      'POST /v1/users/sign-in/code': () => json(401, { code, message }),
      'POST /v1/users/sign-in/code-challenges': () =>
        json(201, { challengeId: 'c-2', expiresAt: inSeconds(300), resendAvailableAt: inSeconds(60) }),
    })
    const { wrapper } = await mountAt(`/signin/code/enter${Q}`)
    await wrapper.get('[data-testid=code-digit-0]').trigger('paste', { clipboardData: { getData: () => '111111' } })
    await flushPromises()

    const alert = wrapper.get('[data-testid=code-alert]')
    expect(alert.text()).toContain(title)
    expect(alert.text()).toContain(message)
    expect(wrapper.get('[data-testid=code-digit-0]').attributes('disabled')).toBeDefined()
    const primary = wrapper.get('[data-testid=code-primary]')
    expect(primary.text()).toBe('Send a new code')

    await primary.trigger('click')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(calls[calls.length - 1]).toMatchObject({ path: '/v1/users/sign-in/code-challenges', body: { email: 'an@example.com' } })
    expect(JSON.parse(sessionStorage.getItem(KEY)!).challengeId).toBe('c-2')
    expect(wrapper.find('[data-testid=code-alert]').exists()).toBe(false)
    expect(wrapper.get('[data-testid=code-resent]').text()).toContain('New code sent.')
  })

  it('shows the locked state and moves focus to the other way to sign in', async () => {
    storeChallenge()
    mockApi({
      'POST /v1/users/sign-in/code': () =>
        json(423, { code: 'identity.account_locked', message: 'Too many failed attempts. This account is locked for 15 minutes.' }),
    })
    const { wrapper } = await mountAt(`/signin/code/enter${Q}`)

    await wrapper.get('[data-testid=code-digit-0]').trigger('paste', { clipboardData: { getData: () => '111111' } })
    await flushPromises()

    expect(wrapper.get('[data-testid=code-alert]').text()).toContain('Account temporarily locked')
    const otherWay = wrapper.get('[data-testid=other-way]')
    expect(target(otherWay.attributes('href')!)).toEqual({ path: '/signin', returnUrl: RETURN })
    expect(document.activeElement).toBe(otherWay.element)
    expect(wrapper.get('[data-testid=code-primary]').attributes('disabled')).toBeDefined()
  })

  it('resends after the wait and restarts both countdowns', async () => {
    storeChallenge({ resendAvailableAt: inSeconds(-1) })
    mockApi({
      'POST /v1/users/sign-in/code-challenges': () =>
        json(201, { challengeId: 'c-3', expiresAt: inSeconds(300), resendAvailableAt: inSeconds(60) }),
    })
    const { wrapper } = await mountAt(`/signin/code/enter${Q}`)
    expect(wrapper.get('[data-testid=code-resend]').text()).toBe('Resend code')

    await wrapper.get('[data-testid=code-resend]').trigger('click')
    await flushPromises()

    expect(JSON.parse(sessionStorage.getItem(KEY)!)).toMatchObject({ challengeId: 'c-3', returnUrl: RETURN })
    expect(wrapper.get('[data-testid=code-resend]').text()).toMatch(/^Resend code in (1:00|0:59)$/)
    expect(wrapper.get('[data-testid=code-resent]').attributes('role')).toBe('status')
  })
})

describe('FEAT-006 copy', () => {
  it('has vi and en strings for every new key', () => {
    for (const source of [enAuth.auth, viAuth.auth]) {
      expect(Object.keys(source.code.request)).toHaveLength(8)
      expect(Object.keys(source.code.entry)).toHaveLength(16)
      expect(source.signIn.codeLink).toBeTruthy()
      expect(source.errors.identity.sign_in_code_expired.title).toBeTruthy()
      expect(source.errors.identity.sign_in_challenge_ended.title).toBeTruthy()
    }
    expect(Object.keys(viAuth.auth.code.entry).sort()).toEqual(Object.keys(enAuth.auth.code.entry).sort())
  })
})
