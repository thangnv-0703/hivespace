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

// ---- FEAT-010: shop settings (SCR-SEL-007, SCR-SEL-008) ----

When('the user opens {string} from the dashboard checklist', async ({ page }, action: string) => {
  await page.getByRole('link', { name: action }).click()
})

Then('the pickup and payout page is shown with both sections missing', async ({ page }) => {
  await expect(page).toHaveURL(seller('/settings/pickup-payout'))
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Pickup address and payout')
  await expect(page.getByTestId('address-badge')).toHaveText('Missing')
  await expect(page.getByTestId('payout-badge')).toHaveText('Missing')
})

When('the user saves the pickup address', async ({ page }) => {
  await page.getByLabel('Contact name').fill('An Nguyen')
  await page.getByLabel('Phone').fill('0912 345 678')
  await page.getByLabel('Province or city').fill('Ha Noi')
  await page.getByLabel('District').fill('Ba Dinh')
  await page.getByLabel('Ward or commune').fill('Phuc Xa')
  await page.getByLabel('Street address').fill('12 Le Loi')
  await page.getByRole('button', { name: 'Save address' }).click()
})

Then('the pickup address badge says {string}', async ({ page }, text: string) => {
  await expect(page.getByTestId('address-badge')).toHaveText(text)
})

When('the user saves the payout with account number {string}', async ({ page }, accountNumber: string) => {
  await page.getByLabel('Bank name').fill('Vietcombank')
  await page.getByLabel('Account number').fill(accountNumber)
  await page.getByLabel('Account holder').fill('NGUYEN VAN AN')
  await page.getByRole('button', { name: 'Save payout details' }).click()
})

Then('the payout shows the masked number {string} and not the digits', async ({ page }, masked: string) => {
  await expect(page.getByTestId('masked-number')).toContainText(masked)
  await expect(page.getByTestId('payout-badge')).toHaveText('Saved')
  await expect(page.locator('body')).not.toContainText('0071')
  await expect(page.locator('input[value*="0071"]')).toHaveCount(0)
})

// The Settings group sits in the sidebar, off-canvas below lg: the link is followed by its address-bar target after opening the group.
When('the user opens {string} from the settings sidebar group', async ({ page }, name: string) => {
  const sidebar = page.getByTestId('sidebar')
  const link = sidebar.getByRole('link', { name, exact: true })
  if (!(await link.isVisible())) await sidebar.getByRole('button', { name: 'Shop settings' }).click()
  await link.click()
  await expect(page).toHaveURL(seller('/settings/profile'))
})

When('the user saves the profile description {string}', async ({ page }, description: string) => {
  await page.getByLabel('Description').fill(description)
  await page.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.getByText('Shop profile saved').first()).toBeVisible()
})

Then('the shop profile shows the description {string}', async ({ page }, description: string) => {
  await page.reload()
  await expect(page.getByLabel('Description')).toHaveValue(description)
})

When('the user opens the dashboard', async ({ page }) => {
  await page.goto(seller('/'))
})

Then('the dashboard shows the checklist {string}', async ({ page }, progress: string) => {
  await expect(page.getByTestId('setup-progress')).toHaveText(progress)
})

Then('the dashboard says the shop is ready', async ({ page }) => {
  await expect(page.getByText('Your shop is ready to publish products.')).toBeVisible()
})
