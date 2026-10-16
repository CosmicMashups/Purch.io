import { call, unlockOnTill } from './support/api';
import { expect, test } from './support/fixtures';

interface Ingredient {
  id: string;
  name: string;
  quantityOnHand: number;
  lowStockThreshold: number | null;
}

const stamp = () => Date.now().toString(36);

async function newIngredient(token: string, branchId: string, name: string, sku: string | null, stock: number) {
  const created = await call<Ingredient>('POST', '/inventory-items', {
    token,
    body: { name, sku, baseUnit: 'g', packagingUnit: 'sack', packagingSize: 1000, lowStockThreshold: null },
  });
  await call('POST', '/inventory/movements', {
    token,
    body: { itemId: null, inventoryItemId: created.id, branchId, type: 0, quantity: stock, note: 'e2e opening', reasonCategory: null, photoUrl: null, supplierReference: null },
  });
  return created;
}

const stockOf = async (token: string, id: string) => (await call<Ingredient[]>('GET', '/inventory-items', { token })).find((i) => i.id === id)!;

test.describe('inventory depth', () => {
  test('a manager counts a line, reviews and applies a stock take with an approver PIN, changing stock by the variance only', async ({ page, signInAs, seed, ip }) => {
    const boss = await unlockOnTill(seed, 'admin', ip);
    const name = `Cocoa ${stamp()}`;
    const ing = await newIngredient(boss.accessToken, seed.branchId, name, null, 10);

    await signInAs('manager');
    await page.goto('/inventory/stock-takes');
    await page.getByRole('button', { name: 'New stock take' }).click();
    await page.getByLabel('Name').fill(`Count ${name}`);
    await page.getByRole('button', { name: 'Create stock take' }).click();
    await expect(page.getByRole('button', { name: 'Start counting' })).toBeVisible();
    await page.getByRole('button', { name: 'Start counting' }).click();

    const input = page.getByLabel(`Counted quantity for ${name}`);
    await input.fill('8');
    await input.locator('xpath=ancestor::li[1]').getByRole('button', { name: 'Save count' }).click();
    await expect(page.getByText('1 counted').first()).toBeVisible();

    // Stock moves after the count: the variance is applied as a delta on top of it, not an overwrite.
    await call('POST', '/inventory/movements', {
      token: boss.accessToken,
      body: { itemId: null, inventoryItemId: ing.id, branchId: seed.branchId, type: 0, quantity: 5, note: 'e2e late delivery', reasonCategory: null, photoUrl: null, supplierReference: null },
    });

    await page.getByRole('button', { name: 'Move to review' }).click();
    await page.getByRole('button', { name: 'Apply counts' }).click();
    await page.getByLabel('Manager or admin PIN').fill(seed.pins.admin);
    await page.getByRole('button', { name: 'Approve' }).click();
    await expect(page.getByText('Stock take applied').first()).toBeVisible();

    // Counted 8 against 10 expected: variance -2 applied to the 15 now on hand.
    expect((await stockOf(boss.accessToken, ing.id)).quantityOnHand).toBe(13);
  });

  test('a catalog CSV creates a category and an item and updates one ingredient after review and an approver PIN', async ({ page, signInAs, seed, ip }) => {
    const boss = await unlockOnTill(seed, 'admin', ip);
    const tag = stamp();
    const sku = `E2E-${tag}`;
    const existing = await newIngredient(boss.accessToken, seed.branchId, `Sugar ${tag}`, sku, 4);

    await signInAs('manager');
    await page.goto('/inventory/import');
    const csv = [
      'type,sku,barcode,name,category,price,cost,reorder_threshold,unit,active',
      `ingredient,${sku},,Sugar ${tag},,,,7,,`,
      `item,NEW-${tag},,Zebra Cake ${tag},Zebra Bakes ${tag},45,,,,`,
    ].join('\n');
    await page.locator('input[type=file]').setInputFiles({ name: 'catalog.csv', mimeType: 'text/csv', buffer: Buffer.from(csv) });
    await page.getByRole('button', { name: 'Upload and review' }).click();

    await expect(page.getByRole('heading', { name: /Will create/ })).toBeVisible();
    await page.getByLabel('Confirm all').check();
    await page.getByRole('button', { name: 'Commit import' }).click();
    await page.getByLabel('Manager or admin PIN').fill(seed.pins.admin);
    await page.getByRole('button', { name: 'Approve' }).click();

    await expect(page.getByRole('heading', { name: 'Import complete' })).toBeVisible();
    await expect(page.getByText('1 categories created')).toBeVisible();
    await expect(page.getByText('1 updated')).toBeVisible();
    expect((await stockOf(boss.accessToken, existing.id)).lowStockThreshold).toBe(7);
    const items = await call<{ name: string }[]>('GET', '/items', { token: boss.accessToken });
    expect(items.some((i) => i.name === `Zebra Cake ${tag}`)).toBe(true);
  });
});
