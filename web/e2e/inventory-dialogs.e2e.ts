import { expect, test } from './support/fixtures';

test.describe('inventory forms', () => {
  test('an ingredient is stocked in from the Record movement dialog, found by typing', async ({ page, signInAs }) => {
    await signInAs('admin');
    const name = `Flour ${Date.now().toString(36)}`;

    await page.goto('/inventory/ingredients');
    await page.getByRole('button', { name: 'Add Ingredient' }).click();
    await page.getByLabel('Name').fill(name);
    await page.getByLabel('Base unit').fill('g');
    await page.getByLabel('Packaging unit').fill('sack');
    await page.getByLabel('Packaging size').fill('1000');
    await page.getByRole('button', { name: 'Add', exact: true }).click();
    await expect(page.getByRole('dialog')).toBeHidden();

    await page.goto('/inventory/movements');
    await expect(page.getByLabel('Branch')).toBeVisible();
    await page.getByRole('button', { name: 'Record movement' }).click();
    const dialog = page.getByRole('dialog', { name: 'Record movement' });
    await dialog.getByRole('combobox', { name: 'Item or ingredient' }).fill(name.slice(0, 8));
    await page.getByRole('option', { name: new RegExp(name) }).click();
    await dialog.getByLabel('Branch').selectOption({ index: 1 });
    await dialog.getByLabel('Quantity').fill('500');
    await dialog.getByRole('button', { name: 'Record movement' }).click();

    await expect(dialog).toBeHidden();
    await expect(page.getByText(name).first()).toBeVisible();
    await expect(page.getByText('Stock-In: 500')).toBeVisible();
  });

  test('transfers, suppliers and purchase orders open their forms in a dialog, not on the page', async ({ page, signInAs }) => {
    await signInAs('admin');
    const pages: [string, string, string][] = [
      ['/inventory/transfers', 'New transfer', 'Send from'],
      ['/inventory/suppliers', 'Add supplier', 'Specialization (optional)'],
      ['/inventory/purchase-orders', 'New purchase order', 'Deliver to branch'],
      ['/inventory/incoming-receiving', 'Record a delivery', 'Date of delivery'],
    ];
    for (const [path, button, field] of pages) {
      await page.goto(path);
      await expect(page.getByLabel(field)).toHaveCount(0);
      await page.getByRole('button', { name: button }).click();
      await expect(page.getByRole('dialog').getByLabel(field)).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByRole('dialog')).toBeHidden();
    }
  });
});
