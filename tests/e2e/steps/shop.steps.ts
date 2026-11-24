import { expect, type Page } from '@playwright/test'
import { When, Then } from '../support/fixtures'

// SCR-SEL-001 and SCR-SEL-002 on the seller host (ADR-0027), reached from the storefront header. Labels match the English copy.

const seller = (path: string) => `${process.env.SELLER_URL}${path}`

// Below the md breakpoint the header collapses into a menu button that holds the Seller centre link.
async function openMenuIfCollapsed(page: Page) {
  const menu = page.getByRole('button', { name: 'Open menu' })
  if (await menu.isVisible()) await menu.click()
}

When('the user follows the {string} link in the header', async ({ page }, name: string) => {
  await openMenuIfCollapsed(page)
  await page.getByRole('link', { name }).click()
})

// The storefront session is shared through Identity, so the seller host signs the user in with no password prompt.
Then('the seller centre asks for a shop name and no password', async ({ page }) => {
  await expect(page).toHaveURL(new RegExp(`^${seller('/register-shop')}`))
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Open your shop on HiveSpace')
  await expect(page.getByLabel('Shop name')).toBeVisible()
  await expect(page.locator('input[type=password]')).toHaveCount(0)
})

When('the user registers a shop with a unique name as {string}', async ({ page, world }, sellerType: string) => {
  world.shopName = `Tiệm ${Date.now()}`
  await page.getByLabel('Shop name').fill(world.shopName)
  // The radio input is visually hidden (the styled circle is its label), so the click is forced on the input.
  await page.getByRole('radio', { name: sellerType, exact: true }).check({ force: true })
  await page.getByRole('button', { name: 'Open my shop' }).click()
})

Then('the dashboard shows the new shop and the checklist {string}', async ({ page, world }, progress: string) => {
  await expect(page).toHaveURL(seller('/'))
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Dashboard')
  await expect(page.getByTestId('shop-subtitle')).toHaveText(`${world.shopName} · Individual`)
  await expect(page.getByRole('heading', { name: 'Set up your shop' })).toBeVisible()
  await expect(page.getByTestId('setup-progress')).toHaveText(progress)
})

When('the user opens the registration page again', async ({ page }) => {
  await page.goto(seller('/register-shop'))
})

Then('the dashboard is shown instead', async ({ page }) => {
  await expect(page).toHaveURL(seller('/'))
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Dashboard')
})

// The sidebar is off-canvas below lg, so it is checked in the DOM, not for visibility.
Then('the seller centre sidebar lists {string}', async ({ page }, section: string) => {
  await expect(page.getByTestId('sidebar')).toContainText(section)
})

When('the user opens an unknown seller centre page', async ({ page }) => {
  await page.goto(seller('/no-such-page'))
})
