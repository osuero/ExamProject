import { defineConfig, devices } from '@playwright/test';

// E2E runs against the dev stack: API (Development, local mailbox) on :5080 and Angular dev server on :4200.
// CHROMIUM_PATH lets CI or this sandbox use a preinstalled browser instead of downloading one.
const executablePath = process.env['CHROMIUM_PATH'] || undefined;

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 90_000,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }], ['junit', { outputFile: 'test-results/e2e-junit.xml' }]],
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    launchOptions: { executablePath },
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], launchOptions: { executablePath } }, grepInvert: /@mobile/ },
    { name: 'mobile', use: { ...devices['Pixel 7'], launchOptions: { executablePath } }, grep: /@mobile/ },
  ],
});
