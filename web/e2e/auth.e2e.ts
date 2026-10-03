import type { Page } from '@playwright/test';
import { expect, test } from './support/fixtures';

/** Makes this browser a paired till, the way pairing it with its one-time code would have. */
async function pairedTill(page: Page, credential: string) {
  await page.addInitScript((value) => localStorage.setItem('purch.deviceCredential', value), credential);
}

async function typePin(page: Page, pin: string) {
  for (const digit of pin) await page.getByRole('button', { name: digit, exact: true }).click();
}

test.describe('signing in', () => {
  test('a cashier unlocks the till with their own PIN, then locks it again', async ({ page, seed }) => {
    await pairedTill(page, seed.register.credential);
    await page.goto('/');
    await expect(page).toHaveURL(/\/unlock$/);

    await page.getByRole('button', { name: 'Carlo Cashier' }).click();
    await typePin(page, seed.pins.cashier);
    await page.getByRole('button', { name: 'Unlock' }).click();

    await expect(page).toHaveURL(/\/sell$/);
    await page.getByRole('button', { name: 'Lock' }).click();
    await expect(page).toHaveURL(/\/unlock$/);
    await expect.poll(() => page.evaluate(() => localStorage.getItem('purch.accessToken'))).toBeNull();
  });

  test('a wrong PIN says how many tries are left and stays on the lock screen', async ({ page, seed }) => {
    await pairedTill(page, seed.register.credential);
    await page.goto('/unlock');
    await page.getByRole('button', { name: 'Carlo Cashier' }).click();
    await typePin(page, '9999');
    await page.getByRole('button', { name: 'Unlock' }).click();
    await expect(page.getByRole('alert')).toContainText(/tries left/i);
    await expect(page).toHaveURL(/\/unlock$/);
  });

  test('the owner signs in with email and password', async ({ page, seed }) => {
    await page.goto('/login');
    await page.getByLabel('Email').fill(seed.owner.email);
    await page.getByLabel('Password', { exact: true }).fill(seed.owner.password);
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page.getByText('Signed in as Admin')).toBeVisible();
  });

  test('a signed-out visitor is sent to sign in from any page', async ({ page }) => {
    await page.goto('/business/staff');
    await expect(page).toHaveURL(/\/login$/);
  });
});
