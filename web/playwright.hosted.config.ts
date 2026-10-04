import { existsSync } from 'node:fs';
import { defineConfig } from '@playwright/test';

if (existsSync('e2e/.env.debug')) process.loadEnvFile('e2e/.env.debug');

const WEB_PORT = process.env.E2E_WEB_PORT ?? '5181';

/**
 * Read-only checks of the hosted deployment as the enrolled debug admins (credentials in the git-ignored
 * `e2e/.env.debug`). No local backend: the dev server proxies /api to the hosted API unless VITE_DEV_PROXY_TARGET is set.
 * These tests only sign in and look; they must not create or change data.
 */
export default defineConfig({
  testDir: './e2e/hosted',
  testMatch: '**/*.hosted.ts',
  workers: 1,
  fullyParallel: false,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  reporter: [['list']],
  use: {
    baseURL: `http://127.0.0.1:${WEB_PORT}`,
    channel: process.env.PW_CHANNEL || undefined,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: {
    command: `npm run dev -- --host 127.0.0.1 --port ${WEB_PORT} --strictPort`,
    url: `http://127.0.0.1:${WEB_PORT}`,
    timeout: 120_000,
    reuseExistingServer: !process.env.CI,
  },
});
