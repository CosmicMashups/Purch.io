import { mkdirSync } from 'node:fs';
import path from 'node:path';
import type { Page } from '@playwright/test';
import { call, signIn } from './support/api';
import { expect, test } from './support/fixtures';

/** Set KIOSK_SHOTS to a folder to also save a screenshot of every screen the customer sees, for reviewing the design. */
const SHOTS = process.env.KIOSK_SHOTS;
async function shot(page: Page, name: string): Promise<void> {
  if (!SHOTS) return;
  mkdirSync(SHOTS, { recursive: true });
  await page.screenshot({ path: path.join(SHOTS, `${name}.png`) });
}

interface Created {
  id: string;
}

test.describe('customising an order at the kiosk', () => {
  test('a customer builds a meal with sides and a drink, in portrait and in landscape, and the kitchen sees the choices', async ({ actor, seed }) => {
    // The menu: a meal with an optional sides group and an optional drinks group, plus a deal with a fixed part.
    const { accessToken } = await signIn(seed.owner.email, seed.owner.password);
    const api = <T>(method: string, route: string, body?: unknown) => call<T>(method, route, { token: accessToken, body });
    const stamp = Date.now().toString(36);

    const meals = await api<Created>('POST', '/categories', { name: 'Meals', sortOrder: 3, imageUrl: null });
    const promos = await api<Created>('POST', '/categories', { name: 'Promos', sortOrder: 4, imageUrl: null });
    const stock = (itemId: string) => api('POST', '/inventory/movements', { itemId, branchId: seed.branchId, type: 0, quantity: 100, note: 'e2e seed', reasonCategory: null, photoUrl: null, supplierReference: null });

    const chicken = await api<Created>('POST', '/items', { name: `Chicken Meal ${stamp}`, sku: null, barcode: null, categoryId: meals.id, basePrice: 99, imageUrl: null, pricingType: 0 });
    await stock(chicken.id);
    const sides = await api<Created>('POST', '/modifier-groups', { name: 'Add fries & sides', allowMultipleSelection: true, isRequired: false });
    await api('POST', `/modifier-groups/${sides.id}/modifiers`, { name: 'Fries', priceDelta: 40 });
    await api('POST', `/modifier-groups/${sides.id}/modifiers`, { name: 'Onion rings', priceDelta: 45 });
    const drinks = await api<Created>('POST', '/modifier-groups', { name: 'Add drinks', allowMultipleSelection: false, isRequired: false });
    await api('POST', `/modifier-groups/${drinks.id}/modifiers`, { name: 'Coke Zero', priceDelta: 25 });
    await api('POST', `/modifier-groups/${drinks.id}/modifiers`, { name: 'Royal', priceDelta: 25 });
    await api('POST', `/items/${chicken.id}/modifier-groups`, { modifierGroupId: sides.id });
    await api('POST', `/items/${chicken.id}/modifier-groups`, { modifierGroupId: drinks.id });

    const deal = await api<Created>('POST', '/items', { name: `Chicken Buy 1 Take 1 ${stamp}`, sku: null, barcode: null, categoryId: promos.id, basePrice: 99, imageUrl: null, pricingType: 4 });
    await api('POST', `/items/${deal.id}/combo-components`, { componentCategoryId: '00000000-0000-0000-0000-000000000000', slotLabel: '2 pcs Fried Chicken', quantity: 2, substitutionUpchargeAmount: null, componentItemId: chicken.id });

    const kiosk = await actor('kiosk');
    const kitchen = await actor('kitchen');

    // ---- Portrait -------------------------------------------------------------------------------------
    await kiosk.setViewportSize({ width: 1080, height: 1920 });
    await kiosk.goto('/kiosk');
    await expect(kiosk.getByRole('link', { name: /Tap anywhere to begin/ })).toBeVisible();
    await shot(kiosk, 'portrait-1-welcome');
    await kiosk.getByRole('link', { name: /Tap anywhere to begin/ }).click();

    // One scroll with every category in it, a rail on the left and no "All".
    const rail = kiosk.getByRole('navigation', { name: 'Categories' });
    await expect(rail.getByRole('button', { name: 'Meals' })).toBeVisible();
    await expect(rail.getByRole('button', { name: 'All' })).toHaveCount(0);
    await expect(kiosk.getByRole('heading', { level: 2, name: 'Promos' })).toBeAttached();
    await shot(kiosk, 'portrait-2-menu');

    // Tapping a category scrolls to it and the rail follows.
    await rail.getByRole('button', { name: 'Promos' }).click();
    await expect(rail.getByRole('button', { name: 'Promos' })).toHaveAttribute('aria-pressed', 'true');
    await shot(kiosk, 'portrait-3-menu-promos');

    await kiosk.getByRole('button', { name: new RegExp(`Chicken Meal ${stamp}`) }).click();
    await expect(kiosk.getByRole('heading', { name: `Chicken Meal ${stamp}` })).toBeVisible();
    await expect(kiosk.getByRole('tab')).toHaveCount(3);
    await shot(kiosk, 'portrait-4-item-quantity');

    // Every group needs an explicit choice, so adding is held until both are decided.
    await expect(kiosk.getByRole('button', { name: 'Add to order' })).toBeDisabled();
    await kiosk.getByRole('tab', { name: /Add fries & sides/ }).click();
    await kiosk.getByRole('checkbox', { name: /^Fries/ }).click();
    await shot(kiosk, 'portrait-5-item-sides');
    await kiosk.getByRole('tab', { name: /Add drinks/ }).click();
    await shot(kiosk, 'portrait-6-item-drinks-undecided');
    await kiosk.getByRole('radio', { name: /^Coke Zero/ }).click();
    await expect(kiosk.getByText('₱164.00')).toBeVisible(); // 99 + 40 + 25
    await kiosk.getByRole('button', { name: 'Add to order' }).click();
    await expect(kiosk.getByText('Added to your order')).toBeVisible();
    await kiosk.waitForTimeout(950);
    await shot(kiosk, 'portrait-7-added');

    await kiosk.getByRole('link', { name: 'Complete Order' }).click({ timeout: 15_000 });
    await expect(kiosk.getByRole('heading', { name: 'Your order' })).toBeVisible();
    await expect(kiosk.getByText('Coke Zero')).toBeVisible();
    await shot(kiosk, 'portrait-8-cart');

    // Edit the line from the cart: it opens with the choices filled in and comes back to the cart.
    await kiosk.getByRole('button', { name: /Edit/ }).click();
    await kiosk.getByRole('tab', { name: /Add drinks/ }).click();
    await expect(kiosk.getByRole('radio', { name: /^Coke Zero/ })).toBeChecked();
    await kiosk.getByRole('button', { name: 'Update order' }).click();
    await expect(kiosk.getByRole('heading', { name: 'Your order' })).toBeVisible({ timeout: 15_000 });

    await kiosk.getByRole('link', { name: 'Proceed to payment' }).click();
    await shot(kiosk, 'portrait-9-order-type');
    await kiosk.getByRole('button', { name: /Dine In/ }).click();
    await shot(kiosk, 'portrait-10-payment');
    await kiosk.getByRole('button', { name: /^With Discounts/ }).click();
    await shot(kiosk, 'portrait-11-discount');
    await kiosk.getByRole('radio', { name: 'PWD' }).click();
    await kiosk.getByRole('button', { name: 'Place order' }).click();
    await expect(kiosk.getByRole('status').filter({ hasText: /Sending your order/ })).toBeVisible();
    await shot(kiosk, 'portrait-12-processing');

    const number = kiosk.getByLabel(/Order number \d+/);
    await expect(number).toBeVisible({ timeout: 20_000 });
    await expect(kiosk.getByText('Pay at the counter and enjoy!')).toBeVisible();
    await expect(kiosk.getByText('With Discounts: PWD')).toBeVisible();
    await shot(kiosk, 'portrait-13-done');
    const orderNumber = Number(((await number.getAttribute('aria-label')) ?? '').replace('Order number ', ''));

    // The kitchen sees the customer's choices.
    await kitchen.goto('/kitchen');
    const ticket = kitchen.getByRole('listitem').filter({ hasText: `#${orderNumber}` }).filter({ has: kitchen.getByRole('button') });
    await expect(ticket).toContainText(`Chicken Meal ${stamp}`);
    await expect(ticket).toContainText('Fries');
    await expect(ticket).toContainText('Coke Zero');

    // ---- Landscape -------------------------------------------------------------------------------------
    await kiosk.setViewportSize({ width: 1920, height: 1080 });
    await kiosk.getByRole('button', { name: 'Start a new order' }).click();
    await shot(kiosk, 'landscape-1-welcome');
    await kiosk.getByRole('link', { name: /Tap anywhere to begin/ }).click();
    await shot(kiosk, 'landscape-2-menu');

    // A deal with a fixed part: nothing to choose, so the page is just quantity, with what it includes.
    await kiosk.getByRole('button', { name: new RegExp(`Chicken Buy 1 Take 1 ${stamp}`) }).click();
    await expect(kiosk.getByText('Includes 2 x')).toBeVisible();
    await shot(kiosk, 'landscape-3-deal');
    await kiosk.getByRole('button', { name: 'Add to order' }).click();
    await kiosk.getByRole('link', { name: 'Complete Order' }).click({ timeout: 15_000 });
    await shot(kiosk, 'landscape-4-cart');
    await kiosk.getByRole('link', { name: 'Proceed to payment' }).click();
    await kiosk.getByRole('button', { name: /Take Out/ }).click();
    await shot(kiosk, 'landscape-5-payment');
    await kiosk.getByRole('button', { name: /^Cash/ }).click();
    await kiosk.getByRole('button', { name: 'Place order' }).click();
    await expect(kiosk.getByLabel(/Order number \d+/)).toBeVisible({ timeout: 20_000 });
    await shot(kiosk, 'landscape-6-done');
  });
});
