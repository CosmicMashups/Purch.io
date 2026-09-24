import { resetRegister } from './support/api';
import { expect, test } from './support/fixtures';

test.describe('at the counter', () => {
  test('a cashier takes a kiosk order and collects payment for it', async ({ page, actor, signInAs, ip, seed }) => {
    // The customer orders at the kiosk.
    const kiosk = await actor('kiosk');
    await kiosk.goto('/kiosk');
    await kiosk.getByRole('link', { name: 'Start your order' }).click();
    await kiosk.getByRole('button', { name: /Ube Cookie/ }).click();
    await kiosk.getByRole('link', { name: /View your order \(1\)/ }).click();
    await kiosk.getByRole('link', { name: 'Continue' }).click();
    await kiosk.getByRole('button', { name: /Dine In/ }).click();
    const label = (await kiosk.getByLabel(/Order number \d+/).getAttribute('aria-label')) ?? '';
    const number = label.replace('Order number ', '');

    // The cashier takes it and is paid.
    await signInAs('cashier');
    await resetRegister(seed.register.code, seed.pins.manager, ip, { removeCart: true });
    await page.goto('/sell/kiosk-orders');
    const order = page.getByRole('listitem').filter({ hasText: `Order ${number}` });
    await order.getByRole('button', { name: 'Take this order' }).click();

    await expect(page).toHaveURL(/\/sell$/);
    await expect(page.getByText('Ube Cookie').first()).toBeVisible();
    await page.getByRole('button', { name: /Charge/ }).click();
    await page.getByLabel('Cash received').fill('100');
    await page.getByRole('button', { name: /^Confirm ₱60\.00/ }).click();

    await expect(page.getByRole('article', { name: 'Receipt' })).toContainText('₱40.00');
  });

  test('a cashier opens a shift with a cash count and closes it with a matching count', async ({ page, signInAs, ip, seed }) => {
    await signInAs('manager');
    await resetRegister(seed.register.code, seed.pins.manager, ip);
    await page.goto('/sell/shift');

    // A shift left open by an earlier run is closed first, so the test starts from a closed drawer.
    const closeForm = page.getByLabel('Closing cash count (PHP)');
    if (await closeForm.isVisible().catch(() => false)) {
      await closeForm.fill('0');
      await page.getByLabel('Manager or admin PIN').fill(seed.pins.manager);
      await page.getByRole('button', { name: 'Close shift' }).click();
      await page.getByRole('dialog').getByRole('button', { name: 'Close shift' }).click();
      await expect(page.getByRole('heading', { name: 'Shift closed' })).toBeVisible();
      await page.goto('/sell/shift');
    }

    await page.getByLabel('Opening cash count (PHP)').fill('1000');
    await page.getByRole('button', { name: 'Open shift' }).click();
    await expect(page.getByRole('heading', { name: 'Shift open' })).toBeVisible();

    await page.getByLabel('Closing cash count (PHP)').fill('1000');
    await page.getByRole('button', { name: 'Close shift' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Close shift' }).click();

    const summary = page.getByRole('region', { name: 'Shift summary' });
    await expect(summary).toContainText('Shift closed');
    await expect(summary).toContainText('₱1,000.00');
  });
});

// Known problem (G15 in docs/REACT-MIGRATION.md): the server refuses to hand a kiosk order to a till that already
// has an open cart, and merely opening Sell creates one. Only a manager can clear it. Remove `fixme` once fixed.
test.fixme('a cashier who has looked at an empty register can still take a kiosk order', async ({ page, actor, signInAs, ip, seed }) => {
  const kiosk = await actor('kiosk');
  await kiosk.goto('/kiosk');
  await kiosk.getByRole('link', { name: 'Start your order' }).click();
  await kiosk.getByRole('button', { name: /Ube Cookie/ }).click();
  await kiosk.getByRole('link', { name: /View your order \(1\)/ }).click();
  await kiosk.getByRole('link', { name: 'Continue' }).click();
  await kiosk.getByRole('button', { name: /Dine In/ }).click();
  await expect(kiosk.getByLabel(/Order number \d+/)).toBeVisible();

  await signInAs('cashier');
  await resetRegister(seed.register.code, seed.pins.manager, ip, { removeCart: true });
  await page.goto('/sell');
  await expect(page.getByRole('button', { name: /Iced Latte/ })).toBeVisible();
  await page.goto('/sell/kiosk-orders');
  await page.getByRole('button', { name: 'Take this order' }).first().click();
  await expect(page).toHaveURL(/\/sell$/);
});

test.describe('the home dashboard', () => {
  test('loads for an admin with real figures and no error', async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/');
    await expect(page.getByText('Signed in as Admin')).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/could not be loaded|something went wrong/i)).toHaveCount(0);
  });
});
