import { expect, test } from '@playwright/test'

// EIR-UI-003: the auth pages fit a 360 px screen. A technical check, so it stays plain Playwright (ADR-0026).
test('the auth pages have no horizontal scroll at 360 px', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('hs-locale', 'en'))
  await page.setViewportSize({ width: 360, height: 780 })
  for (const path of ['/register', '/verify-email?token=x']) {
    await page.goto(path)
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)
    expect(overflow, path).toBe(false)
  }
})
