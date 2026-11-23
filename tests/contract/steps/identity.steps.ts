import assert from 'node:assert/strict'
import { Given, Then, When } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'

// Steps for contracts/features/identity/*.feature, driven over HTTP against the running Identity API.
// Three things are not reachable through the API and use the service's own Postgres: reading the
// verification token (it is only emailed), expiring a token, and counting queued events.

async function register(world: ContractWorld, email: string, password: string): Promise<void> {
  world.email = email
  await world.post('/v1/users', { email, displayName: 'An', password })
  assert.equal(world.response!.status, 201)
  world.userId = world.body.userId
  world.token = await tokenFromOutbox(world, world.body.userId)
}

async function tokenFromOutbox(world: ContractWorld, userId: string): Promise<string> {
  const rows = await world.service.db.query(
    `select "Envelope" as envelope from outbox where "EventType" = 'UserRegistered' order by "OccurredAt"`,
  )
  const match = rows.rows.find((r) => r.envelope.data.userId === userId)
  if (!match) throw new Error('No UserRegistered event for the user.')
  return match.envelope.data.verificationToken as string
}

async function countEvents(world: ContractWorld, eventType: string): Promise<number> {
  const result = await world.service.db.query('select count(*)::int as n from outbox where "EventType" = $1', [eventType])
  return result.rows[0].n as number
}

// ---- Given ----

Given('a registered user with email {string}', async function (this: ContractWorld, email: string) {
  await register(this, email, 'hello1234')
})

Given('a registered user with email {string} and password {string}', async function (this: ContractWorld, email: string, password: string) {
  await register(this, email, password)
})

Given('a registered user with a pending verification token', async function (this: ContractWorld) {
  await register(this, 'an@example.com', 'hello1234')
})

Given('a registered user with an expired verification token', async function (this: ContractWorld) {
  await register(this, 'an@example.com', 'hello1234')
  await this.service.db.query(`update verification_tokens set expires_at = now() - interval '1 hour'`)
})

Given('a registered user whose email is verified with a token', async function (this: ContractWorld) {
  await register(this, 'an@example.com', 'hello1234')
  await this.post('/v1/users/verify-email', { token: this.token })
  assert.equal(this.response!.status, 204)
})

// ---- When ----

When('a visitor registers with email {string}, name {string} and password {string}', async function (this: ContractWorld, email: string, displayName: string, password: string) {
  await this.post('/v1/users', { email, displayName, password })
})

When('a visitor registers in language {string} with email {string}, name {string} and password {string}', async function (this: ContractWorld, language: string, email: string, displayName: string, password: string) {
  await this.post('/v1/users', { email, displayName, password }, language)
})

When('the visitor verifies with that token', async function (this: ContractWorld) {
  await this.post('/v1/users/verify-email', { token: this.token })
})

When('the visitor verifies with token {string}', async function (this: ContractWorld, token: string) {
  await this.post('/v1/users/verify-email', { token })
})

When('the visitor requests a new verification email for that user', async function (this: ContractWorld) {
  this.earlierToken = this.token
  await this.post('/v1/users/verification-email', { email: this.email })
})

When('the visitor requests a new verification email for {string}', async function (this: ContractWorld, email: string) {
  this.verificationEvents = await countEvents(this, 'VerificationEmailRequested')
  await this.post('/v1/users/verification-email', { email })
})

When('the visitor signs in with {string} and {string}', async function (this: ContractWorld, email: string, password: string) {
  await this.post('/v1/users/sign-in', { email, password })
})

When('the visitor signs in with {string} and {string} and returnUrl {string}', async function (this: ContractWorld, email: string, password: string, returnUrl: string) {
  await this.post('/v1/users/sign-in', { email, password, returnUrl })
})

// ---- Then ----

Then('the response contains a user id', function (this: ContractWorld) {
  assert.match(this.body.userId, /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/)
  assert.notEqual(this.body.userId, '00000000-0000-0000-0000-000000000000')
})

Then('the earlier token is reported as expired when used', async function (this: ContractWorld) {
  await this.post('/v1/users/verify-email', { token: this.earlierToken })
  assert.equal(this.response!.status, 400)
  assert.equal(this.body.code, 'identity.verification_token_expired')
})

Then('no verification email event was queued', async function (this: ContractWorld) {
  assert.equal(await countEvents(this, 'VerificationEmailRequested'), this.verificationEvents)
})

Then('the redirect URL is {string}', function (this: ContractWorld, url: string) {
  assert.equal(this.body.redirectUrl, url)
})

Then('an Identity session cookie is set', function (this: ContractWorld) {
  const cookies = this.response!.headers.getSetCookie().filter((c) => c.startsWith('hs.idp='))
  assert.equal(cookies.length, 1)
  assert.match(cookies[0]!, /httponly/i)
  assert.match(cookies[0]!, /samesite=lax/i)
})
