import { createBdd, test as base } from 'playwright-bdd'

/** State a scenario's steps share: the account being registered and the link from its email. */
export interface World {
  address: string
  displayName: string
  password: string
  verifyLink?: string
}

export const test = base.extend<{ world: World }>({
  // eslint-disable-next-line no-empty-pattern
  world: async ({}, use) => {
    await use({
      address: `e2e-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.com`,
      displayName: 'E2E User',
      password: 'correct-horse-1',
    })
  },
})

export const { Given, When, Then } = createBdd(test)
