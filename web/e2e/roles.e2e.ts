import type { Page } from '@playwright/test';
import { expect, test } from './support/fixtures';

const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });

test.describe('what each role can reach', () => {
  test('an admin sees every tab', async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/');
    for (const tab of ['Home', 'Cashier', 'Inventory', 'Business']) await expect(nav(page).getByRole('link', { name: tab })).toBeVisible();
  });

  test('a manager sees every tab but not Devices or Settings', async ({ page, signInAs }) => {
    await signInAs('manager');
    await page.goto('/business');
    for (const tab of ['Home', 'Cashier', 'Inventory', 'Business']) await expect(nav(page).getByRole('link', { name: tab })).toBeVisible();
    await expect(page.getByRole('link', { name: /^Devices/ })).toHaveCount(0);
    await expect(page.getByRole('link', { name: /^Settings/ })).toHaveCount(0);
  });

  test('a cashier sees Home and Cashier only, and is turned away from Business', async ({ page, signInAs }) => {
    await signInAs('cashier');
    await page.goto('/');
    await expect(nav(page).getByRole('link', { name: 'Cashier' })).toBeVisible();
    await expect(nav(page).getByRole('link', { name: 'Business' })).toHaveCount(0);
    await expect(nav(page).getByRole('link', { name: 'Inventory' })).toHaveCount(0);
    await page.goto('/business');
    await expect(page).not.toHaveURL(/\/business/);
  });

  test('a warehouse officer sees Home and Inventory only, and is turned away from Cashier', async ({ page, signInAs }) => {
    await signInAs('warehouse');
    await page.goto('/');
    await expect(nav(page).getByRole('link', { name: 'Inventory' })).toBeVisible();
    await expect(nav(page).getByRole('link', { name: 'Cashier' })).toHaveCount(0);
    await page.goto('/sell');
    await expect(page).not.toHaveURL(/\/sell/);
  });

  test('only an admin can open device and settings pages by address', async ({ page, signInAs }) => {
    await signInAs('manager');
    await page.goto('/business/devices');
    await expect(page).toHaveURL(/\/business$/);
  });
});

test.describe('device sessions stay apart from staff', () => {
  test('a kiosk session cannot enter the staff shell', async ({ page, pairDevice }) => {
    await pairDevice('kiosk');
    await page.goto('/');
    await expect(page).toHaveURL(/\/kiosk$/);
    await expect(page.getByRole('heading', { name: 'Welcome!' })).toBeVisible();
  });

  test('an unpaired browser is sent to pair the kiosk', async ({ page }) => {
    await page.goto('/kiosk');
    await expect(page).toHaveURL(/\/kiosk\/pair$/);
  });

  test('a staff session on a device screen is told it is set up for something else', async ({ page, signInAs }) => {
    await signInAs('cashier');
    await page.goto('/kiosk');
    await expect(page.getByRole('heading', { name: 'This browser is set up for something else' })).toBeVisible();
  });
});
