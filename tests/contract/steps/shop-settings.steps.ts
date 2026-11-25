import assert from 'node:assert/strict'
import { Given, Then, When } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'
import { signIn, tampered, wrongAudience } from './profile.steps.ts'

// Steps for contracts/features/identity/shop-settings.feature. Signing in, registering a shop and the shared status/error steps
// live in profile.steps.ts, shop.steps.ts and common.steps.ts.

const accountNumber = '0071001234567'
const validAddress = { contactName: 'Nguyễn Văn An', phone: '0912345678', province: 'Hà Nội', district: 'Ba Đình', ward: 'Phúc Xá', street: '12 Lê Lợi' }
const validPayout = { bankName: 'Vietcombank', accountNumber, accountHolder: 'NGUYEN VAN AN' }
const routes = {
  updateMyShopProfile: '/v1/shops/me/profile',
  setMyShopPickupAddress: '/v1/shops/me/pickup-address',
  setMyShopPayout: '/v1/shops/me/payout',
} as const

/** `\n` typed in a feature file is a literal backslash-n; the API sees a real newline. */
const text = (value: string) => value.replace(/\\n/g, '\n')

async function countEvents(world: ContractWorld, eventType: string): Promise<number> {
  const result = await world.service.db.query('select count(*)::int as n from outbox where "EventType" = $1', [eventType])
  return result.rows[0].n as number
}

function saveProfile(world: ContractWorld, body: unknown, language?: string) {
  return world.send('PUT', routes.updateMyShopProfile, body, language)
}

// ---- Given ----

Given('a signed-in buyer who has not registered a shop', async function (this: ContractWorld) {
  await signIn(this, 'buyer@example.com', 'Binh Tran')
})

// ---- When: profile ----

When('the user saves the shop profile with name {string} and description {string}', async function (this: ContractWorld, name: string, description: string) {
  await saveProfile(this, { name, description: text(description) })
})

When('the user saves the shop profile with name {string} and no description', async function (this: ContractWorld, name: string) {
  await saveProfile(this, { name, description: null })
})

When('the user saves the shop profile in language {string} with name {string} and no description', async function (this: ContractWorld, language: string, name: string) {
  await saveProfile(this, { name, description: null }, language)
})

When('the user saves the shop profile with a description of {int} characters', async function (this: ContractWorld, length: number) {
  await saveProfile(this, { name: 'Tiệm Bánh An', description: 'd'.repeat(length) })
})

When('the user saves the shop profile with name {string} and a description with a control character', async function (this: ContractWorld, name: string) {
  await saveProfile(this, { name, description: 'bad\u0007text' })
})

When('the user saves the shop profile with only a name', async function (this: ContractWorld) {
  await saveProfile(this, { name: 'Tiệm Bánh An' })
})

When('the user saves the shop profile with an extra key {string}', async function (this: ContractWorld, key: string) {
  await saveProfile(this, { name: 'Tiệm Bánh An', description: null, [key]: 'x' })
})

When('the user saves the shop profile with a number as the name', async function (this: ContractWorld) {
  await saveProfile(this, { name: 42, description: null })
})

// ---- When: pickup address ----

When('the user saves the pickup address with phone {string} and street {string}', async function (this: ContractWorld, phone: string, street: string) {
  await this.put(routes.setMyShopPickupAddress, { ...validAddress, phone, street })
})

When('the user saves the pickup address with an extra key {string}', async function (this: ContractWorld, key: string) {
  await this.put(routes.setMyShopPickupAddress, { ...validAddress, [key]: 'x' })
})

When('the user saves a pickup address with every field invalid', async function (this: ContractWorld) {
  await this.put(routes.setMyShopPickupAddress, {
    contactName: '',
    phone: '12345',
    province: 'p'.repeat(61),
    district: 'd\u0007x',
    ward: '   ',
    street: 's'.repeat(201),
  })
})

// ---- When: payout ----

When('the user saves the payout with bank {string}, account number {string} and holder {string}', async function (this: ContractWorld, bankName: string, number: string, holder: string) {
  await this.put(routes.setMyShopPayout, { bankName, accountNumber: number, accountHolder: holder })
})

