import { createHiveI18n } from '@hivespace/core'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import { createAuthGuard } from '../app/guards'
import { AccountMenu, useSessionStore } from '../features/auth'
import { ProfilePage, ProfileToasts, profileSessionKey, useLanguagePreference, type ProfileSession } from '../features/profile'
import * as navigation from '../features/profile/navigation'
import { toasts } from '../features/profile/toasts'
import { messages } from '../locales'
import enProfile from '../locales/en/profile.json'
import viProfile from '../locales/vi/profile.json'

interface Call {
  method: string
  path: string
  headers: Headers
  body: unknown
}

const calls: Call[] = []

/** Answers fetch by `METHOD /path`; anything unlisted is recorded and answered 404. */
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

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

const PROFILE = {
  userId: '0193a000-0000-7000-8000-000000000001',
  email: 'an@example.com',
  displayName: 'An Nguyen',
  phone: '+84912345678',
  gender: 'female',
  dateOfBirth: '1995-04-30',
  avatarUrl: null,
  language: 'vi',
  hasPassword: true,
}
const SESSION = { isAuthenticated: true, userId: PROFILE.userId, email: PROFILE.email, displayName: 'Lan Tran', emailVerified: true, roles: ['Buyer'], language: 'en' }

type FakeSession = ProfileSession & { refresh: Mock<() => Promise<void>>; markSignedOut: Mock<() => void> }

function fakeSession(signedIn = true): FakeSession {
  return { isSignedIn: () => signedIn, email: () => PROFILE.email, refresh: vi.fn<() => Promise<void>>(async () => undefined), markSignedOut: vi.fn<() => void>() }
}

async function mountPage(session: ProfileSession, locale: 'vi' | 'en' = 'en') {
  localStorage.setItem('hs-locale', locale)
  document.documentElement.lang = locale
  toasts.value = []
  const i18n = createHiveI18n(messages)
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { template: '<div />' } }] })
  const wrapper = mount(
    { components: { ProfilePage, ProfileToasts }, template: '<div><ProfilePage /><ProfileToasts /></div>' },
    { global: { plugins: [i18n, router], provide: { [profileSessionKey as symbol]: session } }, attachTo: document.body },
  )
  await flushPromises()
  return { wrapper, i18n, router }
}

const calledWith = (method: string, path: string) => calls.filter((c) => c.method === method && c.path === path)

