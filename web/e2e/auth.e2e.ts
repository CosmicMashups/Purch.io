import { expect, test } from './support/fixtures';

test.describe('signing in', () => {
  test('a cashier signs in with the register code and PIN, then signs out', async ({ page, seed }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/\/login$/);

    await page.getByLabel(/device code/i).fill(seed.register.code);
    await page.getByLabel(/pin/i).fill(seed.pins.cashier);
    await page.getByRole('button', { name: /sign in/i }).click();

    await expect(page.getByText('Signed in as Cashier')).toBeVisible();
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page).toHaveURL(/\/login$/);
    await expect.poll(() => page.evaluate(() => localStorage.getItem('purch.accessToken'))).toBeNull();
  });

  test('a wrong PIN gets a plain message and stays on the sign-in screen', async ({ page, seed }) => {
    await page.goto('/login');
    await page.getByLabel(/device code/i).fill(seed.register.code);
    await page.getByLabel(/pin/i).fill('9999');
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page.getByRole('alert')).toContainText(/incorrect/i);
    await expect(page).toHaveURL(/\/login$/);
  });

  test('the owner signs in with email and password', async ({ page, seed }) => {
    await page.goto('/login');
    await page.getByRole('button', { name: 'Admin', exact: true }).click();
    await page.getByLabel(/email/i).fill(seed.owner.email);
    await page.getByLabel(/password/i).fill(seed.owner.password);
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page.getByText('Signed in as Admin')).toBeVisible();
  });

  test('a signed-out visitor is sent to sign in from any page', async ({ page }) => {
    await page.goto('/business/staff');
    await expect(page).toHaveURL(/\/login$/);
  });
});
