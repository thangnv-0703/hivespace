import { expect, type Page } from '@playwright/test'
import { When, Then } from '../support/fixtures'

// SCR-STF-016 and the header account menu, driven through the real gateway. Labels match the storefront's English copy.

async function openMenuIfCollapsed(page: Page) {
  const menu = page.getByRole('button', { name: 'Open menu' })
  if (await menu.isVisible()) await menu.click()
}

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

async function chooseDate(page: Page, day: number, month: string, year: number) {
  await page.getByLabel('Day', { exact: true }).selectOption(String(day))
  await page.getByLabel('Month', { exact: true }).selectOption(String(MONTHS.indexOf(month) + 1))
  await page.getByLabel('Year', { exact: true }).selectOption(String(year))
}

When('the user opens the profile page from the account menu', async ({ page }) => {
  await openMenuIfCollapsed(page)
  await page.getByRole('button', { name: 'Account' }).click()
  await page.getByRole('menuitem', { name: 'Profile' }).click()
  await expect(page).toHaveURL(/\/profile$/)
})

Then('the email field is read-only and shows the registration address', async ({ page, world }) => {
  const email = page.getByLabel('Email', { exact: true })
  await expect(email).toHaveValue(world.address)
  await expect(email).toHaveAttribute('readonly', '')
})

When(
  'the user saves the profile with name {string}, phone {string}, gender {string} and date of birth {int} {word} {int}',
  async ({ page }, name: string, phone: string, gender: string, day: number, month: string, year: number) => {
    await page.getByLabel('Display name').fill(name)
    await page.getByLabel('Phone').fill(phone)
    // The radio input is visually hidden (the styled circle is its label), so the click is forced on the input.
    await page.getByRole('radio', { name: gender, exact: true }).check({ force: true })
    await chooseDate(page, day, month, year)
    await page.getByRole('button', { name: 'Save changes' }).click()
  },
)

Then('a toast says {string}', async ({ page }, text: string) => {
  await expect(page.getByRole('status').filter({ hasText: text })).toBeVisible()
})

// The header name must follow the save at once, not after the 15 minute token expiry (AC-7).
Then('the account menu shows the updated name {string}', async ({ page }, name: string) => {
  await expect(page.getByTestId('account-name')).toHaveText(name)
})

Then('the phone field shows {string}', async ({ page }, phone: string) => {
  await expect(page.getByLabel('Phone')).toHaveValue(phone)
})

When('the user tries to save the date of birth {int} {word} {int}', async ({ page }, day: number, month: string, year: number) => {
  await chooseDate(page, day, month, year)
  await page.getByRole('button', { name: 'Save changes' }).click()
})

Then('the date of birth shows the error {string}', async ({ page }, message: string) => {
  await expect(page.getByRole('alert').filter({ hasText: message })).toBeVisible()
})

When('the user picks {string} as the language', async ({ page }, language: string) => {
  await page.getByRole('radio', { name: language, exact: true }).check({ force: true })
})

// A new context has no localStorage and no cookies, so the Vietnamese UI can only come from the saved profile (D-7).
Then('signing in from a fresh browser context shows the profile in Vietnamese', async ({ browser, baseURL, world }) => {
  const context = await browser.newContext({ baseURL, locale: 'en-US' })
  try {
    const page = await context.newPage()
    await page.goto('/bff/login?returnUrl=%2Fprofile')
    await expect(page).toHaveURL(/\/signin\?returnUrl=/)
    await page.getByLabel('Email').fill(world.address)
    await page.getByLabel(/^Password/).and(page.locator('input')).fill(world.password)
    await page.getByRole('button', { name: 'Sign in' }).click()
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Hồ sơ của tôi')
    await expect(page.locator('html')).toHaveAttribute('lang', 'vi')
  } finally {
    await context.close()
  }
})
