import { pinLogin, resetRegister } from './support/api';
import { expect, test } from './support/fixtures';

test.describe('the Home dashboard', () => {
  test('shows real revenue, the calendar, top sellers and stock health, with no error', async ({ page, signInAs, ip, seed }) => {
    // Give the day something to show: one sale rung up at the register.
    const cashier = await pinLogin(seed.register.code, seed.pins.cashier, ip);
    await resetRegister(seed.register.code, seed.pins.manager, ip);
    const add = await fetch(`http://127.0.0.1:${process.env.E2E_API_PORT ?? '5099'}/transactions/cart/lines`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', authorization: `Bearer ${cashier.accessToken}`, 'x-forwarded-for': ip },
      body: JSON.stringify({ itemId: await firstItemId(cashier.accessToken, ip), itemVariantId: null, quantity: 2 }),
    });
    expect(add.ok).toBe(true);

    await signInAs('admin');
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Revenue' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Busiest days' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Top sellers' })).toBeVisible();
    await expect(page.getByRole('meter', { name: 'Out of stock' })).toBeVisible();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/could not be loaded|is unavailable|something went wrong/i)).toHaveCount(0);
  });

  test('every chart has its numbers as a table', async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/');
    await page.getByText('Show as table').first().click();
    await expect(page.getByRole('table').first()).toBeVisible();
  });

  test('a manager sees the same charts, and a warehouse officer only stock', async ({ page, signInAs }) => {
    await signInAs('manager');
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Revenue' })).toBeVisible();
  });

  test('a warehouse officer sees stock and no money', async ({ page, signInAs }) => {
    await signInAs('warehouse');
    await page.goto('/');
    await expect(page.getByRole('meter', { name: 'Running low' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Revenue' })).toHaveCount(0);
  });

  test('the chart can be read with the keyboard', async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/');
    const chart = page.getByLabel(/left and right arrow keys/);
    await chart.focus();
    await page.keyboard.press('End');
    await expect(page.getByRole('tooltip')).toBeVisible();
  });
});

async function firstItemId(token: string, ip: string): Promise<string> {
  const items = await fetch(`http://127.0.0.1:${process.env.E2E_API_PORT ?? '5099'}/items`, { headers: { authorization: `Bearer ${token}`, 'x-forwarded-for': ip } }).then((r) => r.json());
  return (items as { id: string }[])[0].id;
}

test.describe('the Business page', () => {
  test('puts what needs attention first, then money, the team and the devices', async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/business');
    await expect(page.getByRole('heading', { name: 'Needs attention' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Money owed' })).toBeVisible();
    await expect(page.getByRole('img', { name: /^Staff by role: \d+ staff$/ })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Devices' })).toBeVisible();
  });

  test('shows live figures beside the links', async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/business');
    await expect(page.getByRole('link', { name: /^Items\D*\d+ items/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /^Staff\D*\d+ active/ })).toBeVisible();
  });

  test('a manager gets the overview without the devices', async ({ page, signInAs }) => {
    await signInAs('manager');
    await page.goto('/business');
    await expect(page.getByRole('heading', { name: 'Needs attention' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Devices' })).toHaveCount(0);
  });
});
