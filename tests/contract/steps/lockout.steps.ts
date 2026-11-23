import assert from 'node:assert/strict'
import { Given, Then, When } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'

// Steps for contracts/features/identity/lockout.feature and the lockout scenarios of change-password.feature (FR-AUTH-011/012).
// The lock state is read and moved through the service's own Postgres: the API deliberately shows no unlock time.

const userIdOf = async (world: ContractWorld, email = world.email): Promise<string> => {
  const row = await world.service.db.query('select id from users where normalized_email = $1', [email.toLowerCase()])
  assert.equal(row.rowCount, 1, `no user ${email}`)
  return row.rows[0].id as string
}

Given('the account is locked', async function (this: ContractWorld) {
  const id = await userIdOf(this)
  await this.service.db.query(
    `insert into sign_in_lockouts (user_id, failed_count, window_started_at, locked_until)
     values ($1, 5, now(), now() + interval '15 minutes')
     on conflict (user_id) do update set failed_count = 5, window_started_at = now(), locked_until = now() + interval '15 minutes'`,
    [id],
  )
})

Given('the lock has ended', async function (this: ContractWorld) {
  await this.service.db.query(
    `update sign_in_lockouts set locked_until = now() - interval '1 minute', window_started_at = now() - interval '20 minutes'`,
  )
})

When('the visitor signs in wrongly {int} times as {string}', async function (this: ContractWorld, times: number, email: string) {
  for (let i = 0; i < times; i++) await this.post('/v1/users/sign-in', { email, password: 'wrong9999' })
})

When('the visitor signs in in language {string} with {string} and {string}', async function (this: ContractWorld, language: string, email: string, password: string) {
  await this.post('/v1/users/sign-in', { email, password }, language)
})

Then('the account is locked for 15 minutes', async function (this: ContractWorld) {
  const result = await this.service.db.query(
    `select extract(epoch from (l.locked_until - now())) as seconds
     from sign_in_lockouts l join users u on u.id = l.user_id where u.normalized_email = $1`,
    [this.email.toLowerCase()],
  )
  assert.equal(result.rowCount, 1)
  const seconds = Number(result.rows[0].seconds)
  assert.ok(seconds > 14 * 60 && seconds <= 15 * 60, `locked for ${seconds} s`)
})

Then('no Identity session cookie is set by the response', function (this: ContractWorld) {
  assert.deepEqual(this.response!.headers.getSetCookie().filter((c) => c.startsWith('hs.idp=')), [])
})

Then('the account has no lockout record', async function (this: ContractWorld) {
  const result = await this.service.db.query('select count(*)::int as n from sign_in_lockouts where user_id = $1', [await userIdOf(this)])
  assert.equal(result.rows[0].n, 0)
})

Then('no lockout record exists', async function (this: ContractWorld) {
  const result = await this.service.db.query('select count(*)::int as n from sign_in_lockouts')
  assert.equal(result.rows[0].n, 0)
})

Then('an {string} audit entry exists for the account', async function (this: ContractWorld, type: string) {
  const result = await this.service.db.query('select count(*)::int as n from audit_entries where user_id = $1 and type = $2', [await userIdOf(this), type])
  assert.equal(result.rows[0].n, 1)
})

Then('the audit has a {string} entry and a {string} entry for the account', async function (this: ContractWorld, first: string, second: string) {
  const id = await userIdOf(this)
  for (const type of [first, second]) {
    const result = await this.service.db.query('select count(*)::int as n from audit_entries where user_id = $1 and type = $2 and method = $3', [id, type, 'password'])
    assert.equal(result.rows[0].n, 1, type)
  }
})

Then('no audit entry contains the email {string}', async function (this: ContractWorld, email: string) {
  const result = await this.service.db.query('select count(*)::int as n from audit_entries a where a::text ilike $1', [`%${email}%`])
  assert.equal(result.rows[0].n, 0)
})
