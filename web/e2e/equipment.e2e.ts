import { call } from './support/api';
import { expect, test } from './support/fixtures';

test.describe('equipment', () => {
  test('a machine going out of service shows its items as out of stock at the till, and coming back clears it', async ({ page, signInAs, actor, seed }) => {
    // Two waits of 11s below, to let the till's 10s catalog freshness lapse.
    test.setTimeout(120_000);
    const tokens = await signInAs('admin');
    const stamp = Date.now().toString(36);
    const machine = `Ice cream machine ${stamp}`;
    const sundae = `Hot Fudge Sundae ${stamp}`;

    // An item with stock, so equipment is the only thing that can make it show as out of stock.
    const item = await call<{ id: string }>('POST', '/items', {
      token: tokens.accessToken,
      body: { name: sundae, sku: null, barcode: null, categoryId: null, basePrice: 90, imageUrl: null, pricingType: 0 },
    });
    await call('POST', '/inventory/movements', {
      token: tokens.accessToken,
      body: { itemId: item.id, branchId: seed.branchId, type: 0, quantity: 50, note: null, reasonCategory: null, photoUrl: null, supplierReference: null },
    });

    // Add the machine from the Equipment page.
    await page.goto('/inventory/equipment');
    await expect(page.getByRole('link', { name: 'Back to Inventory' })).toBeVisible();
    await page.getByRole('button', { name: 'Add Equipment' }).click();
    await page.getByLabel('Name').fill(machine);
    await page.getByRole('button', { name: 'Add', exact: true }).click();
    await expect(page.getByText(machine, { exact: true })).toBeVisible();

    // Say the sundae needs it, from the item's own edit dialog.
    await page.goto('/catalog/items');
    await page.getByPlaceholder('Search items…').fill(sundae);
    await page.getByRole('button', { name: `Actions for ${sundae}` }).click();
    await page.getByRole('menuitem', { name: 'Edit' }).click();
    const dialog = page.getByRole('dialog', { name: 'Edit Item' });
    await dialog.getByRole('combobox', { name: 'Required equipment' }).click();
    await page.getByRole('option', { name: new RegExp(machine) }).click();
    await dialog.getByRole('button', { name: 'Save Changes' }).click();
    await expect(dialog).toBeHidden();
    const linked = await call<{ equipmentName: string }[]>('GET', `/items/${item.id}/equipment`, { token: tokens.accessToken });
    expect(linked.map((l) => l.equipmentName)).toEqual([machine]);

    // The till shows it as available.
    const till = await actor('cashier');
    await till.goto('/sell');
    const tile = till.getByRole('button', { name: new RegExp(sundae) });
    await expect(tile).toBeVisible();
    await expect(tile).not.toContainText('Out of stock');

    // Mark the machine out of service.
    await page.goto('/inventory/equipment');
    await page.getByRole('button', { name: `Actions for ${machine}` }).click();
    await page.getByRole('menuitem', { name: 'Mark out of service' }).click();
    await expect(page.getByText(/is out of service\. 1 item shows as out of stock/)).toBeVisible();

    // The till keeps a catalog fresh for 10 seconds, so wait that out; a reload then fetches the current one.
    await till.waitForTimeout(11_000);
    await till.reload();
    await expect(till.getByRole('button', { name: new RegExp(sundae) })).toContainText('Out of stock');

    // Back in service, and it clears.
    await page.getByRole('button', { name: `Actions for ${machine}` }).click();
    await page.getByRole('menuitem', { name: 'Mark operational' }).click();
    await expect(page.getByText(/is now operational/)).toBeVisible();
    await till.waitForTimeout(11_000);
    await till.reload();
    await expect(till.getByRole('button', { name: new RegExp(sundae) })).not.toContainText('Out of stock');
  });

  test('equipment can be made inactive, deleted and restored from its row menu', async ({ page, signInAs }) => {
    await signInAs('admin');
    const name = `Deep fryer ${Date.now().toString(36)}`;
    await page.goto('/inventory/equipment');
    await page.getByRole('button', { name: 'Add Equipment' }).click();
    await page.getByLabel('Name').fill(name);
    await page.getByRole('button', { name: 'Add', exact: true }).click();
    await expect(page.getByText(name, { exact: true })).toBeVisible();

    await page.getByRole('button', { name: `Actions for ${name}` }).click();
    await page.getByRole('menuitem', { name: 'Delete' }).click();
    const sure = page.getByRole('dialog', { name: `Delete ${name}?` });
    await expect(sure.getByText(/You can restore it later/)).toBeVisible();
    await sure.getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText(name, { exact: true })).toBeHidden();

    await page.getByRole('button', { name: 'Deleted' }).click();
    await page.getByRole('button', { name: `Restore ${name}` }).click();
    await expect(page.getByText(/restored as an inactive equipment/)).toBeVisible();
  });

  test('a warehouse user can open the Equipment page from the inventory hub', async ({ page, signInAs }) => {
    await signInAs('warehouse');
    await page.goto('/inventory');
    await page.getByRole('link', { name: /Equipment/ }).first().click();
    await expect(page).toHaveURL(/\/inventory\/equipment$/);
    await expect(page.getByRole('heading', { name: 'Equipment' })).toBeVisible();
  });
});
