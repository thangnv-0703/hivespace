import { defineConfig, devices } from '@playwright/test'
import { defineBddProject } from 'playwright-bdd'

// Drives the real local stack through the gateway: the storefront host, with the seller centre on its own host (ADR-0027).
// If nothing answers on BASE_URL, Playwright starts the AppHost itself and waits for the gateway.
const baseURL = process.env.BASE_URL ?? 'http://localhost:5080'
process.env.SELLER_URL ??= 'http://seller.localhost:5080'

const desktop = { ...devices['Desktop Chrome'] }
// EIR-UI-003: the UI works at 360 px.
const mobile = { ...devices['Desktop Chrome'], viewport: { width: 360, height: 780 } }

// User journeys are Gherkin in features/ (ADR-0026); `bddgen` turns them into Playwright tests first.
const journeys = { features: 'features/**/*.feature', steps: ['steps/**/*.ts', 'support/fixtures.ts'] }

export default defineConfig({
  testDir: './specs',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: { baseURL, trace: 'retain-on-failure' },
  projects: [
    { name: 'desktop', use: desktop },
    { name: 'mobile-360', use: mobile },
    { ...defineBddProject({ name: 'journeys-desktop', ...journeys }), use: desktop },
    { ...defineBddProject({ name: 'journeys-mobile-360', ...journeys }), use: mobile },
  ],
  webServer: {
    command: 'dotnet run --project ../../apphost/HiveSpace.AppHost --launch-profile http',
    url: `${baseURL}/health`,
    reuseExistingServer: true,
    timeout: 10 * 60_000,
    stdout: 'ignore',
  },
})
