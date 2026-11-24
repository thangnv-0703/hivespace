import assert from 'node:assert/strict'
import { createHash, createPrivateKey, randomBytes, sign } from 'node:crypto'
import { Given, Then, When } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'

// Steps for contracts/features/identity/profile.feature. The user signs in the way the gateway does (cookie session,
// then authorization code with PKCE) and calls /v1/me/* with the resulting bearer access token.

const redirectUri = 'http://localhost:5080/bff/callback'
const b64url = (data: Buffer | string) => Buffer.from(data).toString('base64url')

export async function signIn(world: ContractWorld, email: string, displayName: string, password = 'hello1234'): Promise<void> {
  world.email = email
  await world.post('/v1/users', { email, displayName, password })
  assert.equal(world.response!.status, 201)
  world.userId = world.body.userId
  await world.post('/v1/users/sign-in', { email, password })
  assert.equal(world.response!.status, 200)
  const cookie = world.response!.headers.getSetCookie().find((c) => c.startsWith('hs.idp='))!.split(';')[0]!
  world.idpCookie = cookie
  await tokenForCookie(world, cookie)
}

/** Authorization code with PKCE for the browser holding `cookie` (`hs.idp=…`), then the bearer access token into `world.accessToken`. */
export async function tokenForCookie(world: ContractWorld, cookie: string): Promise<void> {
  const verifier = b64url(randomBytes(32))
  const challenge = b64url(createHash('sha256').update(verifier).digest())
  const query = new URLSearchParams({
    client_id: 'hivespace-storefront',
    response_type: 'code',
    state: 'xyz',
    redirect_uri: redirectUri,
    scope: 'openid email profile roles offline_access',
    code_challenge: challenge,
    code_challenge_method: 'S256',
  })
  const authorize = await fetch(`${world.service.baseUrl}/connect/authorize?${query}`, { headers: { cookie }, redirect: 'manual' })
  const code = new URL(authorize.headers.get('location')!, world.service.baseUrl).searchParams.get('code')
  assert.ok(code, 'authorize did not return a code')

  const token = await fetch(`${world.service.baseUrl}/connect/token`, {
    method: 'POST',
    body: new URLSearchParams({
      grant_type: 'authorization_code',
      code,
      code_verifier: verifier,
      redirect_uri: redirectUri,
      client_id: 'hivespace-storefront',
      client_secret: 'test-client-secret',
    }),
  })
  assert.equal(token.status, 200)
  world.accessToken = ((await token.json()) as { access_token: string }).access_token
}

async function countEvents(world: ContractWorld, eventType: string): Promise<number> {
  const result = await world.service.db.query('select count(*)::int as n from outbox where "EventType" = $1', [eventType])
  return result.rows[0].n as number
}

function isoDate(yearsAgo: number, dayOffset = 0): string {
  const d = new Date()
  d.setUTCFullYear(d.getUTCFullYear() - yearsAgo)
  d.setUTCDate(d.getUTCDate() + dayOffset)
  return d.toISOString().slice(0, 10)
}

/** The same access token with other claims, re-signed with the service's own key (the signature is valid, only the claims differ). */
export function resign(world: ContractWorld, change: Record<string, unknown>): string {
  const [header, payload] = world.accessToken!.split('.') as [string, string]
  const claims = JSON.parse(Buffer.from(payload, 'base64url').toString()) as Record<string, unknown>
  Object.assign(claims, change)
  const input = `${header}.${b64url(JSON.stringify(claims))}`
  const signature = sign('sha256', Buffer.from(input), { key: createPrivateKey(world.service.signingKeyPem!), dsaEncoding: 'ieee-p1363' })
  return `${input}.${b64url(signature)}`
}

/** The same access token with another audience. */
export function wrongAudience(world: ContractWorld): string {
  return resign(world, { aud: 'other-api' })
}

export function tampered(world: ContractWorld): string {
  const token = world.accessToken!
  return token.slice(0, -1) + (token.endsWith('A') ? 'B' : 'A')
}

const complete = { displayName: 'An Nguyen', phone: '0912 345 678', gender: 'male', dateOfBirth: '1990-01-01' }

// ---- Given ----

Given('a signed-in user with email {string} and name {string}', async function (this: ContractWorld, email: string, name: string) {
  await signIn(this, email, name)
})

Given('a signed-in user with email {string} and password {string}', async function (this: ContractWorld, email: string, password: string) {
  await signIn(this, email, 'An Nguyen', password)
})

Given('the account has no password', async function (this: ContractWorld) {
  await this.service.db.query('delete from credentials where user_id = $1', [this.userId])
})

Given('a signed-in user with a complete profile', async function (this: ContractWorld) {
  await signIn(this, 'an@example.com', 'An Nguyen')
  await this.put('/v1/me/profile', complete)
  assert.equal(this.response!.status, 200)
})

// ---- When ----