When('the user saves a payout with every field invalid', async function (this: ContractWorld) {
  await this.put(routes.setMyShopPayout, { bankName: 'b'.repeat(81), accountNumber: '12ab', accountHolder: '' })
})

When('the user saves the payout with a spoofed X-User-Id of the other user and a body shopId', async function (this: ContractWorld) {
  // The shop id in the body is an unknown key, so the save is refused; the spoofed header alone must not move the write either.
  await this.send('PUT', routes.setMyShopPayout, { ...validPayout, shopId: this.otherUserId }, undefined, undefined, this.otherUserId)
  assert.equal(this.response!.status, 400)
  await this.send('PUT', routes.setMyShopPayout, validPayout, undefined, undefined, this.otherUserId)
})

// ---- When: forbidden and unauthenticated ----

When('the buyer calls {word}', async function (this: ContractWorld, operation: keyof typeof routes) {
  const body = operation === 'updateMyShopProfile' ? { name: 'Tiệm Bánh An', description: null } : operation === 'setMyShopPickupAddress' ? validAddress : validPayout
  await this.put(routes[operation], body)
})

When(/^a caller saves shop settings with (no token|a tampered token|a wrong-audience token)$/, async function (this: ContractWorld, credential: string) {
  const token = credential === 'no token' ? null : credential === 'a tampered token' ? tampered(this) : wrongAudience(this)
  for (const route of Object.values(routes)) {
    await this.send('PUT', route, {}, undefined, token)
    assert.equal(this.response!.status, 401, route)
  }
})

// ---- Then ----

Then('the shop has no description, no pickup address and no payout and an unfinished setup', function (this: ContractWorld) {
  assert.deepEqual(
    { description: this.body.description, pickupAddress: this.body.pickupAddress, payout: this.body.payout, setup: this.body.setup },
    { description: null, pickupAddress: null, payout: null, setup: { pickupAddress: false, payout: false } },
  )
})

Then('the shop profile is name {string} and description {string}', function (this: ContractWorld, name: string, description: string) {
  assert.deepEqual({ name: this.body.name, description: this.body.description }, { name, description: text(description) })
})

Then('the shop profile is name {string} and no description', function (this: ContractWorld, name: string) {
  assert.deepEqual({ name: this.body.name, description: this.body.description }, { name, description: null })
})

Then('the pickup address has phone {string} and street {string}', function (this: ContractWorld, phone: string, street: string) {
  assert.deepEqual(this.body.pickupAddress, { ...validAddress, phone, street })
})

Then('the payout shows bank {string}, last four {string} and holder {string}', function (this: ContractWorld, bankName: string, last4: string, holder: string) {
  assert.deepEqual(this.body.payout, { bankName, accountNumberLast4: last4, accountHolder: holder })
})

Then('the response does not contain the account number {string}', function (this: ContractWorld, number: string) {
  assert.ok(!JSON.stringify(this.body).includes(number), 'the response carries the account number')
  assert.ok(!JSON.stringify(this.body).includes('accountNumber"'), 'the response has an accountNumber property')
})

Then(/^the setup shows the pickup address (done|not done) and the payout (done|not done)$/, function (this: ContractWorld, address: string, payout: string) {
  assert.deepEqual(this.body.setup, { pickupAddress: address === 'done', payout: payout === 'done' })
})

Then('the stored account number is encrypted and does not contain {string}', async function (this: ContractWorld, number: string) {
  const result = await this.service.db.query('select payout_account_ciphertext as c, payout_account_last4 as l from shops where owner_user_id = $1', [this.userId])
  const bytes = result.rows[0].c as Buffer
  assert.ok(bytes.length > 0)
  assert.ok(!bytes.toString('latin1').includes(number) && !bytes.toString('utf8').includes(number), 'the ciphertext carries the digits')
  assert.equal(result.rows[0].l, number.slice(-4))
})

Then("the other user's shop has no payout", async function (this: ContractWorld) {
  const result = await this.service.db.query('select payout_bank_name as b from shops where owner_user_id = $1', [this.otherUserId])
  assert.equal(result.rows[0].b, null)
})

Then(/^exactly (\d+) ShopChanged events? (?:was|were) queued$/, async function (this: ContractWorld, count: string) {
  assert.equal(await countEvents(this, 'ShopChanged'), Number(count))
})
