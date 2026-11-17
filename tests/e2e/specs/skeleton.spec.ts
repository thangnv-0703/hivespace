import { expect, test } from '@playwright/test'

// The walking skeleton's done-when, browser half (docs/implementation-plan.md §2):
// the storefront shell renders with the design tokens and shows data that came
// through gateway → Catalog → Postgres; the language switch works; the seller centre loads at /seller.

test('storefront shows the seeded category from the database, in vi and en', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('hs-locale', 'vi'))
  await page.goto('/')

  await expect(page.getByTestId('brand')).toHaveText('HiveSpace')
  await expect(page.getByTestId('category')).toHaveText('Thời trang')

  await page.getByTestId('language-switcher').selectOption('en')
  await expect(page.getByTestId('category')).toHaveText('Fashion')
  await expect(page.locator('html')).toHaveAttribute('lang', 'en')
})

test('the shell uses the design tokens (brand colour from tokens.json)', async ({ page }) => {
  await page.goto('/')

  // theme.light action.primary.bg = brand.500 = #465fff = rgb(70, 95, 255)
  await expect(page.getByTestId('brand')).toHaveCSS('color', 'rgb(70, 95, 255)')
})

test('seller centre loads under /seller with its sidebar', async ({ page, viewport }) => {
  test.skip((viewport?.width ?? 0) < 1024, 'the sidebar is off-canvas below lg')
  await page.addInitScript(() => localStorage.setItem('hs-locale', 'en'))
  await page.goto('/seller/')

  await expect(page.getByTestId('sidebar')).toContainText('Products')
})

test('unknown storefront paths show the 404 page', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('hs-locale', 'en'))
  await page.goto('/no-such-page')

  await expect(page.getByRole('alert')).toContainText('Page not found')
})
