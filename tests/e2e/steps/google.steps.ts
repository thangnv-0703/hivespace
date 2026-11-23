import { expect, type BrowserContext } from '@playwright/test'
import { emailWithSubject } from '../support/mailpit'
import { Given, When, Then } from '../support/fixtures'

// UC-025 against tools/fake-idp, which the AppHost starts on a fixed port in place of Google. The outcome and identity of the
// next sign-in are set per browser context (the fake's `fake_next` cookie, same body as POST /__fake/next), so the desktop and
// mobile projects running in parallel never see each other's identity.
const fakeIdp = process.env.FAKE_IDP_URL ?? 'http://localhost:5090'

async function nextGoogleSignIn(context: BrowserContext, next: { outcome: string; sub: string; email: string; name: string }) {
  await context.addCookies([{ name: 'fake_next', value: encodeURIComponent(JSON.stringify(next)), url: fakeIdp }])
}

Given('Google will sign in a new person named {string}', async ({ context, world }, name: string) => {
  world.address = world.address.replace('@example.com', '@gmail.com')
  await nextGoogleSignIn(context, { outcome: 'ok', sub: `sub-${world.address}`, email: world.address, name })
})

Given('a visitor registered with a password as {string}', async ({ page, world }, displayName: string) => {
  world.displayName = displayName
  // Through the gateway like the SPA does (CSRF header for non-GET); the email stays unverified.
  const response = await page.request.post('/v1/users', {
    headers: { 'X-HS-CSRF': '1', 'Accept-Language': 'en' },
    data: { email: world.address, displayName, password: world.password },
  })
  expect(response.status()).toBe(201)
})

Given('Google will sign in the same email named {string}', async ({ context, world }, name: string) => {
  await nextGoogleSignIn(context, { outcome: 'ok', sub: `sub-${world.address}`, email: world.address, name })
})

When('the visitor continues with Google from the home page', async ({ page }) => {
  await page.goto('/')
  const menu = page.getByRole('button', { name: 'Open menu' })
  if (await menu.isVisible()) await menu.click()
  await page.getByRole('link', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/signin\?returnUrl=/)
  await page.getByRole('button', { name: 'Continue with Google' }).click()
})

Then('the account menu shows {string}', async ({ page }, name: string) => {
  await expect(page).toHaveURL(/localhost:5080\/(?!signin)/)
  const menu = page.getByRole('button', { name: 'Open menu' })
  if (await menu.isVisible()) await menu.click()
  await expect(page.getByTestId('account-name')).toHaveText(name)
})

Then('an email arrives with subject {string}', async ({ world }, subject: string) => {
  const email = await emailWithSubject(world.address, subject)
  expect(email.text).toContain('Google')
})
