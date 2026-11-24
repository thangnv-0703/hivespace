import { expect, test } from '@playwright/test'

// The walking skeleton's done-when, browser half (docs/implementation-plan.md §2):
// the storefront shell renders with the design tokens and shows data that came
// through gateway → Catalog → Postgres; the language switch works; the seller centre loads on its own host (ADR-0027).

test('storefront shows the seeded category from the database, in vi and en', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('hs-locale', 'vi'))
  await page.goto('/')

  await expect(page.getByTestId('brand').getByRole('img', { name: 'HiveSpace' }).first()).toBeVisible()
  await expect(page.getByTestId('category')).toHaveText('Thời trang')

  // Below md the language switcher lives in the collapsed menu; the toggle's label depends on the locale, its target does not.
  const menuToggle = page.locator('button[aria-controls="storefront-menu"][aria-expanded="false"]')
  if (await menuToggle.isVisible()) await menuToggle.click()
  // The header renders one switcher per breakpoint; only one of them is visible.
  await page.getByTestId('language-switcher').filter({ visible: true }).click()
  await page.getByRole('menuitemradio', { name: 'English' }).click()
  await expect(page.getByTestId('category')).toHaveText('Fashion')
  await expect(page.locator('html')).toHaveAttribute('lang', 'en')
})

test('the shell uses the design tokens (brand colour from tokens.json)', async ({ page }) => {
  await page.goto('/')

  // tokens.json color.brand.500 = #465fff reaches the page as a CSS variable
  const brand500 = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--color-brand-500').trim())
  expect(brand500).toBe('#465fff')
})

const sellerUrl = process.env.SELLER_URL ?? 'http://seller.localhost:5080'

test('the seller centre host serves its own app and sends a visitor to sign in', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('hs-locale', 'en'))
  // Every seller-centre page needs a signed-in seller (FEAT-009 AC-12): a visitor is sent to sign in and returns to the same page.
  // The signed-in sidebar and deep links are covered by features/register-shop.feature.
  await page.goto(`${sellerUrl}/no-such-page`)

  await expect(page).toHaveURL(/\/signin\?returnUrl=.*client_id%3Dhivespace-seller/)
})

test('the storefront links to the seller centre host', async ({ page, viewport }) => {
  test.skip((viewport?.width ?? 0) < 768, 'the header link is in the collapsed menu below md')
  await page.goto('/')

  await expect(page.getByRole('link', { name: /Kênh người bán|Seller centre/ }).first()).toHaveAttribute('href', `${sellerUrl}/`)
})

test('unknown storefront paths show the 404 page', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('hs-locale', 'en'))
  await page.goto('/no-such-page')

  await expect(page.getByRole('alert')).toContainText('Page not found')
})
