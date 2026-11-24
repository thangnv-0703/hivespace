import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { Given, Then, When } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'
import { signIn, tampered, tokenForCookie, wrongAudience } from './profile.steps.ts'

// Steps for contracts/features/identity/register-shop.feature. The user signs in the way the gateway does (see profile.steps.ts)
// and calls /v1/shops with the bearer access token. A token issued before the shop shows no Seller role until it is refreshed.

const email = 'an@example.com'

async function countEvents(world: ContractWorld, eventType: string): Promise<number> {
  const result = await world.service.db.query('select count(*)::int as n from outbox where "EventType" = $1', [eventType])
  return result.rows[0].n as number
}

async function register(world: ContractWorld, name: unknown, sellerType: unknown, language?: string, spoofedUserId?: string): Promise<void> {
  await world.send('POST', '/v1/shops', { name, sellerType }, language, undefined, spoofedUserId)
}

function credentialToken(world: ContractWorld, credential: string): string | null {
  return credential === 'no token' ? null : credential === 'a tampered token' ? tampered(world) : wrongAudience(world)
}

// ---- Given ----

Given('a signed-in user with a verified email {string}', async function (this: ContractWorld, address: string) {
  await signIn(this, address, 'An Nguyen')
  await this.service.db.query('update users set email_verified_at = now() where id = $1', [this.userId])
})

Given('a signed-in user with an unverified email {string}', async function (this: ContractWorld, address: string) {
  await signIn(this, address, 'An Nguyen')
})

Given('another user owns a shop named {string}', async function (this: ContractWorld, name: string) {
  const id = randomUUID()
  this.otherUserId = id
  await this.service.db.query(
    `insert into users (id, email, normalized_email, display_name, language, email_verified_at, created_at, updated_at)
     values ($1, $2, $2, 'Owner', 'vi', now(), now(), now())`,
    [id, `${id}@example.com`],
  )
  await this.service.db.query(
    `insert into shops (id, owner_user_id, name, normalized_name, seller_type, created_at, updated_at)
     values ($1, $2, $3, $4, 'individual', now(), now())`,
    [randomUUID(), id, name, name.toLowerCase()],
  )
})

Given('the user registered a shop named {string} of type {string} and refreshed the token', async function (this: ContractWorld, name: string, sellerType: string) {
  await register(this, name, sellerType)
  assert.equal(this.response!.status, 201)
  await tokenForCookie(this, this.idpCookie!)
})

Given('the user refreshes the token', async function (this: ContractWorld) {
  await tokenForCookie(this, this.idpCookie!)
})

// ---- When ----

When('the user registers a shop named {string} of type {string}', async function (this: ContractWorld, name: string, sellerType: string) {
  await register(this, name, sellerType)
})

When('the user registers a shop in language {string} named {string} of type {string}', async function (this: ContractWorld, language: string, name: string, sellerType: string) {
  await register(this, name, sellerType, language)
})

When('the user registers a shop with a name of {int} characters of type {string}', async function (this: ContractWorld, length: number, sellerType: string) {
  await register(this, 'n'.repeat(length), sellerType)
})

When('the user registers a shop named with a control character of type {string}', async function (this: ContractWorld, sellerType: string) {
  await register(this, 'Tiệm\u0007Bánh', sellerType)
})

When('the user registers a shop with no name and no type', async function (this: ContractWorld) {
  await this.post('/v1/shops', {})
})

When('the user registers a shop with an extra key {string}', async function (this: ContractWorld, key: string) {
  await this.post('/v1/shops', { name: 'Tiệm Bánh An', sellerType: 'individual', [key]: randomUUID() })
})

When('the user registers a shop named {string} of type {string} with a spoofed X-User-Id of the other user', async function (this: ContractWorld, name: string, sellerType: string) {
  await register(this, name, sellerType, undefined, this.otherUserId)
})

When('the user reads the shop', async function (this: ContractWorld) {
  await this.get('/v1/shops/me')
})

When('the user reads the shop with a spoofed X-User-Id of the other user', async function (this: ContractWorld) {
  await this.send('GET', '/v1/shops/me', undefined, undefined, undefined, this.otherUserId)
})

When(/^a caller registers a shop with (no token|a tampered token|a wrong-audience token)$/, async function (this: ContractWorld, credential: string) {
  await signIn(this, email, 'An Nguyen')
  await this.send('POST', '/v1/shops', { name: 'Tiệm Bánh An', sellerType: 'individual' }, undefined, credentialToken(this, credential))
})

When(/^a caller reads the shop with (no token|a tampered token|a wrong-audience token)$/, async function (this: ContractWorld, credential: string) {
  await signIn(this, email, 'An Nguyen')
  await this.get('/v1/shops/me', undefined, credentialToken(this, credential))
})

// ---- Then ----

Then('the location is {string}', function (this: ContractWorld, location: string) {
  assert.equal(this.response!.headers.get('location'), location)
})

Then('the shop has name {string}, type {string} and an unfinished setup', function (this: ContractWorld, name: string, sellerType: string) {
  assert.deepEqual(
    { name: this.body.name, sellerType: this.body.sellerType, setup: this.body.setup },
    { name, sellerType, setup: { pickupAddress: false, payout: false } },
  )
})

Then('the shop is owned by the signed-in user', async function (this: ContractWorld) {
  const result = await this.service.db.query('select owner_user_id from shops where id = $1', [this.body.shopId])
  assert.equal(result.rows[0].owner_user_id, this.userId)
})

Then('the details name the field {string}', function (this: ContractWorld, field: string) {
  assert.ok((this.body.details as { field: string }[]).some((d) => d.field === field), `details do not name ${field}: ${JSON.stringify(this.body.details)}`)
})

Then(/^the user has the roles? (.+)$/, async function (this: ContractWorld, list: string) {
  const expected = [...list.matchAll(/"([^"]+)"/g)].map((m) => m[1]).sort()
  const result = await this.service.db.query('select role from role_grants where user_id = $1', [this.userId])
  assert.deepEqual(result.rows.map((r) => r.role as string).sort(), expected)
})

Then(/^exactly (\d+) ShopRegistered events? was queued$/, async function (this: ContractWorld, count: string) {
  assert.equal(await countEvents(this, 'ShopRegistered'), Number(count))
})