When('the user reads the profile', async function (this: ContractWorld) {
  await this.get('/v1/me/profile')
})

When('the user saves the profile with name {string}, phone {string}, gender {string} and date of birth {string}', async function (this: ContractWorld, displayName: string, phone: string, gender: string, dateOfBirth: string) {
  await this.put('/v1/me/profile', { displayName, phone, gender, dateOfBirth })
})

When('the user saves the profile with name {string} and no phone, gender or date of birth', async function (this: ContractWorld, displayName: string) {
  await this.put('/v1/me/profile', { displayName, phone: null, gender: null, dateOfBirth: null })
})

When('the user saves the profile with name {string}, phone {string}, gender {string} and date of birth tomorrow', async function (this: ContractWorld, displayName: string, phone: string, gender: string) {
  await this.put('/v1/me/profile', { displayName, phone, gender, dateOfBirth: isoDate(0, 2) })
})

When('the user saves the profile in language {string} with name {string}, phone {string}, gender {string} and date of birth tomorrow', async function (this: ContractWorld, language: string, displayName: string, phone: string, gender: string) {
  await this.send('PUT', '/v1/me/profile', { displayName, phone, gender, dateOfBirth: isoDate(0, 2) }, language)
})

When('the user saves the profile with a name of {int} characters', async function (this: ContractWorld, length: number) {
  await this.put('/v1/me/profile', { ...complete, displayName: 'n'.repeat(length) })
})

When('the user saves the profile with a date of birth {int} years ago', async function (this: ContractWorld, years: number) {
  await this.put('/v1/me/profile', { ...complete, dateOfBirth: isoDate(years) })
})

When('the user saves the profile with date of birth {string}', async function (this: ContractWorld, dateOfBirth: string) {
  await this.put('/v1/me/profile', { ...complete, dateOfBirth })
})

When('the user saves the profile with an extra email key {string}', async function (this: ContractWorld, email: string) {
  await this.put('/v1/me/profile', { ...complete, email })
})

When('the user changes the password from {string} to {string}', async function (this: ContractWorld, currentPassword: string, newPassword: string) {
  await this.put('/v1/me/password', { currentPassword, newPassword })
})

When('the user changes the password in language {string} from {string} to {string}', async function (this: ContractWorld, language: string, currentPassword: string, newPassword: string) {
  await this.put('/v1/me/password', { currentPassword, newPassword }, language)
})

When('the user changes the password from {string} to a password of {int} characters', async function (this: ContractWorld, currentPassword: string, length: number) {
  await this.put('/v1/me/password', { currentPassword, newPassword: 'a1' + 'x'.repeat(length - 2) })
})

When(/^a caller changes the password with (no token|a tampered token)$/, async function (this: ContractWorld, credential: string) {
  const bearer = credential === 'no token' ? null : tampered(this)
  await this.send('PUT', '/v1/me/password', { currentPassword: 'hello1234', newPassword: 'world5678' }, undefined, bearer)
})

When('the user saves the language {string}', async function (this: ContractWorld, language: string) {
  await this.put('/v1/me/profile/language', { language })
})

When(/^a caller reads the profile with (no token|a tampered token|a wrong-audience token)$/, async function (this: ContractWorld, credential: string) {
  await signIn(this, 'an@example.com', 'An Nguyen')
  const bearer = credential === 'no token' ? null : credential === 'a tampered token' ? tampered(this) : wrongAudience(this)
  await this.get('/v1/me/profile', undefined, bearer)
})

// ---- Then ----

Then('the profile has email {string}, display name {string} and language {string}', function (this: ContractWorld, email: string, displayName: string, language: string) {
  assert.deepEqual({ email: this.body.email, displayName: this.body.displayName, language: this.body.language }, { email, displayName, language })
})

Then('the profile has no phone, gender, date of birth or avatar', function (this: ContractWorld) {
  assert.deepEqual([this.body.phone, this.body.gender, this.body.dateOfBirth, this.body.avatarUrl], [null, null, null, null])
})

Then('the profile has display name {string}, phone {string}, gender {string} and date of birth {string}', function (this: ContractWorld, displayName: string, phone: string, gender: string, dateOfBirth: string) {
  assert.deepEqual(
    { displayName: this.body.displayName, phone: this.body.phone, gender: this.body.gender, dateOfBirth: this.body.dateOfBirth },
    { displayName, phone, gender, dateOfBirth },
  )
})

Then('the language in the response is {string}', function (this: ContractWorld, language: string) {
  assert.equal(this.body.language, language)
})

Then(/^exactly (\d+) PasswordChanged events? was queued$/, async function (this: ContractWorld, count: string) {
  assert.equal(await countEvents(this, 'PasswordChanged'), Number(count))
})

Then('exactly {int} UserProfileChanged event was queued', async function (this: ContractWorld, count: number) {
  assert.equal(await countEvents(this, 'UserProfileChanged'), count)
})
