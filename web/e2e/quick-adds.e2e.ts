import { resetRegister } from './support/api';
import { expect, test } from './support/fixtures';

// A cashier taps as fast as a customer's basket needs. The register must accept every tap at once, however
// long the server takes, and end up with exactly what was tapped.
test.describe('adding items to the cart quickly', () => {
  test('two different items tapped back to back both reach the cart', async ({ page, signInAs, ip, seed }) => {
    await signInAs('cashier');
    await resetRegister(seed.register.code, seed.pins.manager, ip);
    await page.goto('/sell');
    await expect(page.getByRole('button', { name: /^Iced Latte/ })).toBeVisible();

    await page.getByRole('button', { name: /^Iced Latte/ }).click({ noWaitAfter: true });
    await page.getByRole('button', { name: /^Mocha/ }).click({ noWaitAfter: true });

    await expect(page.getByText('₱320.00').first()).toBeVisible();
    await expect(page.getByRole('button', { name: /Charge ₱320\.00/ })).toBeEnabled();
  });

  test('every tap is accepted on the spot even when the server is slow, and the cart ends up right', async ({ page, signInAs, ip, seed }) => {
    await signInAs('cashier');
    await resetRegister(seed.register.code, seed.pins.manager, ip);
    await page.goto('/sell');
    await expect(page.getByRole('button', { name: /^Iced Latte/ })).toBeVisible();

    // The server takes a full second and a half to answer each add.
    let sent = 0;
    await page.route('**/api/transactions/cart/lines', async (route) => {
      if (route.request().method() === 'POST') {
        sent += 1;
        await new Promise((r) => setTimeout(r, 1500));
      }
      await route.fallback();
    });

    const latte = page.getByRole('button', { name: /^Iced Latte/ });
    const mocha = page.getByRole('button', { name: /^Mocha/ });
    const cookie = page.getByRole('button', { name: /^Ube Cookie/ });
    const started = Date.now();
    await latte.click({ noWaitAfter: true });
    await latte.click({ noWaitAfter: true });
    await mocha.click({ noWaitAfter: true });
    await latte.click({ noWaitAfter: true });
    await cookie.click({ noWaitAfter: true });
    const tapping = Date.now() - started;

    // All five taps were taken long before the first answer could have arrived, and are shown at once.
    expect(tapping).toBeLessThan(1500);
    await expect(page.getByLabel('Adding Iced Latte')).toContainText('x 3', { timeout: 1000 });
    await expect(page.getByLabel('Adding Mocha')).toBeVisible();
    await expect(page.getByLabel('Adding Ube Cookie')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Updating...' })).toBeDisabled();

    // 3 lattes, a mocha and a cookie: 450 + 170 + 60.
    await expect(page.getByRole('button', { name: /Charge ₱680\.00/ })).toBeEnabled({ timeout: 15_000 });
    await expect(page.getByLabel(/^Adding /)).toHaveCount(0);
    // Repeated taps on the latte went out together instead of one request each.
    expect(sent).toBeLessThan(5);
  });

  test('a rapid burst of taps on one item comes out as the right quantity', async ({ page, signInAs, ip, seed }) => {
    await signInAs('cashier');
    await resetRegister(seed.register.code, seed.pins.manager, ip);
    await page.goto('/sell');
    const cookie = page.getByRole('button', { name: /^Ube Cookie/ });
    await expect(cookie).toBeVisible();

    for (let i = 0; i < 7; i += 1) await cookie.click({ noWaitAfter: true });

    await expect(page.getByRole('button', { name: /Charge ₱420\.00/ })).toBeEnabled({ timeout: 15_000 });
  });
});
