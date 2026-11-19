import { expect, test, type Page } from '@playwright/test'

// FEAT-001 hero flow (UC-001, UC-002, AC-12, AC-15, AC-19):
// register -> email in Mailpit -> verify link -> sign in -> reload keeps the session -> sign out -> sign-in asks again.
// Mailpit's HTTP port is fixed by the AppHost (MailpitHttpPort).
const mailpit = process.env.MAILPIT_URL ?? 'http://localhost:8025'
const password = 'correct-horse-1'

async function emailFor(address: string): Promise<{ text: string; subject: string }> {
  const deadline = Date.now() + 60_000
  while (Date.now() < deadline) {
    const search = await fetch(`${mailpit}/api/v1/search?query=${encodeURIComponent(`to:${address}`)}`)
    const body = (await search.json()) as { messages: { ID: string }[] }
    if (body.messages.length > 0) {
      const message = await fetch(`${mailpit}/api/v1/message/${body.messages[0]!.ID}`)
      const detail = (await message.json()) as { Text: string; Subject: string }
      return { text: detail.Text, subject: detail.Subject }
    }
    await new Promise((resolve) => setTimeout(resolve, 500))
  }
  throw new Error(`No email for ${address} reached Mailpit within 60 s`)
}

async function useEnglish(page: Page) {
  await page.addInitScript(() => localStorage.setItem('hs-locale', 'en'))
}

test('register, verify by email link, sign in, keep the session across a reload, sign out', async ({ page, context }) => {
  await useEnglish(page)
  const address = `e2e-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.com`

  // Register (SCR-STF-013): the notice appears as soon as the API answers.
  await page.goto('/register')
  await page.getByLabel('Display name').fill('E2E User')
  await page.getByLabel('Email').fill(address)
  await page.getByLabel(/^Password/).and(page.locator('input')).fill(password)
  await page.getByLabel('Confirm password').fill(password)
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Check your email')
  await expect(page.getByTestId('register-notice')).toContainText(address)

  // The email arrives in Mailpit in the registration language, with a working link (AC-7).
  const email = await emailFor(address)
  expect(email.subject).toBe('Verify your HiveSpace email')
  const link = email.text.match(/https?:\/\/\S+\/verify-email\?token=\S+/)?.[0]
  expect(link).toBeTruthy()

  // Verify (SCR-STF-014): the page verifies once, and the link is single use.
  await page.goto(link!)
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Email verified')
  await page.goto(link!)
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Your email is already verified')

  // Sign in through the gateway: /bff/login -> Identity -> sign-in page -> authorize -> callback -> home (AC-12).
  await page.goto('/')
  await page.getByRole('link', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/signin\?returnUrl=/)
  await page.getByLabel('Email').fill(address)
  await page.getByLabel(/^Password/).and(page.locator('input')).fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByTestId('account-name')).toHaveText('E2E User')

  // The browser holds only the encrypted HttpOnly cookie(s); no token is readable from the page (AC-12).
  const cookies = await context.cookies()
  const session = cookies.filter((c) => c.name.startsWith('hs.bff'))
  expect(session.length).toBeGreaterThan(0)
  expect(session.every((c) => c.httpOnly)).toBe(true)
  const storage = await page.evaluate(() => JSON.stringify({ ...localStorage, ...sessionStorage }))
  expect(storage).not.toMatch(/eyJ/)

  // A reload keeps the session.
  await page.reload()
  await expect(page.getByTestId('account-name')).toHaveText('E2E User')

  // Sign out: a same-origin form POST that ends in Identity's end-session, so the next sign-in asks again (AC-15).
  await page.getByRole('button', { name: 'Account' }).click()
  await page.getByRole('menuitem', { name: 'Sign out' }).click()
  await expect(page.getByRole('link', { name: 'Sign in' })).toBeVisible()
  await page.goto('/bff/login')
  await expect(page).toHaveURL(/\/signin\?returnUrl=/)
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible()
})

test('a wrong password shows one generic error and keeps the email', async ({ page }) => {
  await useEnglish(page)
  await page.goto('/bff/login')
  await expect(page).toHaveURL(/\/signin\?returnUrl=/)

  await page.getByLabel('Email').fill('nobody@example.com')
  await page.getByLabel(/^Password/).and(page.locator('input')).fill('not-a-password-1')
  await page.getByRole('button', { name: 'Sign in' }).click()

  await expect(page.getByRole('alert')).toContainText('Sign-in failed')
  await expect(page.getByLabel('Email')).toHaveValue('nobody@example.com')
})

test('an invalid verification link shows the request-a-new-link state', async ({ page }) => {
  await useEnglish(page)
  await page.goto('/verify-email?token=not-a-real-token')

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('This link is not valid')
  await expect(page.getByRole('button', { name: /Send a new link/ })).toBeVisible()
})

test('the auth pages have no horizontal scroll at 360 px', async ({ page }) => {
  await useEnglish(page)
  await page.setViewportSize({ width: 360, height: 780 })
  for (const path of ['/register', '/verify-email?token=x']) {
    await page.goto(path)
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)
    expect(overflow, path).toBe(false)
  }
})
