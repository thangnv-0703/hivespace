import assert from 'node:assert/strict'
import { Then, When } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'

let category: { name: { vi: string; en: string } } | undefined

When('a client requests the category list', async function (this: ContractWorld) {
  await this.send('GET', '/v1/categories')
})

Then('the response contains a category with slug {string}', function (this: ContractWorld, slug: string) {
  const matches = (this.body.items as { slug: string }[]).filter((c) => c.slug === slug)
  assert.equal(matches.length, 1)
  category = matches[0] as unknown as typeof category
})

Then('that category has a Vietnamese name and an English name', function () {
  assert.ok(category!.name.vi.trim().length > 0)
  assert.ok(category!.name.en.trim().length > 0)
})
