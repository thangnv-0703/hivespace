import assert from 'node:assert/strict'
import { Given, Then, When } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'

// Steps for contracts/features/identity/code-sign-in.feature (FEAT-006). The code is only emailed, so it is read from the
// SignInCodeRequested outbox row; expiry and the 60-second wait are moved through the service's own Postgres.

async function codeEvents(world: ContractWorld, email?: string): Promise<{ code: string; email: string }[]> {
  const rows = await world.service.db.query(
    `select "Envelope" as envelope from outbox where "EventType" = 'SignInCodeRequested' order by "OccurredAt" desc`,
  )
  return rows.rows.map((r) => r.envelope.data).filter((d) => email === undefined || d.email === email.toLowerCase())
}

async function requestCode(world: ContractWorld, email: string): Promise<void> {
  world.earlierChallengeId = world.challengeId
  world.earlierCode = world.code
  await world.post('/v1/users/sign-in/code-challenges', { email })
  if (world.response!.status !== 201) return
  world.challengeId = world.body.challengeId
  // An unknown address gets no event, so there is no code to read.
  world.code = (await codeEvents(world, email))[0]?.code
}

const wrongCode = (world: ContractWorld) => (world.code === '000000' ? '000001' : '000000')

function submit(world: ContractWorld, challengeId: string | undefined, code: string | undefined, returnUrl?: string) {
  return world.post('/v1/users/sign-in/code', { challengeId, code, ...(returnUrl ? { returnUrl } : {}) })
}

// ---- Given ----

Given('the visitor has requested a sign-in code for {string}', async function (this: ContractWorld, email: string) {
  await requestCode(this, email)
  assert.equal(this.response!.status, 201)
})

Given('the code has expired', async function (this: ContractWorld) {
  await this.service.db.query(`update sign_in_code_challenges set expires_at = now() - interval '1 second' where id = $1`, [this.challengeId])
})

Given('the resend wait has passed', async function (this: ContractWorld) {
  await this.service.db.query(`update sign_in_code_challenges set created_at = created_at - interval '61 seconds' where id = $1`, [this.challengeId])
})

// ---- When ----

When('the visitor requests a sign-in code for {string}', async function (this: ContractWorld, email: string) {
  await requestCode(this, email)
})

When('the visitor submits the emailed code', async function (this: ContractWorld) {
  await submit(this, this.challengeId, this.code)
})

When('the visitor submits the emailed code with returnUrl {string}', async function (this: ContractWorld, returnUrl: string) {
  await submit(this, this.challengeId, this.code, returnUrl)
})

When('the visitor submits the earlier emailed code', async function (this: ContractWorld) {
  await submit(this, this.earlierChallengeId, this.earlierCode)
})

When('the visitor submits a wrong code', async function (this: ContractWorld) {
  await submit(this, this.challengeId, wrongCode(this))
})

When('the visitor submits a wrong code {int} times', async function (this: ContractWorld, times: number) {
  for (let i = 0; i < times; i++) await submit(this, this.challengeId, wrongCode(this))
})

When('the visitor submits the code {string}', async function (this: ContractWorld, code: string) {
  await submit(this, this.challengeId, code)
})

// ---- Then ----

Then('the response contains a challenge that expires in 5 minutes and allows a resend in 60 seconds', function (this: ContractWorld) {
  assert.match(this.body.challengeId, /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[0-9a-f]{4}-[0-9a-f]{12}$/)
  const now = Date.now()
  const expiresIn = (Date.parse(this.body.expiresAt) - now) / 1000
  const resendIn = (Date.parse(this.body.resendAvailableAt) - now) / 1000
  assert.ok(expiresIn > 290 && expiresIn <= 300, `expires in ${expiresIn} s`)
  assert.ok(resendIn > 50 && resendIn <= 60, `resend in ${resendIn} s`)
})

Then('a sign-in code email event was queued for {string}', async function (this: ContractWorld, email: string) {
  const events = await codeEvents(this, email)
  assert.equal(events.length, 1)
  assert.match(events[0]!.code, /^[0-9]{6}$/)
})

Then('no sign-in code email event was queued', async function (this: ContractWorld) {
  assert.equal((await codeEvents(this)).length, 0)
})

Then('the response has a Retry-After header', function (this: ContractWorld) {
  const seconds = Number(this.response!.headers.get('retry-after'))
  assert.ok(Number.isInteger(seconds) && seconds > 0 && seconds <= 60, `Retry-After: ${this.response!.headers.get('retry-after')}`)
})
