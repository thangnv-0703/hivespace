import assert from 'node:assert/strict'
import { Then } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'

// Steps every service's features share. Scenarios assert status and the stable error code;
// the localized message is only checked where the feature is about localization.

Then('the response status is {int}', function (this: ContractWorld, status: number) {
  assert.equal(this.response!.status, status)
})

Then('the error code is {string}', function (this: ContractWorld, code: string) {
  assert.equal(this.body.code, code)
})

Then('the error message is {string}', function (this: ContractWorld, message: string) {
  assert.equal(this.body.message, message)
})

Then('the details include {string}', function (this: ContractWorld, name: string) {
  assert.ok(
    (this.body.details as { code: string }[]).some((d) => d.code === `identity.${name}`),
    `details do not include identity.${name}: ${JSON.stringify(this.body.details)}`,
  )
})
