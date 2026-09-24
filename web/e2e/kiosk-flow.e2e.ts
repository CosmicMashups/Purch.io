import type { Page } from '@playwright/test';
import { expect, test } from './support/fixtures';

/** Orders a Latte at the kiosk and returns the order number the customer is given. */
async function orderAtKiosk(kiosk: Page): Promise<number> {
  await kiosk.goto('/kiosk');
  await kiosk.getByRole('link', { name: 'Start your order' }).click();
  await kiosk.getByRole('button', { name: /Iced Latte/ }).click();
  await kiosk.getByRole('link', { name: /View your order \(1\)/ }).click();
  await expect(kiosk.getByText('Iced Latte').first()).toBeVisible();
  await kiosk.getByRole('link', { name: 'Continue' }).click();
  await kiosk.getByRole('button', { name: /Take Out/ }).click();
  const number = kiosk.getByLabel(/Order number \d+/);
  await expect(number).toBeVisible();
  const label = (await number.getAttribute('aria-label')) ?? '';
  return Number(label.replace('Order number ', ''));
}

test.describe('an order from the kiosk to the kitchen, the board and the counter', () => {
  test('the customer orders, the kitchen prepares it, and the board calls the number', async ({ actor }) => {
    const kiosk = await actor('kiosk');
    const kitchen = await actor('kitchen');
    const board = await actor('orderBoard');

    const number = await orderAtKiosk(kiosk);
    expect(number).toBeGreaterThan(0);

    await kitchen.goto('/kitchen');
    const ticket = kitchen.getByRole('listitem').filter({ hasText: `#${number}` }).filter({ has: kitchen.getByRole('button') });
    await expect(ticket).toContainText('1 × Iced Latte');
    await expect(ticket).toContainText('Take Out');

    await board.goto('/order-board');
    await expect(board.getByRole('region', { name: 'Preparing' }).getByText(String(number), { exact: true })).toBeVisible({ timeout: 15_000 });

    await ticket.getByRole('button', { name: 'Start preparing' }).click();
    await expect(ticket.getByRole('button', { name: 'Mark ready' })).toBeVisible();
    await ticket.getByRole('button', { name: 'Mark ready' }).click();

    // The board polls every few seconds.
    await expect(board.getByRole('region', { name: 'Ready for pickup' }).getByText(String(number), { exact: true })).toBeVisible({ timeout: 20_000 });
    await expect(board.getByText(/Iced Latte/)).toHaveCount(0);

    await ticket.getByRole('button', { name: 'Picked up' }).click();
    await expect(board.getByText(String(number), { exact: true })).toHaveCount(0, { timeout: 20_000 });
  });

  test('after the order the kiosk is ready for the next customer with an empty order', async ({ actor }) => {
    const kiosk = await actor('kiosk');
    await orderAtKiosk(kiosk);
    await kiosk.getByRole('button', { name: 'Start a new order' }).click();
    await expect(kiosk).toHaveURL(/\/kiosk$/);
    await kiosk.getByRole('link', { name: 'Start your order' }).click();
    await expect(kiosk.getByRole('link', { name: /View your order \(0\)/ })).toBeVisible();
  });

  test('a device screen never shows another kind of device its data', async ({ actor }) => {
    const board = await actor('orderBoard');
    await board.goto('/kitchen');
    await expect(board.getByRole('heading', { name: 'This browser is set up for something else' })).toBeVisible();
  });
});
