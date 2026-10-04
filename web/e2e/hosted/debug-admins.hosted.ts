import { expect, test } from '@playwright/test';

const password = process.env.DEBUG_ADMIN_PASSWORD;
const accounts = [
  { label: 'Debug Admin (Login Debug Test 2)', email: process.env.DEBUG_ADMIN2_EMAIL },
  { label: 'Debug Admin (Login Debug Test 3)', email: process.env.DEBUG_ADMIN3_EMAIL },
];

test.skip(!password || accounts.some((a) => !a.email), 'Debug admin credentials are not set (see e2e/.env.debug).');

for (const { label, email } of accounts) {
  test(`${label} signs in with email and password`, async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Email').fill(email!);
    await page.getByLabel('Password', { exact: true }).fill(password!);
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page.getByText('Signed in as Admin')).toBeVisible();
  });
}

test('a wrong password is refused', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Email').fill(accounts[0]!.email!);
  await page.getByLabel('Password', { exact: true }).fill(`${password}-wrong`);
  await page.getByRole('button', { name: /sign in/i }).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});
