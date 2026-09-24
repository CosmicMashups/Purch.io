import type { Page } from '@playwright/test';
import { call } from './support/api';
import { expect, test } from './support/fixtures';

/** Rows in the saved catalog cache, read straight from the browser's IndexedDB. */
async function savedRows(page: Page): Promise<number> {
  return page.evaluate(
    () =>
      new Promise<number>((resolve) => {
        const open = indexedDB.open('purch-offline');
        open.onerror = () => resolve(0);
        open.onsuccess = () => {
          const db = open.result;
          if (!db.objectStoreNames.contains('cache')) return resolve(0);
          const count = db.transaction('cache').objectStore('cache').count();
          count.onsuccess = () => resolve(count.result);
          count.onerror = () => resolve(0);
        };
      }),
  );
}

/** The browser reports offline and every API call fails, as with no connection. */
async function goOffline(page: Page) {
  await page.addInitScript(() => Object.defineProperty(navigator, 'onLine', { get: () => false }));
  await page.route('**/api/**', (route) => route.abort('internetdisconnected'));
}

test.describe('offline browsing', () => {
  test('the last loaded catalog can still be browsed with no connection, and says how old it is', async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/catalog/items');
    await expect(page.getByText('Iced Latte')).toBeVisible();
    await expect.poll(() => savedRows(page), { timeout: 15_000 }).toBeGreaterThan(0);

    await goOffline(page);
    await page.reload();

    await expect(page.getByText('Iced Latte')).toBeVisible();
    await expect(page.getByText(/Showing items saved on/)).toBeVisible();
    await expect(page.getByText(/You are offline/)).toBeVisible();
  });

  test('selling is paused while offline, with a reason', async ({ page, signInAs }) => {
    await signInAs('cashier');
    await goOffline(page);
    await page.goto('/sell');
    await expect(page.getByText('Selling needs a connection')).toBeVisible();
  });

  test('signing out wipes the saved catalog', async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/catalog/items');
    await expect(page.getByText('Iced Latte')).toBeVisible();
    await expect.poll(() => savedRows(page), { timeout: 15_000 }).toBeGreaterThan(0);

    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page).toHaveURL(/\/login$/);
    await expect.poll(() => savedRows(page), { timeout: 10_000 }).toBe(0);
  });

  test("one business's saved catalog is never shown to another", async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/catalog/items');
    await expect(page.getByText('Iced Latte')).toBeVisible();
    await expect.poll(() => savedRows(page), { timeout: 15_000 }).toBeGreaterThan(0);

    // A different business signs in on this same browser without the first one signing out.
    const stamp = Date.now().toString(36);
    const other = await call<{ devicePairingCode: string }>('POST', '/onboarding/bootstrap', {
      body: { tenantName: `Other Shop ${stamp}`, businessType: 0, branchName: 'Main', adminName: 'Other Owner', adminPin: '1234', adminEmail: null, adminPassword: null },
    });
    const tokens = await call<{ accessToken: string; refreshToken: string }>('POST', '/auth/login', { body: { devicePairingCode: other.devicePairingCode, pin: '1234' } });
    await page.evaluate((t) => {
      localStorage.setItem('purch.accessToken', t.accessToken);
      localStorage.setItem('purch.refreshToken', t.refreshToken);
    }, tokens);

    await goOffline(page);
    await page.reload();
    await expect(page.getByText('Iced Latte')).toHaveCount(0);
  });
});
