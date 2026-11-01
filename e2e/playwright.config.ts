
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://127.0.0.1:5055',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'ASPNETCORE_URLS=http://127.0.0.1:5055 dotnet run --no-launch-profile --project ../backend/src/InvoiceApp.Api',
    url: 'http://127.0.0.1:5055',
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