afterEach(() => {
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('copy', () => {
  const keys = (value: object, prefix = ''): string[] =>
    Object.entries(value).flatMap(([key, v]) => (typeof v === 'object' ? keys(v as object, `${prefix}${key}.`) : [`${prefix}${key}`]))

  it('has identical profile keys in vi and en', () => {
    expect(keys(viProfile).sort()).toEqual(keys(enProfile).sort())
  })
})

describe('ProfilePage (SCR-STF-016)', () => {
  beforeEach(() => {
    mockApi({ 'GET /v1/me/profile': () => json(200, PROFILE) })
  })

  it('the Password card says Change password when the account has a password and Set a password when it has none (FEAT-005 AC-16)', async () => {
    const withPassword = (await mountPage(fakeSession())).wrapper
    expect(withPassword.get('a[href="/profile/password"]').text()).toBe('Change password')
    expect(withPassword.text()).toContain('Change the password you use to sign in.')
    withPassword.unmount()

    mockApi({ 'GET /v1/me/profile': () => json(200, { ...PROFILE, hasPassword: false }) })
    const without = (await mountPage(fakeSession())).wrapper
    expect(without.get('a[href="/profile/password"]').text()).toBe('Set a password')
    expect(without.text()).toContain("Your account doesn't have a password yet. Set one to also sign in with your email.")
  })

  it('goes to the change password page when the Password card button is clicked', async () => {
    const { wrapper, router } = await mountPage(fakeSession())

    await wrapper.get('a[href="/profile/password"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.fullPath).toBe('/profile/password')
  })

  it('shows the profile: read-only email, name, phone read as +84 912 345 678, gender and the date split in three', async () => {
    const { wrapper } = await mountPage(fakeSession())

    const inputs = wrapper.findAll('input[type="text"], input[type="tel"]')
    const email = wrapper.get('[data-testid="profile-email"]')
    expect(email.attributes('readonly')).toBeDefined()
    expect(email.attributes('aria-readonly')).toBe('true')
    expect((email.element as HTMLInputElement).value).toBe('an@example.com')
    expect(inputs.map((i) => (i.element as HTMLInputElement).value)).toContain('An Nguyen')
    expect(inputs.map((i) => (i.element as HTMLInputElement).value)).toContain('+84 912 345 678')
    expect((wrapper.get('input[type="radio"][value="female"]').element as HTMLInputElement).checked).toBe(true)
    const selects = wrapper.findAll('select').map((s) => (s.element as HTMLSelectElement).value)
    expect(selects).toEqual(['30', '4', '1995'])
    expect(wrapper.text()).toContain('AN')
  })

  it('renders the loading skeleton with aria-busy before the profile arrives', async () => {
    let release: (r: Response) => void = () => undefined
    const asked = new Promise<void>((resolved) => {
      vi.spyOn(globalThis, 'fetch').mockImplementation(
        () =>
          new Promise<Response>((resolve) => {
            release = resolve
            resolved()
          }),
      )
    })
    localStorage.setItem('hs-locale', 'en')
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { template: '<div />' } }] })
    const wrapper = mount(ProfilePage, { global: { plugins: [createHiveI18n(messages), router], provide: { [profileSessionKey as symbol]: fakeSession() } } })

    expect(wrapper.find('[data-testid="profile-loading"]').exists()).toBe(true)
    expect(wrapper.attributes('aria-busy')).toBe('true')
    await asked
    release(json(200, PROFILE))
    await flushPromises()
    expect(wrapper.find('[data-testid="profile-loading"]').exists()).toBe(false)
    expect(wrapper.attributes('aria-busy')).toBe('false')
  })

  it('saves the four fields with PUT, refreshes the session and confirms with a toast', async () => {
    mockApi({
      'GET /v1/me/profile': () => json(200, PROFILE),
      'PUT /v1/me/profile': () => json(200, { ...PROFILE, displayName: 'Lan Tran', phone: '+84987654321' }),
    })
    const session = fakeSession()
    const { wrapper } = await mountPage(session)

    await wrapper.get('input[autocomplete="name"]').setValue('  Lan Tran ')
    await wrapper.get('input[type="tel"]').setValue('0987 654 321')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const put = calledWith('PUT', '/v1/me/profile')[0]!
    expect(put.body).toEqual({ displayName: 'Lan Tran', phone: '0987 654 321', gender: 'female', dateOfBirth: '1995-04-30' })
    expect(put.headers.get('x-hs-csrf')).toBe('1')
    expect(put.headers.get('accept-language')).toBe('en')
    expect(session.refresh).toHaveBeenCalledTimes(1)
    expect(toasts.value.map((t) => t.title)).toEqual(['Profile saved'])
    expect(wrapper.get('[data-testid="profile-name"]').text()).toBe('Lan Tran')
    expect((wrapper.get('input[type="tel"]').element as HTMLInputElement).value).toBe('+84 987 654 321')
  })

  it('sends null for an empty phone, a cleared gender and a cleared date', async () => {
    mockApi({
      'GET /v1/me/profile': () => json(200, PROFILE),
      'PUT /v1/me/profile': () => json(200, { ...PROFILE, phone: null, gender: null, dateOfBirth: null }),
    })
    const { wrapper } = await mountPage(fakeSession())

    await wrapper.get('input[type="tel"]').setValue('')
    for (const button of wrapper.findAll('button').filter((b) => b.text() === 'Clear')) await button.trigger('click')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(calledWith('PUT', '/v1/me/profile')[0]!.body).toEqual({ displayName: 'An Nguyen', phone: null, gender: null, dateOfBirth: null })
  })

  it('checks an empty name and an incomplete or impossible date in the browser and sends nothing', async () => {
    const { wrapper } = await mountPage(fakeSession())

    await wrapper.get('input[autocomplete="name"]').setValue('  ')
    const [day, month, year] = wrapper.findAll('select')
    await year!.setValue('')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('Please enter your name.')
    expect(wrapper.text()).toContain('Choose day, month and year, or clear the date.')

    await wrapper.get('input[autocomplete="name"]').setValue('An')
    await day!.setValue('31')
    await month!.setValue('2')
    await year!.setValue('1995')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('This date does not exist.')
    expect(calledWith('PUT', '/v1/me/profile')).toHaveLength(0)
  })

  it('puts each server detail on its field, keeps the typed values and shows a summary for three or more', async () => {
    mockApi({
      'GET /v1/me/profile': () => json(200, PROFILE),
      'PUT /v1/me/profile': () =>
        json(400, {
          code: 'common.validation_failed',
          message: 'Some fields are not valid.',
          details: [
            { field: 'phone', code: 'identity.phone_invalid', message: 'Enter a Vietnamese phone number.' },
            { field: 'gender', code: 'identity.gender_invalid', message: 'Choose one of the listed options.' },
            { field: 'dateOfBirth', code: 'identity.date_of_birth_in_future', message: 'Date of birth cannot be in the future.' },
          ],
        }),
    })
    const session = fakeSession()
    const { wrapper } = await mountPage(session)

    await wrapper.get('input[type="tel"]').setValue('12345')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('Please fix the following')
    expect(text).toContain('Enter a Vietnamese phone number.')
    expect(text).toContain('Choose one of the listed options.')
    expect(text).toContain('Date of birth cannot be in the future.')
    expect((wrapper.get('input[type="tel"]').element as HTMLInputElement).value).toBe('12345')
    expect(session.refresh).not.toHaveBeenCalled()
    expect(toasts.value).toHaveLength(0)
  })

  it('shows a save error for a server failure and keeps the values', async () => {
    mockApi({ 'GET /v1/me/profile': () => json(200, PROFILE), 'PUT /v1/me/profile': () => json(500, { code: 'common.internal', message: 'x' }) })
    const { wrapper } = await mountPage(fakeSession())

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain("Your changes weren't saved. Try again.")
    expect((wrapper.get('input[autocomplete="name"]').element as HTMLInputElement).value).toBe('An Nguyen')
  })

  it('sends a 401 on save to the sign-in', async () => {
    mockApi({ 'GET /v1/me/profile': () => json(200, PROFILE), 'PUT /v1/me/profile': () => json(401, { code: 'common.unauthenticated', message: 'x' }) })
    const goTo = vi.spyOn(navigation, 'goTo').mockImplementation(() => undefined)
    const session = fakeSession()
    const { wrapper } = await mountPage(session)

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(session.markSignedOut).toHaveBeenCalled()
    expect(goTo).toHaveBeenCalledWith('/bff/login?returnUrl=%2Fprofile')
  })

  it('shows a load error with the support code and a Retry that loads again', async () => {
    let attempts = 0
    mockApi({
      'GET /v1/me/profile': () => (++attempts === 1 ? json(500, { code: 'common.internal', message: 'x' }) : json(200, PROFILE)),
    })
    const { wrapper } = await mountPage(fakeSession())

    expect(wrapper.text()).toContain("We couldn't load your profile.")
    expect(wrapper.text()).toContain('common.internal')
    await wrapper.findAll('button').find((b) => b.text() === 'Retry')!.trigger('click')
    await flushPromises()
    expect((wrapper.get('input[autocomplete="name"]').element as HTMLInputElement).value).toBe('An Nguyen')
  })

  it('renders in Vietnamese with the localized month names', async () => {
    const { wrapper } = await mountPage(fakeSession(), 'vi')

    expect(wrapper.text()).toContain('Hồ sơ của tôi')
    expect(wrapper.findAll('select')[1]!.text()).toContain('Tháng 4')
  })
})

