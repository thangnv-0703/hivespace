import { AfterAll, Before, setDefaultTimeout } from '@cucumber/cucumber'
import { getService, stopAll } from './service.ts'
import type { ContractWorld } from './world.ts'

// The first scenario of a service builds it, pulls and starts Postgres, and waits for /ready.
setDefaultTimeout(240_000)

Before({ tags: '@service:identity' }, async function (this: ContractWorld) {
  this.service = await getService('identity')
  await this.service.db.query('truncate table users, outbox cascade')
})

Before({ tags: '@service:catalog' }, async function (this: ContractWorld) {
  this.service = await getService('catalog')
})

AfterAll(async () => {
  await stopAll()
})
