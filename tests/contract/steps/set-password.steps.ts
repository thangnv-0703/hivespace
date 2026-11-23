import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { Given, Then, When } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'
import { jar, next, signInWithGoogle } from './google.steps.ts'
import { resign, tokenForCookie } from './profile.steps.ts'

// Steps for contracts/features/identity/set-password.feature (FR-AUTH-027). A Google-only user signs in through the fake IdP, so the
// bearer token carries amr=google and a fresh auth_time; an older auth_time is the same token re-signed with the service's own key.

async function googleOnlyUser(world: ContractWorld, email: string): Promise<void> {
  world.email = email
  await next(world, { outcome: 'ok', sub: randomUUID(), email, name: 'An Nguyen' })
  jar(world).clear()
  assert.equal(await signInWithGoogle(world), '/')
  const cookie = jar(world).get('hs.idp')
  assert.ok(cookie, 'Google sign-in did not start an Identity session')
  await tokenForCookie(world, `hs.idp=${cookie}`)
  const user = await world.service.db.query('select id from users where normalized_email = $1', [email.toLowerCase()])
  world.userId = user.rows[0].id as string
}

Given('a Google-only user {string} who signed in with Google just now', async function (this: ContractWorld, email: string) {
  await googleOnlyUser(this, email)
})

Given('a Google-only user {string} whose Google sign-in was {int} minutes ago', async function (this: ContractWorld, email: string, minutes: number) {
  await googleOnlyUser(this, email)
  this.accessToken = resign(this, { auth_time: Math.floor(Date.now() / 1000) - minutes * 60 })
})

Given('the account has a password', async function (this: ContractWorld) {
  await this.service.db.query(`insert into credentials (user_id, password_hash, updated_at) values ($1, 'stub', now())`, [this.userId])
})

When('the user sets the password {string}', async function (this: ContractWorld, newPassword: string) {
  await this.put('/v1/me/password/initial', { newPassword })
})

When('the user sets the password {string} in language {string}', async function (this: ContractWorld, newPassword: string, language: string) {
  await this.put('/v1/me/password/initial', { newPassword }, language)
})

When('a caller sets the password {string} with no token', async function (this: ContractWorld, newPassword: string) {
  await this.send('PUT', '/v1/me/password/initial', { newPassword }, undefined, null)
})

Then('the profile has a password', function (this: ContractWorld) {
  assert.equal(this.body.hasPassword, true)
})

Then('the profile has no password', function (this: ContractWorld) {
  assert.equal(this.body.hasPassword, false)
})
