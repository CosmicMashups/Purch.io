import { resetRegister } from './support/api';
import { expect, test } from './support/fixtures';

test.describe('the register', () => {
  test('rings up two items and takes cash, showing the change the server worked out', async ({ page, signInAs, ip, seed }) => {
    await signInAs('cashier');
    await resetRegister(seed.register.code, seed.pins.manager, ip);
    await page.goto('/sell');

    await page.getByRole('button', { name: /Iced Latte/ }).click();
    await expect(page.getByText('₱150.00').first()).toBeVisible();
    await page.getByRole('button', { name: /Mocha/ }).click();
    await expect(page.getByText('₱320.00').first()).toBeVisible();

    await page.getByRole('button', { name: /Charge/ }).click();
    await expect(page).toHaveURL(/\/sell\/payment$/);
    await page.getByLabel('Cash received').fill('500');
    await page.getByRole('button', { name: /^Confirm ₱320.00/ }).click();

    await expect(page).toHaveURL(/\/sell\/receipt$/);
    await expect(page.getByRole('article', { name: 'Receipt' })).toContainText('₱320.00');
    await expect(page.getByRole('article', { name: 'Receipt' })).toContainText('₱180.00');
  });

  test('a scanned barcode adds the item without touching the screen', async ({ page, signInAs, ip, seed }) => {
    await signInAs('cashier');
    await resetRegister(seed.register.code, seed.pins.manager, ip);
    await page.goto('/sell');
    await expect(page.getByRole('button', { name: /Ube Cookie/ })).toBeVisible();

    await page.locator('body').click({ position: { x: 5, y: 5 } });
    await page.keyboard.type(seed.items['Ube Cookie'].barcode, { delay: 5 });
    await page.keyboard.press('Enter');

    await expect(page.getByText('₱60.00').first()).toBeVisible();
  });

  test('an admin who signed in with email is told selling needs a device sign-in', async ({ page, seed }) => {
    await page.goto('/login');
    await page.getByRole('button', { name: 'Admin', exact: true }).click();
    await page.getByLabel(/email/i).fill(seed.owner.email);
    await page.getByLabel(/password/i).fill(seed.owner.password);
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page.getByText('Signed in as Admin')).toBeVisible();
    await page.goto('/sell');
    await expect(page.getByText('Sign in with a device to sell')).toBeVisible();
  });
});

// Known problem, tracked in docs/REACT-MIGRATION.md: the item grid is disabled while an add is on its way to the
// server, so a fast cashier's second tap is lost. Remove `fixme` when adds no longer block each other.
test.fixme('two items tapped back to back both reach the cart', async ({ page, signInAs, ip, seed }) => {
  await signInAs('cashier');
  await resetRegister(seed.register.code, seed.pins.manager, ip);
  await page.goto('/sell');
  await expect(page.getByRole('button', { name: /Iced Latte/ })).toBeVisible();

  await page.getByRole('button', { name: /Iced Latte/ }).click({ noWaitAfter: true });
  await page.getByRole('button', { name: /Mocha/ }).click({ force: true, noWaitAfter: true });

  await expect(page.getByText('₱320.00').first()).toBeVisible();
});
