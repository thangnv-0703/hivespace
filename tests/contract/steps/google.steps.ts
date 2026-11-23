import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { Before, Given, Then, When } from '@cucumber/cucumber'
import { fakeGoogle } from '../support/service.ts'
import type { ContractWorld } from '../support/world.ts'

// Steps for contracts/features/identity/google-sign-in.feature. The steps play the browser: a cookie jar for Identity, plain
// fetches to the fake IdP, and the public callback URL mapped onto the running Identity like the gateway does.

interface Hop {
  status: number
  location: string | null
  setCookies: string[]
}

const publicOrigin = 'http://localhost:5080'

function jar(world: ContractWorld): Map<string, string> {
  return ((world as unknown as { cookies?: Map<string, string> }).cookies ??= new Map())
}

function hops(world: ContractWorld): Hop[] {
  return ((world as unknown as { hops?: Hop[] }).hops ??= [])
}

async function identityGet(world: ContractWorld, route: string): Promise<Hop> {
  const cookies = jar(world)
  const cookie = [...cookies].map(([k, v]) => `${k}=${v}`).join('; ')
  const response = await fetch(world.service.baseUrl + route, { redirect: 'manual', headers: cookie ? { cookie } : {} })
  const setCookies = response.headers.getSetCookie()
  for (const c of setCookies) {
    const [pair] = c.split(';')
    const eq = pair!.indexOf('=')
    const name = pair!.slice(0, eq)
    const value = pair!.slice(eq + 1)
    if (value === '' || /expires=thu, 01 jan 1970/i.test(c)) cookies.delete(name)
    else cookies.set(name, value)
  }
  world.response = response
  await response.text()
  const hop = { status: response.status, location: response.headers.get('location'), setCookies }
  hops(world).push(hop)
  return hop
}

async function next(world: ContractWorld, body: Record<string, string>): Promise<void> {
  const response = await fetch(`${world.service.fakeIdpUrl}/__fake/next`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  })
  assert.equal(response.status, 200)
}

function startRoute(returnUrl: string | undefined, provider = 'google'): string {
  const base = `/connect/external/${provider}/start`
  return returnUrl === undefined ? base : `${base}?returnUrl=${encodeURIComponent(returnUrl)}`
}

/** start → Google authorize → callback → complete; stops at the first redirect that leaves the flow. */
async function signInWithGoogle(world: ContractWorld, returnUrl?: string): Promise<string | null> {
  hops(world).length = 0
  const start = await identityGet(world, startRoute(returnUrl))
  if (!start.location?.startsWith(world.service.fakeIdpUrl!)) return start.location
  const authorize = await fetch(start.location, { redirect: 'manual' })
  const callback = authorize.headers.get('location')
  if (!callback?.startsWith(publicOrigin)) return callback
  let hop = await identityGet(world, callback.slice(publicOrigin.length))
  while (hop.location?.startsWith('/connect/external/')) hop = await identityGet(world, hop.location)
  return hop.location
}

Before({ tags: '@UC-025' }, async function (this: ContractWorld) {
  jar(this).clear()
  await next(this, { outcome: 'ok' })
})

Given('Google will answer with a verified identity {string} named {string}', async function (this: ContractWorld, email: string, name: string) {
  await next(this, { outcome: 'ok', sub: randomUUID(), email, name })
})

Given('Google will answer with outcome {string}', async function (this: ContractWorld, outcome: string) {
  await next(this, { outcome, sub: randomUUID(), email: `${randomUUID()}@gmail.com` })
})

Given('the visitor signed in with Google', async function (this: ContractWorld) {
  assert.equal(await signInWithGoogle(this), '/')
})

When('the visitor starts Google sign-in with returnUrl {string}', async function (this: ContractWorld, returnUrl: string) {
  hops(this).length = 0
  await identityGet(this, startRoute(returnUrl))
})

When('the visitor starts sign-in with provider {string}', async function (this: ContractWorld, provider: string) {
  hops(this).length = 0
  await identityGet(this, startRoute(undefined, provider))
})

When('the visitor signs in with Google with returnUrl {string}', async function (this: ContractWorld, returnUrl: string) {
  ;(this as unknown as { finalLocation?: string | null }).finalLocation = await signInWithGoogle(this, returnUrl)
})

Then('the redirect goes to the Google authorize endpoint with', function (this: ContractWorld, table: { hashes(): { parameter: string; value: string }[] }) {
  const location = new URL(this.response!.headers.get('location')!)
  assert.equal(`${location.origin}${location.pathname}`, `${this.service.fakeIdpUrl}/authorize`)
  assert.equal(location.searchParams.get('client_id'), fakeGoogle.clientId)
  for (const { parameter, value } of table.hashes()) assert.equal(location.searchParams.get(parameter), value, parameter)
})

Then('the redirect carries a {string}, a {string} and a {string}', function (this: ContractWorld, a: string, b: string, c: string) {
  const location = new URL(this.response!.headers.get('location')!)
  for (const name of [a, b, c]) assert.ok(location.searchParams.get(name), `${name} is missing`)
})

Then('the browser ends at {string}', function (this: ContractWorld, url: string) {
  assert.equal((this as unknown as { finalLocation?: string | null }).finalLocation, url)
})

Then('no Identity session cookie is set', function (this: ContractWorld) {
  const issued = hops(this).flatMap((h) => h.setCookies).filter((c) => c.startsWith('hs.idp=') && !c.startsWith('hs.idp=;'))
  assert.deepEqual(issued, [])
})

Then(/^exactly (\d+) ExternalLoginLinked events? was queued$/, async function (this: ContractWorld, count: string) {
  const result = await this.service.db.query(`select count(*)::int as n from outbox where "EventType" = 'ExternalLoginLinked'`)
  assert.equal(result.rows[0].n, Number(count))
})
