import { defineConfig, devices } from '@playwright/test'

// Drives the real local stack through the gateway (one origin, ADR-0008).
// If nothing answers on BASE_URL, Playwright starts the AppHost itself and waits for the gateway.
const baseURL = process.env.BASE_URL ?? 'http://localhost:5080'

export default defineConfig({
  testDir: './specs',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: { baseURL, trace: 'retain-on-failure' },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    // EIR-UI-003: the UI works at 360 px.
    { name: 'mobile-360', use: { ...devices['Desktop Chrome'], viewport: { width: 360, height: 780 } } },
  ],
  webServer: {
    command: 'dotnet run --project ../../apphost/HiveSpace.AppHost --launch-profile http',
    url: `${baseURL}/health`,
    reuseExistingServer: true,
    timeout: 10 * 60_000,
    stdout: 'ignore',
  },
})
