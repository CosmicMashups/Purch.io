import { defineConfig } from '@playwright/test';

const API_PORT = process.env.E2E_API_PORT ?? '5099';
const WEB_PORT = process.env.E2E_WEB_PORT ?? '5180';

/**
 * End-to-end tests run in a real browser against the real backend and a throwaway Postgres.
 * `e2e/stack/run-backend.mjs` starts the database and the API; the web dev server proxies /api to it.
 * Set PW_CHANNEL=chrome (or msedge) to use an installed browser instead of Playwright's own download.
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  globalSetup: './e2e/global-setup.ts',
  // Every device owns one open cart on the server, so tests that share a till must not overlap.
  workers: 1,
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  timeout: 45_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: `http://127.0.0.1:${WEB_PORT}`,
    channel: process.env.PW_CHANNEL || undefined,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: [
    {
      command: 'node e2e/stack/run-backend.mjs',
      url: `http://127.0.0.1:${API_PORT}/health/ready`,
      timeout: 300_000,
      reuseExistingServer: !process.env.CI,
    },
    {
      command: `npm run dev -- --host 127.0.0.1 --port ${WEB_PORT} --strictPort`,
      url: `http://127.0.0.1:${WEB_PORT}`,
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: { VITE_DEV_PROXY_TARGET: `http://127.0.0.1:${API_PORT}` },
    },
  ],
});
