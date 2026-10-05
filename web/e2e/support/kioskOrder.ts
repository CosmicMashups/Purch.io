import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';

interface KioskOrderOptions {
  orderType?: 'Dine In' | 'Take Out';
  /** The name of the payment tile, as the customer reads it. */
  payment?: RegExp;
}

/**
 * Walks the kiosk the way a customer does: tap to begin, open the item, add it, complete the order, say where they will
 * eat and how they will pay, and wait for the order number. Returns that number.
 */
export async function placeKioskOrder(kiosk: Page, item: RegExp, { orderType = 'Take Out', payment = /^Cash/ }: KioskOrderOptions = {}): Promise<number> {
  await kiosk.goto('/kiosk');
  await kiosk.getByRole('link', { name: /Tap anywhere to begin/ }).click();

  await kiosk.getByRole('button', { name: item }).click();
  await kiosk.getByRole('button', { name: 'Add to order' }).click();
  // The confirmation plays, then the menu comes back with the order in the bar.
  await kiosk.getByRole('link', { name: 'Complete Order' }).click({ timeout: 15_000 });

  await expect(kiosk.getByRole('heading', { name: 'Your order' })).toBeVisible();
  await kiosk.getByRole('link', { name: 'Proceed to payment' }).click();
  await kiosk.getByRole('button', { name: new RegExp(orderType) }).click();
  await kiosk.getByRole('button', { name: payment }).click();
  await kiosk.getByRole('button', { name: 'Place order' }).click();

  const number = kiosk.getByLabel(/Order number \d+/);
  await expect(number).toBeVisible({ timeout: 20_000 });
  const label = (await number.getAttribute('aria-label')) ?? '';
  return Number(label.replace('Order number ', ''));
}
