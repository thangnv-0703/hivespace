import { expect, type Page } from '@playwright/test'
import { emailFor } from '../support/mailpit'
import { When, Then } from '../support/fixtures'

// SCR-STF-012/013/014 driven through the real gateway. Selectors match the storefront's labels in English.

async function fillCredentials(page: Page, email: string, password: string) {
  await page.getByLabel('Email').fill(email)
  await page.getByLabel(/^Password/).and(page.locator('input')).fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
}

// Below the md breakpoint the header collapses into a menu button that holds the sign-in link and account menu.
async function openMenuIfCollapsed(page: Page) {
  const menu = page.getByRole('button', { name: 'Open menu' })
  if (await menu.isVisible()) await menu.click()
}

// ---- Register and verify ----

When('a visitor registers as {string} with a new email address', async ({ page, world }, displayName: string) => {
  world.displayName = displayName
  await page.goto('/register')
  await page.getByLabel('Display name').fill(displayName)
  await page.getByLabel('Email').fill(world.address)
  await page.getByLabel(/^Password/).and(page.locator('input')).fill(world.password)
  await page.getByLabel('Confirm password').fill(world.password)
  await page.getByRole('button', { name: 'Create account' }).click()
})

Then('the page shows the address the email was sent to', async ({ page, world }) => {
  await expect(page.getByTestId('register-notice')).toContainText(world.address)
})

Then('a verification email arrives with subject {string}', async ({ world }, subject: string) => {
  const email = await emailFor(world.address)
  expect(email.subject).toBe(subject)
  world.verifyLink = email.text.match(/https?:\/\/\S+\/verify-email\?token=\S+/)?.[0]
  expect(world.verifyLink).toBeTruthy()
})

When('the visitor opens the verification link', async ({ page, world }) => {
  await page.goto(world.verifyLink!)
})

When('the visitor opens the verification link again', async ({ page, world }) => {
  await page.goto(world.verifyLink!)
})

When('a visitor opens the verification link with token {string}', async ({ page }, token: string) => {
  await page.goto(`/verify-email?token=${token}`)
})

Then('a {string} button is offered', async ({ page }, name: string) => {
  await expect(page.getByRole('button', { name: new RegExp(name) })).toBeVisible()
})

// ---- Sign in, session, sign out ----

When('the visitor signs in from the home page', async ({ page, world }) => {
  await page.goto('/')
  await openMenuIfCollapsed(page)
  await page.getByRole('link', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/signin\?returnUrl=/)
  await fillCredentials(page, world.address, world.password)
})

When('a visitor opens the login route', async ({ page }) => {
  await page.goto('/bff/login')
  await expect(page).toHaveURL(/\/signin\?returnUrl=/)
})

When('the visitor submits email {string} and password {string}', async ({ page }, email: string, password: string) => {
  await fillCredentials(page, email, password)
})

Then('the account menu shows the display name', async ({ page, world }) => {
  await expect(page.getByTestId('account-name')).toHaveText(world.displayName)
})

Then('the browser holds only HttpOnly session cookies', async ({ context }) => {
  const session = (await context.cookies()).filter((c) => c.name.startsWith('hs.bff'))
  expect(session.length).toBeGreaterThan(0)
  expect(session.every((c) => c.httpOnly)).toBe(true)
})

Then('no token is readable by the page', async ({ page }) => {
  const storage = await page.evaluate(() => JSON.stringify({ ...localStorage, ...sessionStorage }))
  expect(storage).not.toMatch(/eyJ/)
})

When('the visitor reloads the page', async ({ page }) => {
  await page.reload()
})

When('the visitor signs out', async ({ page }) => {
  await openMenuIfCollapsed(page)
  await page.getByRole('button', { name: 'Account' }).click()
  await page.getByRole('menuitem', { name: 'Sign out' }).click()
})

Then('the {string} link is shown', async ({ page }, name: string) => {
  // The page may still be reloading after sign-out, so retry opening the menu until the link shows.
  await expect(async () => {
    await openMenuIfCollapsed(page)
    await expect(page.getByRole('link', { name })).toBeVisible({ timeout: 1_000 })
  }).toPass({ timeout: 15_000 })
})

Then('opening the login route asks for credentials again', async ({ page }) => {
  await page.goto('/bff/login')
  await expect(page).toHaveURL(/\/signin\?returnUrl=/)
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible()
})

Then('an alert says {string}', async ({ page }, text: string) => {
  await expect(page.getByRole('alert')).toContainText(text)
})

Then('the email field still holds {string}', async ({ page }, email: string) => {
  await expect(page.getByLabel('Email')).toHaveValue(email)
})