describe('language switching (FR-LOC-002, UC-027 3 and 3a)', () => {
  beforeEach(() => {
    mockApi({ 'GET /v1/me/profile': () => json(200, PROFILE), 'PUT /v1/me/profile/language': () => json(200, { language: 'en' }) })
  })

  async function mountSwitcher(session: ProfileSession) {
    localStorage.setItem('hs-locale', 'vi')
    document.documentElement.lang = 'vi'
    toasts.value = []
    const i18n = createHiveI18n(messages)
    const Probe = { setup: () => useLanguagePreference(), template: '<button @click="change(\'en\')">go</button>' }
    const wrapper = mount(Probe, { global: { plugins: [i18n], provide: { [profileSessionKey as symbol]: session } } })
    return { wrapper, i18n }
  }

  it('signed in: the UI switches at once, then the profile is saved and the session refreshed', async () => {
    const session = fakeSession()
    const { wrapper, i18n } = await mountSwitcher(session)

    const click = wrapper.get('button').trigger('click')
    expect(i18n.global.locale.value).toBe('en')
    expect(document.documentElement.lang).toBe('en')
    expect(localStorage.getItem('hs-locale')).toBe('en')
    await click
    await flushPromises()

    expect(calledWith('PUT', '/v1/me/profile/language')[0]!.body).toEqual({ language: 'en' })
    expect(session.refresh).toHaveBeenCalledTimes(1)
  })

  it('visitor: the choice stays in the browser and no request is sent', async () => {
    const session = fakeSession(false)
    const { wrapper, i18n } = await mountSwitcher(session)

    await wrapper.get('button').trigger('click')
    await flushPromises()

    expect(i18n.global.locale.value).toBe('en')
    expect(localStorage.getItem('hs-locale')).toBe('en')
    expect(calls).toHaveLength(0)
    expect(session.refresh).not.toHaveBeenCalled()
  })

  it('a failed save keeps the new language for this visit and shows an error toast', async () => {
    mockApi({ 'PUT /v1/me/profile/language': () => json(500, { code: 'common.internal', message: 'Something went wrong.' }) })
    const session = fakeSession()
    const { wrapper, i18n } = await mountSwitcher(session)

    await wrapper.get('button').trigger('click')
    await flushPromises()

    expect(i18n.global.locale.value).toBe('en')
    expect(session.refresh).not.toHaveBeenCalled()
    expect(toasts.value.map((t) => [t.variant, t.title])).toEqual([['error', 'Something went wrong.']])
  })

  it('the language card selects the active language and saves a pick', async () => {
    const session = fakeSession()
    const { wrapper, i18n } = await mountPage(session, 'vi')
    expect((wrapper.get('input[type="radio"][value="vi"]').element as HTMLInputElement).checked).toBe(true)

    await wrapper.get('input[type="radio"][value="en"]').setValue(true)
    await flushPromises()

    expect(i18n.global.locale.value).toBe('en')
    expect(calledWith('PUT', '/v1/me/profile/language')).toHaveLength(1)
    expect(session.refresh).toHaveBeenCalledTimes(1)
  })
})

