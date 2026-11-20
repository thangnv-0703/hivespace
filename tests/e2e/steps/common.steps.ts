import { expect } from '@playwright/test'
import { Given, Then } from '../support/fixtures'

Given('the storefront is shown in English', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('hs-locale', 'en'))
})

Then('the page says {string}', async ({ page }, heading: string) => {
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(heading)
})