describe('session store and account menu', () => {
  it('refresh() replaces the user with the token claims from POST /bff/session/refresh', async () => {
    mockApi({ 'GET /bff/user': () => json(200, { ...SESSION, displayName: 'An', language: 'vi' }), 'POST /bff/session/refresh': () => json(200, SESSION) })
    setActivePinia(createPinia())
    const session = useSessionStore()
    await session.load()
    expect(session.user?.displayName).toBe('An')

    await session.refresh()

    expect(session.user).toMatchObject({ displayName: 'Lan Tran', language: 'en' })
    expect(calledWith('POST', '/bff/session/refresh')[0]!.headers.get('x-hs-csrf')).toBe('1')
  })

  it('refresh() with the session gone makes the user anonymous', async () => {
    mockApi({ 'GET /bff/user': () => json(200, SESSION), 'POST /bff/session/refresh': () => json(200, { isAuthenticated: false }) })
    setActivePinia(createPinia())
    const session = useSessionStore()
    await session.load()

    await session.refresh()

    expect(session.status).toBe('anonymous')
  })

  it('shows Profile (to /profile) and Change password (to /profile/password) above Sign out in the account menu', async () => {
    mockApi({ 'GET /bff/user': () => json(200, SESSION) })
    localStorage.setItem('hs-locale', 'en')
    const pinia = createPinia()
    setActivePinia(pinia)
    await useSessionStore().load()
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: { template: '<div />' } }, { path: '/profile', component: { template: '<div />' } }, { path: '/profile/password', component: { template: '<div />' } }] })
    await router.push('/')
    const wrapper = mount(AccountMenu, { global: { plugins: [router, createHiveI18n(messages), pinia] }, attachTo: document.body })

    await wrapper.get('button[aria-haspopup="menu"]').trigger('click')

    const rows = wrapper.findAll('[role="menuitem"]')
    expect(rows.map((r) => r.text())).toEqual(['Profile', 'Change password', 'Sign out'])
    expect(rows[0]!.attributes('href')).toBe('/profile')
    expect(rows[1]!.attributes('href')).toBe('/profile/password')
    expect(wrapper.find('[role="separator"]').exists()).toBe(true)
  })
})

describe('page guard (requiresAuth)', () => {
  async function runGuard(signedIn: boolean, assign: (url: string) => void) {
    mockApi({ 'GET /bff/user': () => json(200, signedIn ? SESSION : { isAuthenticated: false }) })
    setActivePinia(createPinia())
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/', component: { template: '<div />' } },
        { path: '/profile', component: { template: '<div />' }, meta: { requiresAuth: true } },
      ],
    })
    router.beforeEach(createAuthGuard(assign))
    await router.push('/profile?tab=1')
    return router
  }

  it('lets a signed-in user in', async () => {
    const assign = vi.fn<(url: string) => void>()

    const router = await runGuard(true, assign)

    expect(router.currentRoute.value.path).toBe('/profile')
    expect(assign).not.toHaveBeenCalled()
  })

  it('sends a visitor to the gateway sign-in with the page as returnUrl and stays out', async () => {
    const assign = vi.fn<(url: string) => void>()

    const router = await runGuard(false, assign)

    expect(assign).toHaveBeenCalledWith('/bff/login?returnUrl=%2Fprofile%3Ftab%3D1')
    expect(router.currentRoute.value.path).not.toBe('/profile')
  })
})
